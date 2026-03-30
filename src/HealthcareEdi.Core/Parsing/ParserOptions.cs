namespace HealthcareEdi.Core.Parsing;

/// <summary>
/// Configuration options for the EDI parser engine.
/// </summary>
public sealed class ParserOptions
{
    /// <summary>
    /// Validation strictness level. Default is Lenient.
    /// </summary>
    public ValidationMode ValidationMode { get; set; } = ValidationMode.Lenient;

    /// <summary>
    /// Buffer size for file streaming (bytes). Default is 64KB.
    /// </summary>
    public int StreamBufferSize { get; set; } = 65536;

    /// <summary>
    /// Maximum number of validation issues to collect before stopping (Lenient mode).
    /// Prevents memory issues on extremely malformed files. Default is 10,000.
    /// </summary>
    public int MaxValidationIssues { get; set; } = 10_000;

    /// <summary>
    /// Whether to trim whitespace from element values. Default is true.
    /// Many real-world files pad elements with spaces.
    /// </summary>
    public bool TrimElementValues { get; set; } = true;

    /// <summary>
    /// Registered extended parsers for payer-specific customization.
    /// Consumers add parsers via <see cref="RegisterExtension{T}"/>.
    /// </summary>
    private readonly List<object> _extendedParsers = [];

    /// <summary>
    /// Read-only access to registered extended parsers.
    /// </summary>
    public IReadOnlyList<object> ExtendedParsers => _extendedParsers;

    /// <summary>
    /// Register a custom parser extension for a transaction type.
    /// </summary>
    public ParserOptions RegisterExtension<T>(IExtendedParser<T> parser) where T : class
    {
        _extendedParsers.Add(parser);
        return this;
    }
}

/// <summary>
/// Controls how strictly the parser validates EDI content.
/// </summary>
public enum ValidationMode
{
    /// <summary>
    /// Rejects non-compliant content. Throws EdiValidationException.
    /// Use for compliance testing and clearinghouse validation.
    /// </summary>
    Strict,

    /// <summary>
    /// Parses all content possible. Collects issues into ValidationIssues.
    /// Does not throw on non-fatal issues. Default mode.
    /// </summary>
    Lenient,

    /// <summary>
    /// No validation. Best-effort parsing. Fastest mode.
    /// Unmapped segments go to UnmappedSegments collection.
    /// </summary>
    None
}

/// <summary>
/// Plugin interface for payer-specific loop/segment extensions.
/// </summary>
public interface IExtendedParser<T> where T : class
{
    /// <summary>
    /// Called after standard parsing to apply payer-specific mappings.
    /// </summary>
    void PostProcess(T model, IReadOnlyList<string> rawSegments, DelimiterContext delimiters);
}
