using System.Diagnostics;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.PriorAuth278.Loops;
using HealthcareEdi.Transactions.PriorAuth278.Models;
using HealthcareEdi.Transactions.PriorAuth278.Segments;

namespace HealthcareEdi.Transactions.PriorAuth278.Parsing;

public sealed class PriorAuth278Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public PriorAuth278Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    public EdiBatchResult<PriorAuth278RequestModel> ParseRequestFile(string content) => ParseFile<PriorAuth278RequestModel>(content);
    public EdiBatchResult<PriorAuth278ResponseModel> ParseResponseFile(string content) => ParseFile<PriorAuth278ResponseModel>(content);

    private EdiBatchResult<T> ParseFile<T>(string content) where T : PriorAuth278BaseModel, new()
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(content);
        var success = new List<T>();
        var failed = new List<FailedTransaction>();

        foreach (var txn in tokens.Transactions)
        {
            try { success.Add(ParseTxn<T>(txn, tokens.Delimiters)); }
            catch (Exception ex) { failed.Add(new FailedTransaction { RawSegments = txn.Segments, Exception = ex }); }
        }
        sw.Stop();
        return new EdiBatchResult<T>
        {
            Transactions = success,
            FailedTransactions = failed,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    private T ParseTxn<T>(TransactionSegmentGroup group, DelimiterContext delimiters) where T : PriorAuth278BaseModel, new()
    {
        var model = new T();
        model.TransactionSetHeader = StSegment.Parse(delimiters.SplitElements(group.Segments[0]));
        if (group.IsaElements != null) model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null) model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        ServiceReviewLoop? currentReview = null;
        string ctx = "HEADER";

        foreach (var raw in group.Segments)
        {
            string[] el;
            try { el = delimiters.SplitElements(raw); } catch { continue; }
            var sid = el[0];

            try
            {
                switch (sid)
                {
                    case "ST":
                    case "SE":
                    case "BHT":
                        if (sid == "SE" && int.TryParse(el.ElementAtOrDefault(1), out var cnt)) model.SegmentCount = cnt;
                        break;
                    case "HL":
                        var hl = HlSegment.Parse(el);
                        switch (hl.HierarchicalLevelCode)
                        {
                            case "20": model.Payer.HierarchicalLevel = hl; ctx = "2000A"; break;
                            case "21": model.Provider.HierarchicalLevel = hl; ctx = "2000B"; break;
                            case "22":
                                model.Subscriber = new ReviewSubscriberLoop { HierarchicalLevel = hl };
                                ctx = "2000C"; break;
                            case "23":
                                model.Patient = new ReviewPatientLoop { HierarchicalLevel = hl };
                                ctx = "2000E"; break;
                            default:
                                // Service level or other
                                currentReview = new ServiceReviewLoop();
                                model.ServiceReviews.Add(currentReview);
                                ctx = "2000F"; break;
                        }
                        break;
                    case "NM1":
                        var nm1 = Nm1Segment.Parse(el);
                        switch (ctx)
                        {
                            case "2000A": model.Payer.Name = nm1; break;
                            case "2000B": model.Provider.Name = nm1; break;
                            case "2000C": if (model.Subscriber != null) model.Subscriber.Name = nm1; break;
                            case "2000E": if (model.Patient != null) model.Patient.Name = nm1; break;
                        }
                        break;
                    case "DMG":
                        if (ctx == "2000E" && model.Patient != null) model.Patient.Demographics = DmgSegment.Parse(el);
                        else if (ctx == "2000C" && model.Subscriber != null) model.Subscriber.Demographics = DmgSegment.Parse(el);
                        break;
                    case "PRV":
                        if (ctx == "2000B") model.Provider.ProviderInfo = PrvSegment.Parse(el);
                        break;
                    case "UM":
                        if (currentReview == null) { currentReview = new ServiceReviewLoop(); model.ServiceReviews.Add(currentReview); ctx = "2000F"; }
                        currentReview.UtilizationManagement = UmSegment.Parse(el, delimiters);
                        break;
                    case "HCR":
                        if (currentReview != null) currentReview.ReviewDecision = HcrSegment.Parse(el);
                        break;
                    case "HI":
                        if (currentReview != null)
                            for (int i = 1; i < el.Length; i++)
                            {
                                var code = el[i];
                                if (!string.IsNullOrEmpty(code))
                                {
                                    var parts = delimiters.SplitComponents(code);
                                    if (parts.Length >= 2) currentReview.DiagnosisCodes.Add(parts[1]);
                                }
                            }
                        break;
                    case "REF":
                        var r = RefSegment.Parse(el);
                        if (currentReview != null) currentReview.References.Add(r);
                        else if (ctx == "2000B") model.Provider.References.Add(r);
                        else if (ctx == "2000A") model.Payer.References.Add(r);
                        else if (model.Subscriber != null) model.Subscriber.References.Add(r);
                        break;
                    case "DTP":
                        if (currentReview != null) currentReview.Dates.Add(DtpSegment.Parse(el));
                        break;
                    default:
                        if (_options.ValidationMode == ValidationMode.None) model.UnmappedSegments.Add(raw);
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_options.ValidationMode == ValidationMode.Strict) throw;
            }
        }
        return model;
    }
}
