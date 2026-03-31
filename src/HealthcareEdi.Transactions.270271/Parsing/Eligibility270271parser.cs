using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using HealthcareEdi.Transactions.Eligibility270271.Loops;
using HealthcareEdi.Transactions.Eligibility270271.Models;
using HealthcareEdi.Transactions.Eligibility270271.Segments;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.Eligibility270271.Parsing;

/// <summary>
/// Parser for HIPAA X12 5010 Transactions 270/271 (Eligibility Inquiry and Response).
/// Implementation Guide: 005010X279A1.
/// Automatically detects 270 vs 271 from ST01 and returns the appropriate model type.
/// </summary>
public sealed class Eligibility270271Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public Eligibility270271Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    /// <summary>
    /// Parses 270 (inquiry) files.
    /// </summary>
    public EdiBatchResult<Eligibility270Model> Parse270File(string fileContent)
    {
        return ParseFile<Eligibility270Model>(fileContent, "270");
    }

    /// <summary>
    /// Parses 271 (response) files.
    /// </summary>
    public EdiBatchResult<Eligibility271Model> Parse271File(string fileContent)
    {
        return ParseFile<Eligibility271Model>(fileContent, "271");
    }

    private EdiBatchResult<T> ParseFile<T>(string fileContent, string expectedTxnId) where T : EligibilityBaseModel, new()
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
                    StControlNumber = GetStControlNumber(txnGroup, tokens.Delimiters),
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

    internal T ParseTransaction<T>(TransactionSegmentGroup group, DelimiterContext delimiters) where T : EligibilityBaseModel, new()
    {
        var segments = group.Segments;
        var model = new T();

        var stElements = delimiters.SplitElements(segments[0]);
        model.TransactionSetHeader = StSegment.Parse(stElements);

        if (group.IsaElements != null)
            model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null)
            model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        ParseSegmentsIntoModel(model, segments, delimiters);

        // Apply extensions
        foreach (var ext in _options.ExtendedParsers.OfType<IExtendedParser<T>>())
            ext.PostProcess(model, segments, delimiters);

        if (_options.ValidationMode == ValidationMode.Strict && model.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))
            throw new EdiValidationException(model.ValidationIssues);

        return model;
    }

    /// <summary>
    /// State machine for parsing 270/271 segments.
    /// 
    /// Structure (both 270 and 271):
    ///   ST → BHT
    ///   → HL*20 (2000A Info Source) → NM1/REF/N3/N4/PER
    ///   → HL*21 (2000B Info Receiver) → NM1/REF/N3/N4/PRV
    ///   → HL*22 (2000C Subscriber) → NM1/REF/N3/N4/DMG/DTP
    ///     → EQ (270 inquiry) or EB (271 response) → REF/DTP/MSG
    ///   → HL*23 (2000D Dependent) → NM1/REF/N3/N4/DMG/DTP/INS
    ///     → EQ (270 inquiry) or EB (271 response) → REF/DTP/MSG
    ///   → SE
    /// </summary>
    private void ParseSegmentsIntoModel(EligibilityBaseModel model, List<string> segments, DelimiterContext delimiters)
    {
        EligibilitySubscriberLoop? currentSubscriber = null;
        EligibilityDependentLoop? currentDependent = null;
        BenefitLoop? currentBenefit = null;
        string loopContext = "HEADER";

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

                    case "BHT":
                        // BHT - Beginning of Hierarchical Transaction. Store as reference.
                        break;

                    case "HL":
                        var hl = HlSegment.Parse(elements);
                        currentBenefit = null;
                        switch (hl.HierarchicalLevelCode)
                        {
                            case "20": // Information Source (Payer)
                                model.InformationSource.HierarchicalLevel = hl;
                                loopContext = "2000A";
                                currentSubscriber = null;
                                currentDependent = null;
                                break;
                            case "21": // Information Receiver (Provider)
                                model.InformationReceiver.HierarchicalLevel = hl;
                                loopContext = "2000B";
                                currentSubscriber = null;
                                currentDependent = null;
                                break;
                            case "22": // Subscriber
                                currentSubscriber = new EligibilitySubscriberLoop { HierarchicalLevel = hl };
                                model.Subscribers.Add(currentSubscriber);
                                currentDependent = null;
                                loopContext = "2000C";
                                break;
                            case "23": // Dependent
                                currentDependent = new EligibilityDependentLoop { HierarchicalLevel = hl };
                                model.Dependents.Add(currentDependent);
                                loopContext = "2000D";
                                break;
                        }
                        break;

                    case "NM1":
                        var nm1 = Nm1Segment.Parse(elements);
                        switch (loopContext)
                        {
                            case "2000A":
                                model.InformationSource.Name = nm1;
                                loopContext = "2100A";
                                break;
                            case "2000B":
                                model.InformationReceiver.Name = nm1;
                                loopContext = "2100B";
                                break;
                            case "2000C":
                            case "2100C":
                                if (currentSubscriber != null)
                                    currentSubscriber.Name = nm1;
                                loopContext = "2100C";
                                break;
                            case "2000D":
                            case "2100D":
                                if (currentDependent != null)
                                    currentDependent.Name = nm1;
                                loopContext = "2100D";
                                break;
                            case "2110": // Loop 2120 - Benefit-related entity
                                if (currentBenefit != null)
                                    currentBenefit.BenefitRelatedEntity = nm1;
                                break;
                        }
                        break;

                    case "N3":
                        var n3 = N3Segment.Parse(elements);
                        switch (loopContext)
                        {
                            case "2100A": model.InformationSource.Address = n3; break;
                            case "2100B": model.InformationReceiver.Address = n3; break;
                            case "2100C": if (currentSubscriber != null) currentSubscriber.Address = n3; break;
                            case "2100D": if (currentDependent != null) currentDependent.Address = n3; break;
                        }
                        break;

                    case "N4":
                        var n4 = N4Segment.Parse(elements);
                        switch (loopContext)
                        {
                            case "2100A": model.InformationSource.CityStateZip = n4; break;
                            case "2100B": model.InformationReceiver.CityStateZip = n4; break;
                            case "2100C": if (currentSubscriber != null) currentSubscriber.CityStateZip = n4; break;
                            case "2100D": if (currentDependent != null) currentDependent.CityStateZip = n4; break;
                        }
                        break;

                    case "REF":
                        var refSeg = RefSegment.Parse(elements);
                        if (currentBenefit != null && loopContext == "2110")
                            currentBenefit.References.Add(refSeg);
                        else if (currentDependent != null && loopContext is "2000D" or "2100D")
                            currentDependent.References.Add(refSeg);
                        else if (currentSubscriber != null && loopContext is "2000C" or "2100C")
                            currentSubscriber.References.Add(refSeg);
                        else if (loopContext is "2100B" or "2000B")
                            model.InformationReceiver.References.Add(refSeg);
                        else if (loopContext is "2100A" or "2000A")
                            model.InformationSource.References.Add(refSeg);
                        break;

                    case "DTP":
                        var dtp = DtpSegment.Parse(elements);
                        if (currentBenefit != null && loopContext == "2110")
                            currentBenefit.Dates.Add(dtp);
                        else if (currentDependent != null && loopContext is "2000D" or "2100D")
                            currentDependent.Dates.Add(dtp);
                        else if (currentSubscriber != null && loopContext is "2000C" or "2100C")
                            currentSubscriber.Dates.Add(dtp);
                        else
                            model.TransactionDates.Add(dtp);
                        break;

                    case "DMG":
                        var dmg = DmgSegment.Parse(elements);
                        if (currentDependent != null && loopContext is "2000D" or "2100D")
                            currentDependent.Demographics = dmg;
                        else if (currentSubscriber != null && loopContext is "2000C" or "2100C")
                            currentSubscriber.Demographics = dmg;
                        break;

                    case "PER":
                        if (loopContext is "2100A" or "2000A")
                            model.InformationSource.Contact = PerSegment.Parse(elements);
                        break;

                    case "PRV":
                        if (loopContext is "2100B" or "2000B")
                            model.InformationReceiver.ProviderInfo = PrvSegment.Parse(elements);
                        break;

                    case "INS":
                        // INS in 270/271 provides relationship info for dependents
                        if (currentDependent != null)
                        {
                            var ins = elements;
                            currentDependent.RelationshipCode = elements.ElementAtOrDefault(2) ?? "";
                        }
                        break;

                    case "EQ": // 270 Inquiry
                        var eq = EqSegment.Parse(elements);
                        if (currentDependent != null)
                            currentDependent.Inquiries.Add(eq);
                        else if (currentSubscriber != null)
                            currentSubscriber.Inquiries.Add(eq);
                        break;

                    case "EB": // 271 Response
                        var eb = EbSegment.Parse(elements);
                        currentBenefit = new BenefitLoop { EligibilityBenefit = eb };
                        if (currentDependent != null)
                        {
                            currentDependent.Benefits.Add(currentBenefit);
                        }
                        else if (currentSubscriber != null)
                        {
                            currentSubscriber.Benefits.Add(currentBenefit);
                        }
                        loopContext = "2110";
                        break;

                    case "MSG":
                        if (currentBenefit != null)
                            currentBenefit.Messages.Add(MsgSegment.Parse(elements));
                        break;

                    case "III":
                        if (currentBenefit != null)
                            currentBenefit.AdditionalInfo.Add(IiiSegment.Parse(elements));
                        break;

                    case "LS":
                    case "LE":
                        // LS/LE are loop header/trailer markers - no model impact
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

    private void AddWarning(EligibilityBaseModel model, string rawSegment, string description)
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

    // ── Streaming Methods ───────────────────────────────────────

    private async IAsyncEnumerable<T> ParseFileStreamingInternal<T>(string filePath) where T : EligibilityBaseModel, new()
    {
        var tokenizer = new StreamingEdiTokenizer(_options);
        await foreach (var txnGroup in tokenizer.TokenizeFileAsync(filePath))
        {
            T? model = null;
            try
            {
                var group = new TransactionSegmentGroup { Segments = txnGroup.Segments, IsaElements = txnGroup.IsaElements, GsElements = txnGroup.GsElements };
                model = ParseTransaction<T>(group, txnGroup.Delimiters);
            }
            catch { continue; }
            if (model != null) yield return model;
        }
    }

    private async IAsyncEnumerable<T> ParseStreamingInternal<T>(Stream stream) where T : EligibilityBaseModel, new()
    {
        var tokenizer = new StreamingEdiTokenizer(_options);
        await foreach (var txnGroup in tokenizer.TokenizeAsync(stream))
        {
            T? model = null;
            try
            {
                var group = new TransactionSegmentGroup { Segments = txnGroup.Segments, IsaElements = txnGroup.IsaElements, GsElements = txnGroup.GsElements };
                model = ParseTransaction<T>(group, txnGroup.Delimiters);
            }
            catch { continue; }
            if (model != null) yield return model;
        }
    }

    /// <summary>Streams a 270 file transaction-by-transaction.</summary>
    public IAsyncEnumerable<Eligibility270Model> Parse270FileStreamingAsync(string filePath) => ParseFileStreamingInternal<Eligibility270Model>(filePath);

    /// <summary>Streams a 271 file transaction-by-transaction.</summary>
    public IAsyncEnumerable<Eligibility271Model> Parse271FileStreamingAsync(string filePath) => ParseFileStreamingInternal<Eligibility271Model>(filePath);

    /// <summary>Streams a 270 from a Stream.</summary>
    public IAsyncEnumerable<Eligibility270Model> Parse270StreamingAsync(Stream stream) => ParseStreamingInternal<Eligibility270Model>(stream);

    /// <summary>Streams a 271 from a Stream.</summary>
    public IAsyncEnumerable<Eligibility271Model> Parse271StreamingAsync(Stream stream) => ParseStreamingInternal<Eligibility271Model>(stream);

    /// <summary>Processes a 270 file in batches.</summary>
    public IAsyncEnumerable<StreamingBatchResult<Eligibility270Model>> Parse270FileInBatchesAsync(string filePath, int batchSize = 500)
    {
        var processor = new EdiBatchProcessor<Eligibility270Model>(batchSize, _options);
        return processor.ProcessFileAsync(filePath, (g, d) => ParseTransaction<Eligibility270Model>(g, d));
    }

    /// <summary>Processes a 271 file in batches.</summary>
    public IAsyncEnumerable<StreamingBatchResult<Eligibility271Model>> Parse271FileInBatchesAsync(string filePath, int batchSize = 500)
    {
        var processor = new EdiBatchProcessor<Eligibility271Model>(batchSize, _options);
        return processor.ProcessFileAsync(filePath, (g, d) => ParseTransaction<Eligibility271Model>(g, d));
    }
}
