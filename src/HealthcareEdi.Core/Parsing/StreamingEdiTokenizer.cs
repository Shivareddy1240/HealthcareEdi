using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;

namespace HealthcareEdi.Core.Parsing;

/// <summary>
/// Streaming tokenizer that reads an EDI file from a Stream character-by-character,
/// yielding one transaction set at a time. Memory usage is proportional to the
/// largest single transaction, not the file size.
/// 
/// Usage:
///   await foreach (var txn in tokenizer.TokenizeAsync(stream))
///   {
///       // Process one transaction at a time
///       // Previous transaction's memory is eligible for GC
///   }
/// </summary>
public sealed class StreamingEdiTokenizer
{
    private readonly ParserOptions _options;

    public StreamingEdiTokenizer(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
    }

    /// <summary>
    /// Reads the ISA header from a stream to detect delimiters without loading the entire file.
    /// Returns the delimiters and positions the stream just after the ISA segment terminator.
    /// </summary>
    private async Task<(DelimiterContext Delimiters, string[] IsaElements)> ReadIsaAsync(StreamReader reader)
    {
        // Read exactly 106 characters for the ISA
        var isaBuffer = new char[106];
        int totalRead = 0;

        // Skip any leading whitespace/BOM to find ISA
        while (!reader.EndOfStream)
        {
            var ch = (char)reader.Read();
            if (ch == 'I')
            {
                isaBuffer[0] = 'I';
                var next2 = new char[2];
                var read2 = await reader.ReadBlockAsync(next2, 0, 2);
                if (read2 == 2 && next2[0] == 'S' && next2[1] == 'A')
                {
                    isaBuffer[1] = 'S';
                    isaBuffer[2] = 'A';
                    totalRead = 3;
                    break;
                }
            }
        }

        if (totalRead < 3)
            throw new EdiParseException("Cannot find ISA segment in stream.");

        // Read remaining ISA characters (positions 3-105)
        int remaining = 106 - totalRead;
        int read = await reader.ReadBlockAsync(isaBuffer, totalRead, remaining);
        if (read < remaining)
            throw new EdiParseException("Stream too short to contain a valid ISA segment.");

        var isaSpan = new ReadOnlySpan<char>(isaBuffer);
        var delimiters = DelimiterContext.DetectFromIsa(isaSpan);

        // Build ISA elements
        var isaString = new string(isaBuffer);
        var isaElements = delimiters.SplitElements(isaString);

        // Read past the segment terminator
        if (!reader.EndOfStream)
        {
            var next = (char)reader.Read();
            // Skip \r\n after terminator if present
            if (next == '\r' && !reader.EndOfStream) reader.Read(); // skip \n
            else if (next != delimiters.SegmentTerminator && next != '\n') { /* already past */ }
        }

        return (delimiters, isaElements);
    }

    /// <summary>
    /// Streams an EDI file, yielding one TransactionSegmentGroup at a time.
    /// Only one transaction's worth of segments is in memory at any point.
    /// </summary>
    public async IAsyncEnumerable<StreamingTransactionGroup> TokenizeAsync(Stream stream)
    {
        using var reader = new StreamReader(stream, bufferSize: _options.StreamBufferSize, leaveOpen: true);

        // Stage 1: Read ISA to detect delimiters
        var (delimiters, isaElements) = await ReadIsaAsync(reader);
        string[]? gsElements = null;

        // Stage 2: Read segment by segment
        var segmentBuffer = new System.Text.StringBuilder(256);
        List<string>? currentTransaction = null;
        long segmentIndex = 0;

        while (!reader.EndOfStream)
        {
            var ch = (char)reader.Read();

            // Skip line breaks
            if (ch == '\r' || ch == '\n')
                continue;

            if (ch == delimiters.SegmentTerminator)
            {
                var rawSegment = segmentBuffer.ToString().Trim();
                segmentBuffer.Clear();

                if (rawSegment.Length == 0)
                    continue;

                segmentIndex++;
                var segId = GetSegmentId(rawSegment, delimiters.ElementSeparator);

                switch (segId)
                {
                    case "ISA":
                        // New interchange - re-parse ISA elements
                        isaElements = delimiters.SplitElements(rawSegment);
                        break;

                    case "GS":
                        gsElements = delimiters.SplitElements(rawSegment);
                        break;

                    case "ST":
                        currentTransaction = new List<string> { rawSegment };
                        break;

                    case "SE":
                        if (currentTransaction != null)
                        {
                            currentTransaction.Add(rawSegment);
                            yield return new StreamingTransactionGroup
                            {
                                Segments = currentTransaction,
                                IsaElements = isaElements,
                                GsElements = gsElements,
                                Delimiters = delimiters,
                                SegmentIndex = segmentIndex,
                            };
                            currentTransaction = null;
                        }
                        break;

                    case "GE":
                    case "IEA":
                        // Envelope trailers - no action needed for streaming
                        break;

                    default:
                        currentTransaction?.Add(rawSegment);
                        break;
                }
            }
            else
            {
                segmentBuffer.Append(ch);
            }
        }

        // Handle unclosed transaction (no SE found)
        if (currentTransaction is { Count: > 0 })
        {
            yield return new StreamingTransactionGroup
            {
                Segments = currentTransaction,
                IsaElements = isaElements,
                GsElements = gsElements,
                Delimiters = delimiters,
                SegmentIndex = segmentIndex,
            };
        }
    }

