namespace ELKA.EventLens.App.Models;

public sealed class EventExplanation
{
    public string ShortSummary { get; init; } = "Windows reported an event that may need review.";

    public IReadOnlyList<string> PossibleCauses { get; init; } = Array.Empty<string>();

    public string SeverityAdvice { get; init; } = "Review this event with nearby events before making changes.";

    public IReadOnlyList<string> RecommendedNextSteps { get; init; } = Array.Empty<string>();

    public string MatchedPattern { get; init; } = "General";
}
