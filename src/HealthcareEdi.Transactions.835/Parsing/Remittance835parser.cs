using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using HealthcareEdi.Transactions.Remittance835.Loops;
using HealthcareEdi.Transactions.Remittance835.Models;
using HealthcareEdi.Transactions.Remittance835.Segments;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.Remittance835.Parsing;

/// <summary>
/// Parser for HIPAA X12 5010 Transaction 835 (Health Care Claim Payment/Advice - ERA).
/// Implementation Guide: 005010X221A1.
/// </summary>
public sealed class Remittance835Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public Remittance835Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    /// <summary>
    /// Parses an 835 EDI file and returns a batch result with all remittance transactions.
    /// </summary>
    public EdiBatchResult<Remittance835Model> ParseFile(string fileContent)
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(fileContent);

        var successList = new List<Remittance835Model>();
        var failedList = new List<FailedTransaction>();

        foreach (var txnGroup in tokens.Transactions)
        {
            try
            {
                var model = ParseTransaction(txnGroup, tokens.Delimiters);
                successList.Add(model);
            }
            catch (Exception ex)
            {
                failedList.Add(new FailedTransaction
                {
                    RawSegments = txnGroup.Segments,
                    Exception = ex,
                    StControlNumber = GetStControlNumber(txnGroup, tokens.Delimiters),
                });
            }
        }

        sw.Stop();

        return new EdiBatchResult<Remittance835Model>
        {
            Transactions = successList,
            FailedTransactions = failedList,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Parses a single 835 transaction set (ST through SE).
    /// </summary>
    private Remittance835Model ParseTransaction(TransactionSegmentGroup group, DelimiterContext delimiters)
    {
        var segments = group.Segments;
        var model = new Remittance835Model();

        // Parse ST header
        var stElements = delimiters.SplitElements(segments[0]);
        model.TransactionSetHeader = StSegment.Parse(stElements);

        // Set envelope data
        if (group.IsaElements != null)
            model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null)
            model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        // Parse all segments
        ParseSegmentsIntoModel(model, segments, delimiters);

        // Apply extensions
        foreach (var ext in _options.ExtendedParsers.OfType<IExtendedParser<Remittance835Model>>())
            ext.PostProcess(model, segments, delimiters);

        // Strict validation
        if (_options.ValidationMode == ValidationMode.Strict && model.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))
            throw new EdiValidationException(model.ValidationIssues);

        return model;
    }

    /// <summary>
    /// State machine for parsing 835 segments into the model.
    /// 
    /// 835 structure:
    ///   ST → BPR → TRN → DTM/REF
    ///   → N1*PR (1000A Payer) → N3/N4/REF/PER
    ///   → N1*PE (1000B Payee) → N3/N4/REF
    ///   → LX (2000 Header)
    ///     → CLP (2100 Claim) → CAS/NM1/REF/DTM/AMT/QTY
    ///       → SVC (2110 Service) → CAS/REF/DTM/AMT/LQ
    ///   → PLB (Provider Adjustment)
    ///   → SE
    /// </summary>
    private void ParseSegmentsIntoModel(Remittance835Model model, List<string> segments, DelimiterContext delimiters)
    {
        RemittanceClaimLoop? currentClaim = null;
        RemittanceServiceLineLoop? currentServiceLine = null;
        string loopContext = "HEADER";  // HEADER, 1000A, 1000B, 2000, 2100, 2110

        foreach (var rawSegment in segments)
        {
            string[] elements;
            try
            {
                elements = delimiters.SplitElements(rawSegment);
            }
            catch
            {
                AddWarning(model, rawSegment, "Could not split segment elements");
                continue;
            }

            var segId = elements[0];

            try
            {
                switch (segId)
                {
                    case "ST":
                    case "SE":
                        if (segId == "SE" && int.TryParse(elements.ElementAtOrDefault(1), out var count))
                            model.SegmentCount = count;
                        break;

                    case "BPR":
                        model.FinancialInformation = BprSegment.Parse(elements);
                        break;

                    case "TRN":
                        model.TraceNumber = TrnSegment.Parse(elements);
                        break;

                    case "DTM":
                    case "DTP":
                        var dtp = DtpSegment.Parse(elements);
                        if (currentServiceLine != null && loopContext == "2110")
                            currentServiceLine.Dates.Add(dtp);
                        else if (currentClaim != null && loopContext == "2100")
                            currentClaim.Dates.Add(dtp);
                        else
                            model.TransactionDates.Add(dtp);
                        break;

                    case "N1":
                        var entityCode = elements.ElementAtOrDefault(1) ?? "";
                        switch (entityCode)
                        {
                            case "PR": // Payer
                                model.Payer.Name = ParseN1AsNm1(elements);
                                loopContext = "1000A";
                                currentClaim = null;
                                currentServiceLine = null;
                                break;
                            case "PE": // Payee
                                model.Payee.Name = ParseN1AsNm1(elements);
                                loopContext = "1000B";
                                currentClaim = null;
                                currentServiceLine = null;
                                break;
                        }
                        break;

                    case "N3":
                        var n3 = N3Segment.Parse(elements);
                        if (loopContext == "1000A") model.Payer.Address = n3;
                        else if (loopContext == "1000B") model.Payee.Address = n3;
                        break;

                    case "N4":
                        var n4 = N4Segment.Parse(elements);
                        if (loopContext == "1000A") model.Payer.CityStateZip = n4;
                        else if (loopContext == "1000B") model.Payee.CityStateZip = n4;
                        break;

                    case "REF":
                        var refSeg = RefSegment.Parse(elements);
                        if (currentServiceLine != null && loopContext == "2110")
                            currentServiceLine.References.Add(refSeg);
                        else if (currentClaim != null && loopContext == "2100")
                            currentClaim.References.Add(refSeg);
                        else if (loopContext == "1000A")
                            model.Payer.References.Add(refSeg);
                        else if (loopContext == "1000B")
                            model.Payee.References.Add(refSeg);
                        else
                            model.TransactionReferences.Add(refSeg);
                        break;

                    case "PER":
                        if (loopContext == "1000A")
                            model.Payer.Contact = PerSegment.Parse(elements);
                        break;

                    case "LX":
                        loopContext = "2000";
                        currentClaim = null;
                        currentServiceLine = null;
                        break;

                    case "CLP":
                        currentClaim = new RemittanceClaimLoop
                        {
                            ClaimPayment = ClpSegment.Parse(elements),
                        };
                        model.Claims.Add(currentClaim);
                        loopContext = "2100";
                        currentServiceLine = null;
                        break;

                    case "CAS":
                        var cas = CasSegment.Parse(elements);
                        if (currentServiceLine != null && loopContext == "2110")
                            currentServiceLine.Adjustments.Add(cas);
                        else if (currentClaim != null)
                            currentClaim.Adjustments.Add(cas);
                        break;

                    case "NM1":
                        var nm1 = Nm1Segment.Parse(elements);
                        if (currentClaim != null && loopContext is "2100" or "2110")
                            currentClaim.Names.Add(nm1);
                        break;

                    case "AMT":
                        // AMT segments carry supplemental amounts - store as references for now
                        if (currentClaim != null)
                        {
                            currentClaim.References.Add(new RefSegment
                            {
                                RawElements = elements,
                                ReferenceIdQualifier = $"AMT-{elements.ElementAtOrDefault(1) ?? ""}",
                                ReferenceId = elements.ElementAtOrDefault(2) ?? "",
                            });
                        }
                        break;

                    case "QTY":
                        // QTY segments carry quantity info - store as references
                        if (currentClaim != null)
                        {
                            currentClaim.References.Add(new RefSegment
                            {
                                RawElements = elements,
                                ReferenceIdQualifier = $"QTY-{elements.ElementAtOrDefault(1) ?? ""}",
                                ReferenceId = elements.ElementAtOrDefault(2) ?? "",
                            });
                        }
                        break;

                    case "SVC":
                        if (currentClaim != null)
                        {
                            currentServiceLine = new RemittanceServiceLineLoop
                            {
                                ServicePayment = SvcSegment.Parse(elements, delimiters),
                            };
                            currentClaim.ServiceLines.Add(currentServiceLine);
                            loopContext = "2110";
                        }
                        break;

                    case "LQ":
                        if (currentServiceLine != null)
                            currentServiceLine.RemarkCodes.Add(LqSegment.Parse(elements));
                        break;

                    case "PLB":
                        model.ProviderAdjustments.Add(PlbSegment.Parse(elements, delimiters));
                        loopContext = "SUMMARY";
                        currentClaim = null;
                        currentServiceLine = null;
                        break;

                    default:
                        if (_options.ValidationMode == ValidationMode.None)
                            model.UnmappedSegments.Add(rawSegment);
                        else if (_options.ValidationMode == ValidationMode.Lenient)
                            AddWarning(model, rawSegment, $"Unmapped segment: {segId}");
                        break;
                }
            }
            catch (Exception ex)
            {
                if (_options.ValidationMode == ValidationMode.Strict)
                    throw;

                AddWarning(model, rawSegment, $"Error parsing {segId}: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// 835 uses N1 instead of NM1 for payer/payee loops.
    /// This maps N1 elements into an Nm1Segment for model consistency.
    /// N1 format: N1*EntityCode*Name*IdQualifier*Id
    /// </summary>
    private static Nm1Segment ParseN1AsNm1(string[] elements)
    {
        return new Nm1Segment
        {
            RawElements = elements,
            EntityIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
            EntityTypeQualifier = "2", // N1 is always organization-level
            LastName = elements.ElementAtOrDefault(2) ?? "",  // Org name
            IdCodeQualifier = elements.ElementAtOrDefault(3) ?? "",
            IdCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }

    private void AddWarning(Remittance835Model model, string rawSegment, string description)
    {
        if (model.ValidationIssues.Count < _options.MaxValidationIssues)
        {
            model.ValidationIssues.Add(new EdiValidationIssue
            {
                Severity = ValidationSeverity.Warning,
                SegmentId = rawSegment.Length >= 3 ? rawSegment[..3] : rawSegment,
                Description = description,
                RawSegment = rawSegment,
            });
        }
    }

    private static string? GetStControlNumber(TransactionSegmentGroup group, DelimiterContext delimiters)
    {
        var stSeg = group.Segments.FirstOrDefault(s => s.StartsWith("ST"));
        if (stSeg == null) return null;
        var elements = delimiters.SplitElements(stSeg);
        return elements.ElementAtOrDefault(2);
    }
}
