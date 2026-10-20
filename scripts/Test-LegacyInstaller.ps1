$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$root = Join-Path $repo ('artifacts/legacy-installer-test-' + [guid]::NewGuid().ToString('N'))
$programs = Join-Path $root 'Programs'
$payload = Join-Path $root 'payload'
$output = Join-Path $root 'installers'
$guid = [guid]::NewGuid().ToString().ToUpperInvariant()
$appName = 'EventLens Upgrade Probe ' + $guid.Substring(0, 8)
$key = "HKCU:\Software\Microsoft\Windows\CurrentVersion\Uninstall\{$guid}_is1"
$legacy = Join-Path $programs "Elka Software\$appName"
$current = Join-Path $programs "ElkaSoft\$appName"
$compiler = Join-Path ${env:ProgramFiles(x86)} 'Inno Setup 6\ISCC.exe'
$bootstrapper = Join-Path $repo 'artifacts/dependencies/MicrosoftEdgeWebview2Setup.exe'
New-Item -ItemType Directory -Path $root,$output -Force | Out-Null

function Assert($condition, [string]$label) {
    if (-not $condition) { throw $label }
    Write-Output "PASS $label"
}
function Run-Setup([string]$file, [string]$arguments) {
    $process = Start-Process -FilePath $file -ArgumentList $arguments -WindowStyle Hidden -PassThru -Wait
    return $process.ExitCode
}
function Remove-Probe {
    if (Test-Path -LiteralPath $key) {
        $location = [IO.Path]::GetFullPath((Get-ItemProperty -LiteralPath $key).InstallLocation)
        Assert ($location.StartsWith([IO.Path]::GetFullPath($programs) + '\', [StringComparison]::OrdinalIgnoreCase)) 'probe uninstall stays inside workspace'
        $code = Run-Setup (Join-Path $location 'unins000.exe') '/VERYSILENT /SUPPRESSMSGBOXES /NORESTART'
        Assert ($code -eq 0 -and -not (Test-Path -LiteralPath $key)) 'isolated probe uninstalls cleanly'
    }
}

dotnet publish (Join-Path $repo 'tests/InstallerProbe/InstallerProbe.csproj') -c Release -o $payload
if ($LASTEXITCODE -ne 0) { throw 'Probe build failed.' }
$oldSource = (git -C $repo show v0.1.0-dev.3:Installer/ELKA.EventLens.iss) -join "`r`n"
if ($LASTEXITCODE -ne 0) { throw 'The dev.3 installer source tag is required.' }
$newSource = Get-Content -LiteralPath (Join-Path $repo 'Installer/ELKA.EventLens.iss') -Raw
foreach ($version in @('0.1.0-dev.3', '0.1.0-dev.4')) {
    $source = if ($version.EndsWith('.3')) { $oldSource.Replace('#define MyCompanyFolderName "ElkaSoft"', '#define MyCompanyFolderName "Elka Software"') } else { $newSource }
    $source = $source.Replace('B536C27A-C350-4B80-9C78-E3AC1AEE58DA', $guid)
    $source = $source.Replace('#define MyAppName "ELKA EventLens"', "#define MyAppName `"$appName`"")
    $source = $source.Replace('{autopf}', $programs)
    $source = $source.Replace('..\src\', (Join-Path $repo 'src') + '\')
    $source = $source.Replace('Branding\', (Join-Path $repo 'Installer/Branding') + '\')
    $script = Join-Path $root "$version.iss"
    [IO.File]::WriteAllText($script, $source)
    & $compiler /Q "/DReleaseVersion=$version" "/DSourcePublishDir=$payload" "/DInstallerOutputDir=$output" "/DWebViewBootstrapper=$bootstrapper" $script
    if ($LASTEXITCODE -ne 0) { throw 'Probe installer compile failed.' }
}
$oldSetup = Join-Path $output 'ELKA_EventLens_Setup_0.1.0-dev.3.exe'
$newSetup = Join-Path $output 'ELKA_EventLens_Setup_0.1.0-dev.4.exe'
$common = '/CURRENTUSER /VERYSILENT /SUPPRESSMSGBOXES /NORESTART /NOICONS /TASKS=""'
$sibling = Join-Path $programs 'Elka Software\UnrelatedApp\sentinel.txt'
New-Item -ItemType Directory -Path (Split-Path -Parent $sibling) -Force | Out-Null
[IO.File]::WriteAllText($sibling, 'Unrelated app must survive')

try {
    foreach ($scenario in @('remembered', 'old-updater', 'running', 'custom')) {
        $target = if ($scenario -eq 'custom') { Join-Path $programs 'Custom Tools\EventLens' } else { $legacy }
        $code = Run-Setup $oldSetup "$common /DIR=`"$target`" /LOG=`"$root\$scenario-old.log`""
        Assert ($code -eq 0 -and (Test-Path -LiteralPath (Join-Path $target 'ELKA.EventLens.App.exe'))) "$scenario old installation seeded"
        $arguments = "$common /LOG=`"$root\$scenario-new.log`""
        $helper = $null
        try {
            if ($scenario -eq 'old-updater') {
                $helper = Start-Process powershell.exe -ArgumentList '-NoProfile -Command Start-Sleep -Seconds 12' -WindowStyle Hidden -PassThru
                $arguments += " /DIR=`"$legacy`" /AUTOUPDATE=1 /ELKAPID=$($helper.Id)"
            }
            if ($scenario -eq 'running') {
                $helper = Start-Process (Join-Path $legacy 'ELKA.EventLens.App.exe') -ArgumentList '--hold' -WindowStyle Hidden -PassThru
                Start-Sleep -Seconds 1
            }
            $code = Run-Setup $newSetup $arguments
            if ($scenario -eq 'running') {
                Assert ($code -ne 0 -and (Test-Path -LiteralPath (Join-Path $legacy 'ELKA.EventLens.App.exe'))) 'running legacy app is not partially uninstalled'
            } else {
                $expected = if ($scenario -eq 'custom') { $target } else { $current }
                Assert ($code -eq 0) "$scenario upgrade succeeded"
                Assert ((Get-ItemProperty -LiteralPath $key).InstallLocation.TrimEnd('\') -eq $expected) "$scenario registry points to correct install directory"
                Assert (Test-Path -LiteralPath (Join-Path $expected 'ELKA.EventLens.App.exe')) "$scenario new executable exists"
                if ($scenario -ne 'custom') { Assert (-not (Test-Path -LiteralPath (Join-Path $legacy 'ELKA.EventLens.App.exe'))) "$scenario old executable removed" }
                if ($scenario -eq 'old-updater') {
                    Assert ($helper.HasExited) 'updater waited for launching process'
                    Start-Sleep -Seconds 2
                    Assert (Test-Path -LiteralPath (Join-Path $expected 'probe-started.txt')) 'updater restarted the new executable'
                }
            }
        } finally {
            if ($helper -and -not $helper.HasExited) { Stop-Process -Id $helper.Id -Force }
            if ($helper) { $helper.Dispose() }
        }
        Assert ((Get-Content -LiteralPath $sibling -Raw) -eq 'Unrelated app must survive') "$scenario preserved unrelated company-folder contents"
        Remove-Probe
    }
} finally { Remove-Probe }
Write-Output "PASS installer regression scenarios. Logs: $root"
