namespace HealthcareEdi.Core.Parsing;

/// <summary>
/// Holds the delimiters detected from the ISA segment.
/// ISA is always exactly 106 characters, making delimiter detection positional.
/// </summary>
public sealed class DelimiterContext
{
    /// <summary>Element separator (ISA position 4, e.g., '*')</summary>
    public char ElementSeparator { get; }

    /// <summary>Component separator for composite elements (ISA position 105, e.g., ':')</summary>
    public char ComponentSeparator { get; }

    /// <summary>Segment terminator (ISA position 106, e.g., '~')</summary>
    public char SegmentTerminator { get; }

    /// <summary>Repetition separator (ISA11, e.g., '^')</summary>
    public char RepetitionSeparator { get; }

    public DelimiterContext(char elementSeparator, char componentSeparator,
        char segmentTerminator, char repetitionSeparator)
    {
        ElementSeparator = elementSeparator;
        ComponentSeparator = componentSeparator;
        SegmentTerminator = segmentTerminator;
        RepetitionSeparator = repetitionSeparator;
    }

    /// <summary>
    /// Detects delimiters from the raw ISA segment (first 106 characters of an X12 file).
    /// </summary>
    public static DelimiterContext DetectFromIsa(ReadOnlySpan<char> isaBlock)
    {
        if (isaBlock.Length < 106)
            throw new EdiParseException("ISA segment must be at least 106 characters.");

        // ISA is fixed-length: positions are absolute
        char elementSep = isaBlock[3];   // Character after "ISA"
        char repetitionSep = isaBlock[82]; // ISA11 at fixed position (after 11th element)
        char componentSep = isaBlock[104]; // ISA16 (second-to-last character)
        char segmentTerm = isaBlock[105];  // Last character of ISA

        // Validate we got reasonable delimiters
        if (elementSep == componentSep || elementSep == segmentTerm || componentSep == segmentTerm)
            throw new EdiParseException(
                $"Detected ambiguous delimiters: element='{elementSep}', component='{componentSep}', segment='{segmentTerm}'");

        return new DelimiterContext(elementSep, componentSep, segmentTerm, repetitionSep);
    }

    /// <summary>
    /// Splits a raw segment string into its elements using the element separator.
    /// </summary>
    public string[] SplitElements(string segment)
        => segment.Split(ElementSeparator);

    /// <summary>
    /// Splits a composite element into its sub-components using the component separator.
    /// </summary>
    public string[] SplitComponents(string element)
        => element.Split(ComponentSeparator);
}
