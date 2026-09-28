using ELKA.EventLens.App.Models;

namespace ELKA.EventLens.App.Services;

public sealed class EventExplanationService
{
    public EventExplanation Explain(EventLogEntry entry)
    {
        var source = entry.ProviderName ?? string.Empty;
        var message = entry.Message ?? string.Empty;
        var combined = $"{source} {message}";

        if (source.Contains("Kernel-Power", StringComparison.OrdinalIgnoreCase) && entry.EventId == 41)
        {
            return new EventExplanation
            {
                MatchedPattern = "Kernel-Power 41",
                ShortSummary = "Unexpected shutdown. Windows did not shut down cleanly.",
                PossibleCauses =
                [
                    "Power loss or forced power-off",
                    "System crash or bugcheck",
                    "Power supply, RAM, CPU, driver, or thermal instability"
                ],
                SeverityAdvice = "Treat this as important if it repeats or appears near crashes, freezes, or restarts.",
                RecommendedNextSteps =
                [
                    "Check temperatures and power stability.",
                    "Review events immediately before the shutdown.",
                    "Update chipset, GPU, storage, and firmware drivers.",
                    "Run memory and storage diagnostics if the pattern repeats."
                ]
            };
        }

        if (source.Contains("WHEA-Logger", StringComparison.OrdinalIgnoreCase))
        {
            return new EventExplanation
            {
                MatchedPattern = "WHEA-Logger",
                ShortSummary = "Hardware error reported by Windows Hardware Error Architecture.",
                PossibleCauses =
                [
                    "CPU, RAM, motherboard, PCIe device, or storage controller issue",
                    "Unstable overclock or undervolt",
                    "Driver or firmware exposing hardware instability"
                ],
                SeverityAdvice = "Investigate promptly, especially if the event mentions corrected hardware error, cache hierarchy error, or bus/interconnect error.",
                RecommendedNextSteps =
                [
                    "Remove overclocks or undervolts and retest.",
                    "Check BIOS, chipset, storage, and GPU driver updates.",
                    "Run memory, CPU, and storage diagnostics.",
                    "Look for matching Event IDs and error details in the raw XML."
                ]
            };
        }

        if (ContainsAny(combined, "Disk", "Ntfs", "storahci", "stornvme", "iaStor", "volmgr"))
        {
            return new EventExplanation
            {
                MatchedPattern = "Storage",
                ShortSummary = "Storage, file system, disk, or controller issue.",
                PossibleCauses =
                [
                    "Disk health problem",
                    "Cable, controller, driver, firmware, or power issue",
                    "File system corruption or delayed write failure"
                ],
                SeverityAdvice = "Back up important data before making changes if these errors repeat.",
                RecommendedNextSteps =
                [
                    "Check drive health with SMART tools.",
                    "Review storage controller and chipset drivers.",
                    "Run chkdsk or vendor diagnostics when appropriate.",
                    "Look for matching disk resets, bad blocks, or NTFS errors."
                ]
            };
        }

        if (source.Contains("Application Error", StringComparison.OrdinalIgnoreCase))
        {
            return new EventExplanation
            {
                MatchedPattern = "Application Error",
                ShortSummary = "An application crashed.",
                PossibleCauses =
                [
                    "Application bug or incompatible version",
                    "Missing runtime dependency",
                    "Faulting module, driver, plugin, or corrupted install"
                ],
                SeverityAdvice = "Usually limited to the named app unless many apps are crashing.",
                RecommendedNextSteps =
                [
                    "Identify the faulting application and module in the event message.",
                    "Update or repair the application.",
                    "Check for related .NET Runtime, Windows Error Reporting, or driver events."
                ]
            };
        }

        if (source.Contains("DistributedCOM", StringComparison.OrdinalIgnoreCase))
        {
            return new EventExplanation
            {
                MatchedPattern = "DistributedCOM",
                ShortSummary = "Windows reported a DCOM permission or activation warning.",
                PossibleCauses =
                [
                    "Normal Windows component permission noise",
                    "App trying to start a COM component without expected rights",
                    "Configuration mismatch after an update"
                ],
                SeverityAdvice = "Often harmless unless it lines up with visible symptoms.",
                RecommendedNextSteps =
                [
                    "Do not change DCOM permissions unless you can connect it to a real problem.",
                    "Search the AppID or CLSID from the message if symptoms are present.",
                    "Check whether the warning appears at the same time as app failures."
                ]
            };
        }

        if (entry.Severity == EventSeverity.Critical)
        {
            return new EventExplanation
            {
                MatchedPattern = "Critical fallback",
                ShortSummary = BuildFallbackSummary(entry, "Critical Windows event."),
                PossibleCauses =
                [
                    "System-level crash, service failure, driver issue, or hardware-related failure"
                ],
                SeverityAdvice = "Critical events deserve review, especially if recent or repeated.",
                RecommendedNextSteps =
                [
                    "Read the full message and raw XML.",
                    "Search by Event ID and source.",
                    "Compare timestamps with crashes, freezes, restarts, or app failures."
                ]
            };
        }

        return new EventExplanation
        {
            ShortSummary = BuildFallbackSummary(entry, entry.Severity == EventSeverity.Warning
                ? "Windows reported a warning."
                : "Windows reported an error."),
            PossibleCauses =
            [
                "The source-specific message usually has the key clue.",
                "Nearby events may identify the root cause."
            ],
            SeverityAdvice = entry.Severity == EventSeverity.Warning
                ? "Warnings are worth tracking when repeated or tied to symptoms."
                : "Errors should be reviewed if recent, repeated, or related to a visible failure.",
            RecommendedNextSteps =
            [
                "Search by Event ID, source, and a few message keywords.",
                "Check events immediately before and after this one.",
                "Avoid registry or permission changes until the cause is clear."
            ]
        };
    }

    private static bool ContainsAny(string value, params string[] needles)
    {
        return needles.Any(needle => value.Contains(needle, StringComparison.OrdinalIgnoreCase));
    }

    private static string BuildFallbackSummary(EventLogEntry entry, string defaultSummary)
    {
        if (string.IsNullOrWhiteSpace(entry.Message))
        {
            return defaultSummary;
        }

        var firstLine = entry.Message
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(firstLine))
        {
            return defaultSummary;
        }

        return firstLine.Length > 160 ? $"{firstLine[..157]}..." : firstLine;
    }
}
