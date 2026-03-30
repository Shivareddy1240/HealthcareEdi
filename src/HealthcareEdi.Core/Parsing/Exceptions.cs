using HealthcareEdi.Core.Validation;

namespace HealthcareEdi.Core.Parsing;

/// <summary>
/// Thrown when the EDI file structure is fundamentally unparseable.
/// </summary>
public class EdiParseException : Exception
{
    public string? SegmentId { get; }
    public long? ByteOffset { get; }

    public EdiParseException(string message) : base(message) { }

    public EdiParseException(string message, string segmentId, long byteOffset)
        : base(message)
    {
        SegmentId = segmentId;
        ByteOffset = byteOffset;
    }

    public EdiParseException(string message, Exception innerException)
        : base(message, innerException) { }
}

/// <summary>
/// Thrown in Strict validation mode when a segment/element violates the implementation guide.
/// </summary>
public class EdiValidationException : Exception
{
    public IReadOnlyList<EdiValidationIssue> Issues { get; }

    public EdiValidationException(IReadOnlyList<EdiValidationIssue> issues)
        : base($"EDI validation failed with {issues.Count} issue(s). First: {issues.FirstOrDefault()?.Description}")
    {
        Issues = issues;
    }
}

/// <summary>
/// Thrown when the parser cannot determine 837P vs 837I vs 837D.
/// </summary>
public class TransactionTypeUnresolvableException : EdiParseException
{
    public string? IsaControlNumber { get; }
    public string? GsControlNumber { get; }
    public string? StControlNumber { get; }

    public TransactionTypeUnresolvableException(string? isaControl, string? gsControl, string? stControl)
        : base($"Cannot determine 837 variant. ISA={isaControl}, GS={gsControl}, ST={stControl}")
    {
        IsaControlNumber = isaControl;
        GsControlNumber = gsControl;
        StControlNumber = stControl;
    }
}
