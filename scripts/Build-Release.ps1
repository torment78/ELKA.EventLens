param(
    [ValidatePattern('^\d+\.\d+\.\d+-dev\.\d+$')]
    [string]$Version = '0.1.0-dev.2',
    [ValidateSet('Release', 'Debug')][string]$Configuration = 'Release'
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repoRoot 'src/ELKA.EventLens.App/ELKA.EventLens.App.csproj'
$compiler = Join-Path ([Environment]::GetFolderPath('ProgramFilesX86')) 'Inno Setup 6/ISCC.exe'
$installerScript = Join-Path $repoRoot 'Installer/ELKA.EventLens.iss'
$artifactsRoot = [IO.Path]::GetFullPath((Join-Path $repoRoot 'artifacts'))
$stageRoot = Join-Path $artifactsRoot ('staging/build-' + [guid]::NewGuid().ToString('N'))
$publish = Join-Path $stageRoot 'installed'
$portable = Join-Path $stageRoot 'ElkaEventLensPort'
$output = Join-Path $artifactsRoot 'installer'
$dependencies = Join-Path $artifactsRoot 'dependencies'
$bootstrapper = Join-Path $dependencies 'MicrosoftEdgeWebview2Setup.exe'
$numericVersion = $Version.Split('-')[0]
$zip = Join-Path $output "ELKA_EventLens_Portable_$Version.zip"
$setup = Join-Path $output "ELKA_EventLens_Setup_$Version.exe"

if (-not (Test-Path -LiteralPath $compiler)) { throw 'Inno Setup 6.6 or newer is required.' }
New-Item -ItemType Directory -Path $publish,$portable,$output,$dependencies -Force | Out-Null

try {
    if (-not (Test-Path -LiteralPath $bootstrapper)) {
        Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $bootstrapper
    }
    $signature = Get-AuthenticodeSignature -LiteralPath $bootstrapper
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch 'O=Microsoft Corporation') {
        throw 'The WebView2 bootstrapper did not pass Microsoft signature verification.'
    }

    dotnet publish $project --configuration $Configuration --runtime win-x64 --self-contained true --output $publish "-p:Version=$Version" "-p:FileVersion=$numericVersion.0" "-p:AssemblyVersion=$numericVersion.0" -p:PublishSingleFile=false -p:PublishReadyToRun=true -p:DebugType=None -p:DebugSymbols=false
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed ($LASTEXITCODE)." }
    if (Test-Path -LiteralPath (Join-Path $publish 'ELKA.EventLens.portable')) { throw 'Installed payload must not contain the portable marker.' }

    Copy-Item -Path (Join-Path $publish '*') -Destination $portable -Recurse
    [IO.File]::WriteAllText((Join-Path $portable 'ELKA.EventLens.portable'), 'Portable mode')
    $portableNotes = @"
ELKA EventLens $Version - Development Preview

Run ELKA.EventLens.App.exe from this extracted folder. .NET is bundled.
Microsoft Edge WebView2 Runtime is needed for embedded browsing.
Download it from https://developer.microsoft.com/microsoft-edge/webview2/

Saves, Logs, and WebView2Profile are created beside the executable.
Minimize and close hide to the tray; choose Exit in the tray menu to quit.
Browser sign-in sessions may be tied to a Windows user/machine and are not guaranteed to transfer between PCs.
"@
    [IO.File]::WriteAllText((Join-Path $portable 'README-PORTABLE.txt'), $portableNotes)
    Compress-Archive -LiteralPath $portable -DestinationPath $zip -CompressionLevel Optimal -Force

    & $compiler "/DAppVersion=$numericVersion" "/DReleaseVersion=$Version" "/DSourcePublishDir=$publish" "/DInstallerOutputDir=$output" "/DWebViewBootstrapper=$bootstrapper" $installerScript
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup failed ($LASTEXITCODE)." }
    if (-not (Test-Path -LiteralPath $setup)) { throw 'Installer output is missing.' }

    $hashes = foreach ($file in @($setup, $zip)) {
        $hash = (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash.ToLowerInvariant()
        "$hash  $([IO.Path]::GetFileName($file))"
    }
    [IO.File]::WriteAllLines((Join-Path $output "SHA256SUMS-$Version.txt"), $hashes, [Text.Encoding]::ASCII)
    Get-Item -LiteralPath $setup,$zip | Select-Object Name,Length,LastWriteTime
}
finally {
    # Only this invocation's fresh staging tree may be removed, never a user's portable folder.
    $resolvedStage = [IO.Path]::GetFullPath($stageRoot)
    $allowedParent = [IO.Path]::GetFullPath((Join-Path $artifactsRoot 'staging')) + [IO.Path]::DirectorySeparatorChar
    if ($resolvedStage.StartsWith($allowedParent, [StringComparison]::OrdinalIgnoreCase) -and
        [IO.Path]::GetFileName($resolvedStage) -match '^build-[a-f0-9]{32}$' -and
        (Test-Path -LiteralPath $resolvedStage)) {
        Remove-Item -LiteralPath $resolvedStage -Recurse -Force
    }
}