    /// <summary>
    /// Convenience: Streams from a file path.
    /// </summary>
    public IAsyncEnumerable<StreamingTransactionGroup> TokenizeFileAsync(string filePath)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: _options.StreamBufferSize, useAsync: true);
        return TokenizeAsync(stream);
    }

    private static string GetSegmentId(string segment, char elementSeparator)
    {
        int sepIndex = segment.IndexOf(elementSeparator);
        return sepIndex > 0 ? segment[..sepIndex].Trim() : segment.Trim();
    }
}

/// <summary>
/// A single transaction group yielded from the streaming tokenizer.
/// Contains everything needed to parse one transaction.
/// </summary>
public sealed class StreamingTransactionGroup
{
    public List<string> Segments { get; init; } = [];
    public string[]? IsaElements { get; init; }
    public string[]? GsElements { get; init; }
    public DelimiterContext Delimiters { get; init; } = null!;
    public long SegmentIndex { get; init; }
}

/// <summary>
/// Result of processing a batch of streamed transactions.
/// </summary>
public sealed class StreamingBatchResult<TModel>
{
    /// <summary>Successfully parsed transactions in this batch.</summary>
    public IReadOnlyList<TModel> Transactions { get; init; } = [];

    /// <summary>Failed transactions in this batch.</summary>
    public IReadOnlyList<FailedTransaction> FailedTransactions { get; init; } = [];

    /// <summary>Total transactions processed so far (cumulative across all batches).</summary>
    public long TotalProcessed { get; init; }

    /// <summary>True if there are more batches to process.</summary>
    public bool HasMore { get; init; }

    /// <summary>Batch number (1-based).</summary>
    public int BatchNumber { get; init; }
}

/// <summary>
/// Processes streamed EDI transactions in configurable batches.
/// The consumer processes each batch and signals readiness for the next one.
/// 
/// Usage:
///   var batcher = new EdiBatchProcessor&lt;Remittance835Model&gt;(batchSize: 500);
///   await foreach (var batch in batcher.ProcessAsync(stream, parseFunc))
///   {
///       // batch.Transactions has up to 500 parsed models
///       await SaveToDatabase(batch.Transactions);
///       // Memory for this batch is released when next batch starts
///   }
/// </summary>
public sealed class EdiBatchProcessor<TModel> where TModel : Models.Base.EdiTransactionBase
{
    private readonly int _batchSize;
    private readonly ParserOptions _options;

    /// <summary>
    /// Creates a batch processor.
    /// </summary>
    /// <param name="batchSize">Number of transactions per batch. Default 500.</param>
    /// <param name="options">Parser options.</param>
    public EdiBatchProcessor(int batchSize = 500, ParserOptions? options = null)
    {
        _batchSize = batchSize;
        _options = options ?? new ParserOptions();
    }

    /// <summary>
    /// Streams and parses an EDI file in batches. Each batch contains up to batchSize
    /// parsed transactions. Memory from the previous batch is eligible for GC when
    /// the next batch starts.
    /// </summary>
    /// <param name="stream">Input stream containing EDI data.</param>
    /// <param name="parseTransaction">Function that parses a single transaction group into a model.
    /// Receives (segments, delimiters) and returns the parsed model.</param>
    public async IAsyncEnumerable<StreamingBatchResult<TModel>> ProcessAsync(
        Stream stream,
        Func<TransactionSegmentGroup, DelimiterContext, TModel> parseTransaction)
    {
        var tokenizer = new StreamingEdiTokenizer(_options);
        var currentBatch = new List<TModel>(_batchSize);
        var currentFailed = new List<FailedTransaction>();
        long totalProcessed = 0;
        int batchNumber = 0;

        await foreach (var txnGroup in tokenizer.TokenizeAsync(stream))
        {
            totalProcessed++;

            try
            {
                var group = new TransactionSegmentGroup
                {
                    Segments = txnGroup.Segments,
                    IsaElements = txnGroup.IsaElements,
                    GsElements = txnGroup.GsElements,
                };
                var model = parseTransaction(group, txnGroup.Delimiters);
                currentBatch.Add(model);
            }
            catch (Exception ex)
            {
                currentFailed.Add(new FailedTransaction
                {
                    RawSegments = txnGroup.Segments,
                    Exception = ex,
                    ByteOffset = txnGroup.SegmentIndex,
                });
            }

            // Yield batch when full
            if (currentBatch.Count >= _batchSize)
            {
                batchNumber++;
                yield return new StreamingBatchResult<TModel>
                {
                    Transactions = currentBatch,
                    FailedTransactions = currentFailed,
                    TotalProcessed = totalProcessed,
                    HasMore = true,
                    BatchNumber = batchNumber,
                };

                // Clear for next batch - previous batch memory eligible for GC
                currentBatch = new List<TModel>(_batchSize);
                currentFailed = new List<FailedTransaction>();
            }
        }

        // Yield final partial batch
        if (currentBatch.Count > 0 || currentFailed.Count > 0)
        {
            batchNumber++;
            yield return new StreamingBatchResult<TModel>
            {
                Transactions = currentBatch,
                FailedTransactions = currentFailed,
                TotalProcessed = totalProcessed,
                HasMore = false,
                BatchNumber = batchNumber,
            };
        }
    }

    /// <summary>
    /// Convenience: Process from a file path.
    /// </summary>
    public IAsyncEnumerable<StreamingBatchResult<TModel>> ProcessFileAsync(
        string filePath,
        Func<TransactionSegmentGroup, DelimiterContext, TModel> parseTransaction)
    {
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read,
            bufferSize: _options.StreamBufferSize, useAsync: true);
        return ProcessAsync(stream, parseTransaction);
    }
}
