namespace HealthcareEdi.Core.Validation;

/// <summary>
/// Represents a single validation issue found during parsing.
/// </summary>
public sealed class EdiValidationIssue
{
    public ValidationSeverity Severity { get; init; }
    public string SegmentId { get; init; } = string.Empty;
    public int? ElementPosition { get; init; }
    public string? LoopId { get; init; }
    public string Description { get; init; } = string.Empty;
    public string? RawSegment { get; init; }

    public override string ToString()
        => $"[{Severity}] {SegmentId}" +
           (ElementPosition.HasValue ? $"-{ElementPosition}" : "") +
           (LoopId is not null ? $" (Loop {LoopId})" : "") +
           $": {Description}";
}

public enum ValidationSeverity
{
    Info,
    Warning,
    Error
}
