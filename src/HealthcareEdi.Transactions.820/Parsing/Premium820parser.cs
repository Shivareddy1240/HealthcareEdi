using System.Diagnostics;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Premium820.Loops;
using HealthcareEdi.Transactions.Premium820.Models;
using HealthcareEdi.Transactions.Premium820.Segments;

namespace HealthcareEdi.Transactions.Premium820.Parsing;

public sealed class Premium820Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public Premium820Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    public EdiBatchResult<PremiumPayment820Model> ParseFile(string content)
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(content);
        var success = new List<PremiumPayment820Model>();
        var failed = new List<FailedTransaction>();

        foreach (var txn in tokens.Transactions)
        {
            try { success.Add(ParseTxn(txn, tokens.Delimiters)); }
            catch (Exception ex) { failed.Add(new FailedTransaction { RawSegments = txn.Segments, Exception = ex }); }
        }
        sw.Stop();
        return new EdiBatchResult<PremiumPayment820Model>
        {
            Transactions = success,
            FailedTransactions = failed,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    private PremiumPayment820Model ParseTxn(TransactionSegmentGroup group, DelimiterContext delimiters)
    {
        var model = new PremiumPayment820Model();
        model.TransactionSetHeader = StSegment.Parse(delimiters.SplitElements(group.Segments[0]));
        if (group.IsaElements != null) model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null) model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        OrganizationSummaryLoop? currentOrg = null;
        PremiumDetailLoop? currentDetail = null;
        string ctx = "HEADER";

        foreach (var raw in group.Segments)
        {
            string[] el;
            try { el = delimiters.SplitElements(raw); } catch { continue; }
            var sid = el[0];

            switch (sid)
            {
                case "ST":
                case "SE":
                    if (sid == "SE" && int.TryParse(el.ElementAtOrDefault(1), out var cnt)) model.SegmentCount = cnt;
                    break;
                case "BPR":
                    model.FinancialInformation = BprSegment820.Parse(el);
                    break;
                case "TRN":
                    model.TraceNumber = el.ElementAtOrDefault(2) ?? "";
                    break;
                case "N1":
                    var ec = el.ElementAtOrDefault(1) ?? "";
                    if (ec == "PR" || ec == "PE") { model.Remitter.Name = ParseN1(el); ctx = "1000A"; }
                    else if (ec == "IN" || ec == "41") { model.Receiver.Name = ParseN1(el); ctx = "1000B"; }
                    break;
                case "N3":
                    var n3 = N3Segment.Parse(el);
                    if (ctx == "1000A") model.Remitter.Address = n3;
                    else if (ctx == "1000B") model.Receiver.Address = n3;
                    break;
                case "N4":
                    var n4 = N4Segment.Parse(el);
                    if (ctx == "1000A") model.Remitter.CityStateZip = n4;
                    else if (ctx == "1000B") model.Receiver.CityStateZip = n4;
                    break;
                case "ENT":
                    currentOrg = new OrganizationSummaryLoop { Entity = EntSegment.Parse(el) };
                    model.Organizations.Add(currentOrg);
                    currentDetail = null;
                    ctx = "2000";
                    break;
                case "RMR":
                    currentDetail = new PremiumDetailLoop { Remittance = RmrSegment.Parse(el) };
                    if (currentOrg != null) currentOrg.PremiumDetails.Add(currentDetail);
                    ctx = "2300";
                    break;
                case "REF":
                    var r = RefSegment.Parse(el);
                    if (currentDetail != null && ctx == "2300") currentDetail.References.Add(r);
                    else if (currentOrg != null && ctx == "2000") currentOrg.References.Add(r);
                    else model.TransactionReferences.Add(r);
                    break;
                case "DTP":
                    var dtp = DtpSegment.Parse(el);
                    if (currentDetail != null) currentDetail.Dates.Add(dtp);
                    else model.TransactionDates.Add(dtp);
                    break;
                default:
                    if (_options.ValidationMode == ValidationMode.None) model.UnmappedSegments.Add(raw);
                    break;
            }
        }
        return model;
    }

    private static Nm1Segment ParseN1(string[] elements) => new()
    {
        RawElements = elements,
        EntityIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
        EntityTypeQualifier = "2",
        LastName = elements.ElementAtOrDefault(2) ?? "",
        IdCodeQualifier = elements.ElementAtOrDefault(3) ?? "",
        IdCode = elements.ElementAtOrDefault(4) ?? "",
    };
}
