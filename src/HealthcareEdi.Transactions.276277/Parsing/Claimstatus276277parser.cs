using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using HealthcareEdi.Transactions.ClaimStatus276277.Loops;
using HealthcareEdi.Transactions.ClaimStatus276277.Models;
using HealthcareEdi.Transactions.ClaimStatus276277.Segments;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.ClaimStatus276277.Parsing;

public sealed class ClaimStatus276277Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public ClaimStatus276277Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    public EdiBatchResult<ClaimStatus276Model> Parse276File(string fileContent) => ParseFile<ClaimStatus276Model>(fileContent);
    public EdiBatchResult<ClaimStatus277Model> Parse277File(string fileContent) => ParseFile<ClaimStatus277Model>(fileContent);

    private EdiBatchResult<T> ParseFile<T>(string fileContent) where T : ClaimStatusBaseModel, new()
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(fileContent);
        var successList = new List<T>();
        var failedList = new List<FailedTransaction>();

        foreach (var txnGroup in tokens.Transactions)
        {
            try
            {
                var model = ParseTransaction<T>(txnGroup, tokens.Delimiters);
                successList.Add(model);
            }
            catch (Exception ex)
            {
                failedList.Add(new FailedTransaction
                {
                    RawSegments = txnGroup.Segments,
                    Exception = ex,
                    StControlNumber = txnGroup.Segments.FirstOrDefault()?.Split(tokens.Delimiters.ElementSeparator).ElementAtOrDefault(2),
                });
            }
        }
        sw.Stop();

        return new EdiBatchResult<T>
        {
            Transactions = successList,
            FailedTransactions = failedList,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    private T ParseTransaction<T>(TransactionSegmentGroup group, DelimiterContext delimiters) where T : ClaimStatusBaseModel, new()
    {
        var model = new T();
        var stElements = delimiters.SplitElements(group.Segments[0]);
        model.TransactionSetHeader = StSegment.Parse(stElements);
        if (group.IsaElements != null) model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null) model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        ClaimStatusSubscriberLoop? currentSub = null;
        ClaimStatusPatientLoop? currentPat = null;
        ClaimStatusDetailLoop? currentDetail = null;
        string loopContext = "HEADER";

        foreach (var rawSegment in group.Segments)
        {
            string[] elements;
            try { elements = delimiters.SplitElements(rawSegment); }
            catch { continue; }

            var segId = elements[0];
            try
            {
                switch (segId)
                {
                    case "ST":
                    case "SE":
                    case "BHT":
                        if (segId == "SE" && int.TryParse(elements.ElementAtOrDefault(1), out var cnt))
                            model.SegmentCount = cnt;
                        break;

                    case "HL":
                        var hl = HlSegment.Parse(elements);
                        currentDetail = null;
                        switch (hl.HierarchicalLevelCode)
                        {
                            case "20":
                                model.Payer.HierarchicalLevel = hl;
                                loopContext = "2000A";
                                currentSub = null; currentPat = null;
                                break;
                            case "21":
                                model.Provider.HierarchicalLevel = hl;
                                loopContext = "2000B";
                                currentSub = null; currentPat = null;
                                break;
                            case "22":
                                currentSub = new ClaimStatusSubscriberLoop { HierarchicalLevel = hl };
                                model.Subscribers.Add(currentSub);
                                currentPat = null;
                                loopContext = "2000C";
                                break;
                            case "23":
                                currentPat = new ClaimStatusPatientLoop { HierarchicalLevel = hl };
                                model.Patients.Add(currentPat);
                                loopContext = "2000D";
                                break;
                        }
                        break;

                    case "NM1":
                        var nm1 = Nm1Segment.Parse(elements);
                        switch (loopContext)
                        {
                            case "2000A": model.Payer.Name = nm1; break;
                            case "2000B": model.Provider.Name = nm1; break;
                            case "2000C": if (currentSub != null) currentSub.Name = nm1; break;
                            case "2000D": if (currentPat != null) currentPat.Name = nm1; break;
                        }
                        break;

                    case "DMG":
                        var dmg = DmgSegment.Parse(elements);
                        if (loopContext == "2000D" && currentPat != null) currentPat.Demographics = dmg;
                        else if (loopContext == "2000C" && currentSub != null) currentSub.Demographics = dmg;
                        break;

                    case "TRN":
                        currentDetail = new ClaimStatusDetailLoop
                        {
                            TraceType = elements.ElementAtOrDefault(1) ?? "",
                            TraceNumber = elements.ElementAtOrDefault(2) ?? "",
                            OriginatingCompanyId = elements.ElementAtOrDefault(3) ?? "",
                        };
                        if (currentPat != null) currentPat.ClaimStatuses.Add(currentDetail);
                        else if (currentSub != null) currentSub.ClaimStatuses.Add(currentDetail);
                        loopContext = loopContext.StartsWith("2000") ? loopContext.Replace("2000", "2200") : loopContext;
                        break;

                    case "STC":
                        if (currentDetail != null)
                            currentDetail.StatusCodes.Add(StcSegment.Parse(elements, delimiters));
                        break;

                    case "REF":
                        var refSeg = RefSegment.Parse(elements);
                        if (currentDetail != null) currentDetail.References.Add(refSeg);
                        else if (loopContext == "2000B") model.Provider.References.Add(refSeg);
                        else if (loopContext == "2000A") model.Payer.References.Add(refSeg);
                        else if (currentSub != null) currentSub.References.Add(refSeg);
                        break;

                    case "DTP":
                        var dtp = DtpSegment.Parse(elements);
                        if (currentDetail != null) currentDetail.Dates.Add(dtp);
                        break;

                    case "AMT":
                        if (currentDetail != null && decimal.TryParse(elements.ElementAtOrDefault(2), out var amt))
                            currentDetail.ChargeAmount = amt;
                        break;

                    default:
                        if (_options.ValidationMode == ValidationMode.None) model.UnmappedSegments.Add(rawSegment);
                        else if (_options.ValidationMode == ValidationMode.Lenient)
                            model.ValidationIssues.Add(new EdiValidationIssue { Severity = ValidationSeverity.Warning, SegmentId = segId, Description = $"Unmapped: {segId}", RawSegment = rawSegment });
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_options.ValidationMode == ValidationMode.Strict) throw;
                model.ValidationIssues.Add(new EdiValidationIssue { Severity = ValidationSeverity.Warning, SegmentId = segId, Description = ex.Message, RawSegment = rawSegment });
            }
        }

        if (_options.ValidationMode == ValidationMode.Strict && model.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))
            throw new EdiValidationException(model.ValidationIssues);
        return model;
    }
}
