using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using HealthcareEdi.Transactions.Claim837.Loops;
using HealthcareEdi.Transactions.Claim837.Models;
using HealthcareEdi.Transactions.Claim837.Segments;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.Claim837.Parsing;

/// <summary>
/// Parser for HIPAA X12 5010 Transaction 837 (Health Care Claim).
/// Supports Professional (P), Institutional (I), and Dental (D) variants.
/// </summary>
public sealed class Claim837Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public Claim837Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    /// <summary>
    /// Parses an 837 EDI file and returns a batch result with all claims.
    /// Supports P, I, and D variants with auto-detection.
    /// </summary>
    public EdiBatchResult<Claim837BaseModel> ParseFile(string fileContent)
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(fileContent);

        var successList = new List<Claim837BaseModel>();
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

        return new EdiBatchResult<Claim837BaseModel>
        {
            Transactions = successList,
            FailedTransactions = failedList,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    /// <summary>
    /// Parses a single transaction set (ST through SE) into a Claim837 model.
    /// </summary>
    internal Claim837BaseModel ParseTransaction(TransactionSegmentGroup group, DelimiterContext delimiters)
    {
        var segments = group.Segments;

        // Parse ST header to detect variant
        var stElements = delimiters.SplitElements(segments[0]);
        var st = StSegment.Parse(stElements);

        // Detect variant: ST03 → GS08 → SV segment inference
        var variant = DetectVariant(st, group.GsElements, segments, delimiters);

        // Create the right model type
        Claim837BaseModel model = variant switch
        {
            Claim837Variant.Professional => new Claim837PModel(),
            Claim837Variant.Institutional => new Claim837IModel(),
            Claim837Variant.Dental => new Claim837DModel(),
            _ => throw new TransactionTypeUnresolvableException(null, null, st.TransactionSetControlNumber),
        };

        // Set envelope data
        model.TransactionSetHeader = st;
        if (group.IsaElements != null)
            model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null)
            model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        // Parse all segments into the model
        ParseSegmentsIntoModel(model, segments, delimiters);

        // Apply payer-specific extensions
        foreach (var ext in _options.ExtendedParsers.OfType<IExtendedParser<Claim837BaseModel>>())
            ext.PostProcess(model, segments, delimiters);

        // Strict validation
        if (_options.ValidationMode == ValidationMode.Strict && model.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))
            throw new EdiValidationException(model.ValidationIssues);

        return model;
    }

    /// <summary>
    /// Cascading variant detection: ST03 → GS08 → SV segment scan.
    /// </summary>
    private Claim837Variant DetectVariant(StSegment st, string[]? gsElements, List<string> segments, DelimiterContext delimiters)
    {
        // Level 1: ST03 (Implementation Convention Reference)
        var st03 = st.ImplementationConventionReference;
        if (!string.IsNullOrEmpty(st03))
        {
            if (st03.Contains("X222")) return Claim837Variant.Professional;
            if (st03.Contains("X223")) return Claim837Variant.Institutional;
            if (st03.Contains("X224")) return Claim837Variant.Dental;
        }

        // Level 2: GS08 (Version/Release Code)
        var gs08 = gsElements?.ElementAtOrDefault(8) ?? "";
        if (!string.IsNullOrEmpty(gs08))
        {
            if (gs08.Contains("X222")) return Claim837Variant.Professional;
            if (gs08.Contains("X223")) return Claim837Variant.Institutional;
            if (gs08.Contains("X224")) return Claim837Variant.Dental;
        }

        // Level 3: Scan for SV1/SV2/SV3 in segments
        foreach (var seg in segments)
        {
            var segId = seg[..Math.Min(3, seg.Length)];
            if (segId == "SV1") return Claim837Variant.Professional;
            if (segId == "SV2") return Claim837Variant.Institutional;
            if (segId == "SV3") return Claim837Variant.Dental;
        }

        // Level 3 logged warning
        if (_options.ValidationMode != ValidationMode.None)
        {
            // If we get here and still can't detect, throw
            throw new TransactionTypeUnresolvableException(
                gsElements?.ElementAtOrDefault(6),
                gsElements?.ElementAtOrDefault(6),
                st.TransactionSetControlNumber);
        }

        // Default to Professional in None mode
        return Claim837Variant.Professional;
    }

    /// <summary>
    /// Main segment-by-segment parsing state machine.
    /// </summary>
    private void ParseSegmentsIntoModel(Claim837BaseModel model, List<string> segments, DelimiterContext delimiters)
    {
        // State tracking
        ClaimLoop? currentClaim = null;
        ServiceLineLoop? currentServiceLine = null;
        OtherSubscriberLoop? currentOtherSub = null;
        string currentLoopContext = "";
        int segmentCount = 0;

        foreach (var rawSegment in segments)
        {
            segmentCount++;
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
                        // Already handled in envelope parsing
                        if (segId == "SE" && int.TryParse(elements.ElementAtOrDefault(1), out var count))
                            model.SegmentCount = count;
                        break;

                    case "HL":
                        var hl = HlSegment.Parse(elements);
                        switch (hl.HierarchicalLevelCode)
                        {
                            case "20": // Billing Provider
                                model.BillingProvider.HierarchicalLevel = hl;
                                currentLoopContext = "2000A";
                                break;
                            case "22": // Subscriber
                                model.Subscriber.HierarchicalLevel = hl;
                                currentLoopContext = "2000B";
                                break;
                            case "23": // Patient (dependent)
                                model.Patient = new PatientLoop { HierarchicalLevel = hl };
                                currentLoopContext = "2000C";
                                break;
                        }
                        currentClaim = null;
                        currentServiceLine = null;
                        break;

                    case "NM1":
                        var nm1 = Nm1Segment.Parse(elements);
                        MapNm1(model, nm1, ref currentLoopContext, currentClaim, currentServiceLine, currentOtherSub);
                        break;

                    case "N3":
                        var n3 = N3Segment.Parse(elements);
                        MapN3(model, n3, currentLoopContext, currentClaim, currentServiceLine);
                        break;

                    case "N4":
                        var n4 = N4Segment.Parse(elements);
                        MapN4(model, n4, currentLoopContext, currentClaim, currentServiceLine);
                        break;

                    case "REF":
                        var refSeg = RefSegment.Parse(elements);
                        MapRef(model, refSeg, currentLoopContext, currentClaim, currentServiceLine, currentOtherSub);
                        break;

                    case "PER":
                        var per = PerSegment.Parse(elements);
                        if (currentLoopContext == "1000A")
                            model.Submitter.Contact = per;
                        break;

                    case "SBR":
                        var sbr = SbrSegment.Parse(elements);
                        if (currentLoopContext is "2320" or "COB")
                        {
                            currentOtherSub = new OtherSubscriberLoop { SubscriberInfo = sbr };
                            currentClaim?.OtherSubscribers.Add(currentOtherSub);
                            currentLoopContext = "2320";
                        }
                        else
                        {
                            model.Subscriber.SubscriberInfo = sbr;
                        }
                        break;

                    case "DMG":
                        var dmg = DmgSegment.Parse(elements);
                        if (currentLoopContext == "2000C" && model.Patient != null)
                            model.Patient.Demographics = dmg;
                        else
                            model.Subscriber.Demographics = dmg;
                        break;

                    case "PRV":
                        var prv = PrvSegment.Parse(elements);
                        if (currentLoopContext == "2000A")
                            model.BillingProvider.ProviderInfo = prv;
                        break;

                    case "CLM":
                        currentClaim = new ClaimLoop
                        {
                            ClaimInfo = ClmSegment.Parse(elements, delimiters)
                        };
                        model.Claims.Add(currentClaim);
                        currentLoopContext = "2300";
                        currentServiceLine = null;
                        currentOtherSub = null;
                        break;

                    case "HI":
                        if (currentClaim != null)
                            currentClaim.DiagnosisCodes.Add(HiSegment.Parse(elements, delimiters));
                        break;

                    case "DTP":
                        var dtp = DtpSegment.Parse(elements);
                        if (currentServiceLine != null)
                            currentServiceLine.Dates.Add(dtp);
                        else if (currentOtherSub != null)
                            currentOtherSub.Dates.Add(dtp);
                        else if (currentClaim != null)
                            currentClaim.Dates.Add(dtp);
                        break;

                    case "CL1":
                        if (currentClaim != null && model.Variant == Claim837Variant.Institutional)
                            currentClaim.InstitutionalClaimCode = Cl1Segment.Parse(elements);
                        break;

                    case "LX":
                        if (currentClaim != null)
                        {
                            currentServiceLine = new ServiceLineLoop();
                            if (int.TryParse(elements.ElementAtOrDefault(1), out var lineNum))
                                currentServiceLine.LineNumber = lineNum;
                            currentClaim.ServiceLines.Add(currentServiceLine);
                            currentLoopContext = "2400";
                        }
                        break;

                    case "SV1":
                        if (currentServiceLine != null)
                            currentServiceLine.ProfessionalService = Sv1Segment.Parse(elements, delimiters);
                        break;

                    case "SV2":
                        if (currentServiceLine != null)
                            currentServiceLine.InstitutionalService = Sv2Segment.Parse(elements, delimiters);
                        break;

                    case "SV3":
                        if (currentServiceLine != null)
                            currentServiceLine.DentalService = Sv3Segment.Parse(elements, delimiters);
                        break;

                    default:
                        // Unmapped segment
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

    /// <summary>Maps NM1 segments to the correct model location based on entity code and loop context.</summary>
    private static void MapNm1(Claim837BaseModel model, Nm1Segment nm1, ref string loopContext,
        ClaimLoop? claim, ServiceLineLoop? serviceLine, OtherSubscriberLoop? otherSub)
    {
        switch (nm1.EntityIdentifierCode)
        {
            case "41": // Submitter
                model.Submitter.Name = nm1;
                loopContext = "1000A";
                break;
            case "40": // Receiver
                model.Receiver.Name = nm1;
                loopContext = "1000B";
                break;
            case "85": // Billing Provider
                model.BillingProvider.Name = nm1;
                loopContext = "2010AA";
                break;
            case "IL": // Insured/Subscriber
                if (loopContext.StartsWith("2320") || loopContext == "COB")
                    break; // Other subscriber NM1 handled differently
                model.Subscriber.SubscriberName = nm1;
                loopContext = "2010BA";
                break;
            case "PR": // Payer
                if (otherSub != null && loopContext == "2320")
                {
                    otherSub.OtherPayers.Add(new OtherPayerLoop { Name = nm1 });
                    loopContext = "2330";
                }
                else
                {
                    model.Subscriber.PayerName = nm1;
                    loopContext = "2010BB";
                }
                break;
            case "QC": // Patient
                if (model.Patient != null)
                    model.Patient.PatientName = nm1;
                break;
            case "DN": // Referring Provider
            case "82": // Rendering Provider
            case "77": // Service Facility
            case "DQ": // Supervising Provider
                if (serviceLine != null)
                {
                    serviceLine.RenderingProviders.Add(new ProviderLoop { Name = nm1 });
                }
                else if (claim != null)
                {
                    claim.Providers.Add(new ProviderLoop { Name = nm1 });
                }
                break;
        }
    }

    private static void MapN3(Claim837BaseModel model, N3Segment n3, string loopContext,
        ClaimLoop? claim, ServiceLineLoop? serviceLine)
    {
        switch (loopContext)
        {
            case "2010AA":
            case "2000A":
                model.BillingProvider.Address = n3;
                break;
            case "2010BA":
                model.Subscriber.SubscriberAddress = n3;
                break;
            case "2000C":
                if (model.Patient != null) model.Patient.PatientAddress = n3;
                break;
        }
    }

    private static void MapN4(Claim837BaseModel model, N4Segment n4, string loopContext,
        ClaimLoop? claim, ServiceLineLoop? serviceLine)
    {
        switch (loopContext)
        {
            case "2010AA":
            case "2000A":
                model.BillingProvider.CityStateZip = n4;
                break;
            case "2010BA":
                model.Subscriber.SubscriberCityStateZip = n4;
                break;
            case "2000C":
                if (model.Patient != null) model.Patient.PatientCityStateZip = n4;
                break;
        }
    }

    private static void MapRef(Claim837BaseModel model, RefSegment refSeg, string loopContext,
        ClaimLoop? claim, ServiceLineLoop? serviceLine, OtherSubscriberLoop? otherSub)
    {
        if (serviceLine != null && loopContext == "2400")
            serviceLine.References.Add(refSeg);
        else if (otherSub != null && loopContext == "2320")
            otherSub.References.Add(refSeg);
        else if (claim != null && loopContext == "2300")
            claim.References.Add(refSeg);
        else if (loopContext == "2010BB")
            model.Subscriber.PayerReferences.Add(refSeg);
        else if (loopContext is "2010BA" or "2000B")
            model.Subscriber.SubscriberReferences.Add(refSeg);
        else if (loopContext is "2010AA" or "2000A")
            model.BillingProvider.References.Add(refSeg);
    }

    private void AddWarning(Claim837BaseModel model, string rawSegment, string description)
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

    // ── Streaming Methods (for GB-scale files) ──────────────────

    /// <summary>Streams an 837 file transaction-by-transaction.</summary>
    public async IAsyncEnumerable<Claim837BaseModel> ParseFileStreamingAsync(string filePath)
    {
        var tokenizer = new StreamingEdiTokenizer(_options);
        await foreach (var txnGroup in tokenizer.TokenizeFileAsync(filePath))
        {
            Claim837BaseModel? model = null;
            try
            {
                var group = new TransactionSegmentGroup { Segments = txnGroup.Segments, IsaElements = txnGroup.IsaElements, GsElements = txnGroup.GsElements };
                model = ParseTransaction(group, txnGroup.Delimiters);
            }
            catch { continue; }
            if (model != null) yield return model;
        }
    }

    /// <summary>Streams an 837 file from a Stream.</summary>
    public async IAsyncEnumerable<Claim837BaseModel> ParseStreamingAsync(Stream stream)
    {
        var tokenizer = new StreamingEdiTokenizer(_options);
        await foreach (var txnGroup in tokenizer.TokenizeAsync(stream))
        {
            Claim837BaseModel? model = null;
            try
            {
                var group = new TransactionSegmentGroup { Segments = txnGroup.Segments, IsaElements = txnGroup.IsaElements, GsElements = txnGroup.GsElements };
                model = ParseTransaction(group, txnGroup.Delimiters);
            }
            catch { continue; }
            if (model != null) yield return model;
        }
    }

    /// <summary>Processes an 837 file in configurable batches.</summary>
    public IAsyncEnumerable<StreamingBatchResult<Claim837BaseModel>> ParseFileInBatchesAsync(string filePath, int batchSize = 500)
    {
        var processor = new EdiBatchProcessor<Claim837BaseModel>(batchSize, _options);
        return processor.ProcessFileAsync(filePath, ParseTransaction);
    }

    /// <summary>Processes an 837 stream in configurable batches.</summary>
    public IAsyncEnumerable<StreamingBatchResult<Claim837BaseModel>> ParseStreamInBatchesAsync(Stream stream, int batchSize = 500)
    {
        var processor = new EdiBatchProcessor<Claim837BaseModel>(batchSize, _options);
        return processor.ProcessAsync(stream, ParseTransaction);
    }
}
