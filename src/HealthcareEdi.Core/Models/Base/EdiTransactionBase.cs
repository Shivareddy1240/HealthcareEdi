using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Validation;

namespace HealthcareEdi.Core.Models.Base;

/// <summary>
/// Base class for all EDI transaction models.
/// Provides envelope data, validation, and PHI redaction.
/// </summary>
public abstract class EdiTransactionBase
{
    // ── Envelope Data ──────────────────────────────────────────
    public IsaSegment InterchangeHeader { get; set; } = new();
    public GsSegment FunctionalGroupHeader { get; set; } = new();
    public StSegment TransactionSetHeader { get; set; } = new();

    // ── Control Numbers (convenience properties) ───────────────
    public string InterchangeControlNumber => InterchangeHeader.InterchangeControlNumber;
    public string GroupControlNumber => FunctionalGroupHeader.GroupControlNumber;
    public string TransactionControlNumber => TransactionSetHeader.TransactionSetControlNumber;

    // ── Validation ─────────────────────────────────────────────
    public List<EdiValidationIssue> ValidationIssues { get; } = [];
    public bool HasParseWarnings => ValidationIssues.Count > 0;
    public bool IsValid => !ValidationIssues.Any(v => v.Severity == ValidationSeverity.Error);

    // ── Raw Data ───────────────────────────────────────────────
    /// <summary>Segments that could not be mapped to any known loop/segment.</summary>
    public List<string> UnmappedSegments { get; } = [];

    /// <summary>Total number of segments in this transaction set (from SE01).</summary>
    public int SegmentCount { get; set; }

    // ── PHI Redaction ──────────────────────────────────────────
    /// <summary>
    /// Redacts Protected Health Information using Safe Harbor method.
    /// Masks names, SSNs, dates of birth, addresses, member IDs, etc.
    /// </summary>
    public virtual void RedactPhi()
    {
        // Base implementation redacts envelope-level PHI
        InterchangeHeader.SenderId = MaskValue(InterchangeHeader.SenderId);
        InterchangeHeader.ReceiverId = MaskValue(InterchangeHeader.ReceiverId);
    }

    /// <summary>
    /// Masks a string value: preserves first char, replaces rest with asterisks.
    /// Returns "***" for null/empty values that contained data.
    /// </summary>
    protected static string MaskValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        if (value.Length <= 2) return "***";
        return value[0] + new string('*', value.Length - 1);
    }

    /// <summary>Fully redacts a value (e.g., SSN).</summary>
    protected static string FullRedact(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        return new string('*', value.Length);
    }
}

/// <summary>
/// Represents a transaction that failed to parse.
/// </summary>
public sealed class FailedTransaction
{
    public string? StControlNumber { get; init; }
    public string? SeControlNumber { get; init; }
    public List<string> RawSegments { get; init; } = [];
    public Exception Exception { get; init; } = null!;
    public long ByteOffset { get; init; }
}

/// <summary>
/// Result of a batch parse operation.
/// </summary>
public sealed class EdiBatchResult<TModel> where TModel : EdiTransactionBase
{
    public IReadOnlyList<TModel> Transactions { get; init; } = [];
    public IReadOnlyList<FailedTransaction> FailedTransactions { get; init; } = [];
    public IsaSegment? InterchangeHeader { get; init; }
    public GsSegment? FunctionalGroupHeader { get; init; }
    public long ParseDurationMs { get; init; }

    public int TotalCount => Transactions.Count + FailedTransactions.Count;
    public bool HasFailures => FailedTransactions.Count > 0;
}
