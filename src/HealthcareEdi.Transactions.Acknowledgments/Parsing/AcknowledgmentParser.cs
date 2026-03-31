using System.Diagnostics;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Acknowledgments.Models;
using HealthcareEdi.Transactions.Acknowledgments.Segments;

namespace HealthcareEdi.Transactions.Acknowledgments.Parsing;

public sealed class AcknowledgmentParser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public AcknowledgmentParser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    /// <summary>Parse a 999 or 997 Implementation/Functional Acknowledgment file.</summary>
    public EdiBatchResult<Acknowledgment999Model> Parse999File(string content)
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(content);
        var success = new List<Acknowledgment999Model>();
        var failed = new List<FailedTransaction>();

        foreach (var txn in tokens.Transactions)
        {
            try { success.Add(Parse999Txn(txn, tokens.Delimiters)); }
            catch (Exception ex) { failed.Add(new FailedTransaction { RawSegments = txn.Segments, Exception = ex }); }
        }
        sw.Stop();
        return new EdiBatchResult<Acknowledgment999Model>
        {
            Transactions = success,
            FailedTransactions = failed,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    private Acknowledgment999Model Parse999Txn(TransactionSegmentGroup group, DelimiterContext delimiters)
    {
        var model = new Acknowledgment999Model();
        model.TransactionSetHeader = StSegment.Parse(delimiters.SplitElements(group.Segments[0]));
        if (group.IsaElements != null) model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null) model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        TransactionSetAcknowledgment? currentTxnAck = null;

        foreach (var raw in group.Segments)
        {
            string[] el;
            try { el = delimiters.SplitElements(raw); } catch { continue; }
            var sid = el[0];

            switch (sid)
            {
                case "ST": case "SE": break;
                case "AK1":
                    model.GroupResponse = Ak1Segment.Parse(el);
                    break;
                case "AK2":
                    currentTxnAck = new TransactionSetAcknowledgment { Header = Ak2Segment.Parse(el) };
                    model.TransactionAcknowledgments.Add(currentTxnAck);
                    break;
                case "IK5":
                case "AK5": // 997 uses AK5 instead of IK5
                    if (currentTxnAck != null) currentTxnAck.Trailer = Ik5Segment.Parse(el);
                    break;
                case "AK9":
                    model.GroupTrailer = Ak9Segment.Parse(el);
                    break;
                case "IK3":
                case "IK4":
                case "AK3":
                case "AK4":
                    // Error detail segments - store raw for now
                    if (currentTxnAck != null) currentTxnAck.ErrorSegments.Add(raw);
                    break;
                default:
                    if (_options.ValidationMode == ValidationMode.None) model.UnmappedSegments.Add(raw);
                    break;
            }
        }
        return model;
    }
}
