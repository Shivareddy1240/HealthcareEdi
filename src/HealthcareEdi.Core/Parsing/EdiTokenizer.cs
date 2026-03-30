namespace HealthcareEdi.Core.Parsing;

/// <summary>
/// Low-level tokenizer that reads an EDI file and splits it into raw segments,
/// grouped by transaction set (ST/SE boundaries).
/// </summary>
public sealed class EdiTokenizer
{
    private readonly ParserOptions _options;

    public EdiTokenizer(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
    }

    /// <summary>
    /// Reads the entire file and returns the delimiter context + list of transaction segment groups.
    /// Each group is the list of raw segment strings between ST and SE (inclusive).
    /// </summary>
    public EdiFileTokens Tokenize(string fileContent)
    {
        if (string.IsNullOrEmpty(fileContent))
            throw new EdiParseException("EDI file content is empty.");

        // Strip BOM if present
        if (fileContent[0] == '\uFEFF')
            fileContent = fileContent[1..];

        // Find ISA start
        int isaStart = fileContent.IndexOf("ISA", StringComparison.Ordinal);
        if (isaStart < 0)
            throw new EdiParseException("Cannot find ISA segment in file.");

        // Need at least 106 chars from ISA start
        if (fileContent.Length < isaStart + 106)
            throw new EdiParseException("File too short to contain a valid ISA segment.");

        // Detect delimiters from ISA
        var delimiters = DelimiterContext.DetectFromIsa(fileContent.AsSpan(isaStart, 106));

        // Split all segments
        var allSegments = SplitSegments(fileContent, isaStart, delimiters);

        // Parse ISA/GS envelopes
        string[]? isaElements = null;
        string[]? gsElements = null;

        // Group segments into transactions
        var transactions = new List<TransactionSegmentGroup>();
        List<string>? currentTransaction = null;

        foreach (var rawSegment in allSegments)
        {
            var segId = GetSegmentId(rawSegment, delimiters.ElementSeparator);
            var trimmed = _options.TrimElementValues ? rawSegment.Trim() : rawSegment;

            switch (segId)
            {
                case "ISA":
                    isaElements = delimiters.SplitElements(trimmed);
                    break;
                case "GS":
                    gsElements = delimiters.SplitElements(trimmed);
                    break;
                case "ST":
                    currentTransaction = [trimmed];
                    break;
                case "SE":
                    if (currentTransaction != null)
                    {
                        currentTransaction.Add(trimmed);
                        transactions.Add(new TransactionSegmentGroup
                        {
                            Segments = currentTransaction,
                            IsaElements = isaElements,
                            GsElements = gsElements,
                        });
                        currentTransaction = null;
                    }
                    break;
                default:
                    currentTransaction?.Add(trimmed);
                    break;
            }
        }

        // Handle unclosed transaction (no SE found)
        if (currentTransaction is { Count: > 0 })
        {
            transactions.Add(new TransactionSegmentGroup
            {
                Segments = currentTransaction,
                IsaElements = isaElements,
                GsElements = gsElements,
            });
        }

        return new EdiFileTokens
        {
            Delimiters = delimiters,
            Transactions = transactions,
            IsaElements = isaElements,
            GsElements = gsElements,
        };
    }

    private static List<string> SplitSegments(string content, int startPos, DelimiterContext delimiters)
    {
        var segments = new List<string>();
        int start = startPos;

        for (int i = startPos; i < content.Length; i++)
        {
            if (content[i] == delimiters.SegmentTerminator)
            {
                var segment = content[start..i].Trim('\r', '\n', ' ');
                if (segment.Length > 0)
                    segments.Add(segment);
                start = i + 1;
            }
        }

        // Handle last segment without terminator
        if (start < content.Length)
        {
            var last = content[start..].Trim('\r', '\n', ' ');
            if (last.Length > 0)
                segments.Add(last);
        }

        return segments;
    }

    private static string GetSegmentId(string segment, char elementSeparator)
    {
        int sepIndex = segment.IndexOf(elementSeparator);
        return sepIndex > 0 ? segment[..sepIndex].Trim() : segment.Trim();
    }
}

/// <summary>
/// Result of tokenizing an EDI file.
/// </summary>
public sealed class EdiFileTokens
{
    public DelimiterContext Delimiters { get; init; } = null!;
    public List<TransactionSegmentGroup> Transactions { get; init; } = [];
    public string[]? IsaElements { get; init; }
    public string[]? GsElements { get; init; }
}

/// <summary>
/// A group of raw segments belonging to a single ST/SE transaction set.
/// </summary>
public sealed class TransactionSegmentGroup
{
    public List<string> Segments { get; init; } = [];
    public string[]? IsaElements { get; init; }
    public string[]? GsElements { get; init; }
}
