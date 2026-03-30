using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using HealthcareEdi.Transactions.Enrollment834.Loops;
using HealthcareEdi.Transactions.Enrollment834.Models;
using HealthcareEdi.Transactions.Enrollment834.Segments;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.Enrollment834.Parsing;

/// <summary>
/// Parser for HIPAA X12 5010 Transaction 834 (Benefit Enrollment and Maintenance).
/// Implementation Guide: 005010X220A1.
/// </summary>
public sealed class Enrollment834Parser
{
    private readonly ParserOptions _options;
    private readonly EdiTokenizer _tokenizer;

    public Enrollment834Parser(ParserOptions? options = null)
    {
        _options = options ?? new ParserOptions();
        _tokenizer = new EdiTokenizer(_options);
    }

    /// <summary>
    /// Parses an 834 EDI file and returns a batch result.
    /// </summary>
    public EdiBatchResult<Enrollment834Model> ParseFile(string fileContent)
    {
        var sw = Stopwatch.StartNew();
        var tokens = _tokenizer.Tokenize(fileContent);

        var successList = new List<Enrollment834Model>();
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

        return new EdiBatchResult<Enrollment834Model>
        {
            Transactions = successList,
            FailedTransactions = failedList,
            InterchangeHeader = tokens.IsaElements != null ? IsaSegment.Parse(tokens.IsaElements) : null,
            FunctionalGroupHeader = tokens.GsElements != null ? GsSegment.Parse(tokens.GsElements) : null,
            ParseDurationMs = sw.ElapsedMilliseconds,
        };
    }

    private Enrollment834Model ParseTransaction(TransactionSegmentGroup group, DelimiterContext delimiters)
    {
        var segments = group.Segments;
        var model = new Enrollment834Model();

        var stElements = delimiters.SplitElements(segments[0]);
        model.TransactionSetHeader = StSegment.Parse(stElements);

        if (group.IsaElements != null)
            model.InterchangeHeader = IsaSegment.Parse(group.IsaElements);
        if (group.GsElements != null)
            model.FunctionalGroupHeader = GsSegment.Parse(group.GsElements);

        ParseSegmentsIntoModel(model, segments, delimiters);

        foreach (var ext in _options.ExtendedParsers.OfType<IExtendedParser<Enrollment834Model>>())
            ext.PostProcess(model, segments, delimiters);

        if (_options.ValidationMode == ValidationMode.Strict && model.ValidationIssues.Any(i => i.Severity == ValidationSeverity.Error))
            throw new EdiValidationException(model.ValidationIssues);

        return model;
    }

    /// <summary>
    /// State machine for parsing 834 segments.
    /// 
    /// 834 structure:
    ///   ST → BGN → REF/DTP (transaction level)
    ///   → N1*P5 (1000A Sponsor) → N3/N4
    ///   → N1*IN (1000B Payer) → N3/N4
    ///   → INS (2000 Member) → REF/DTP
    ///     → NM1*IL (2100A Member Name) → N3/N4/DMG/PER/REF
    ///     → NM1*31/36/etc (2100C-G Additional Names)
    ///     → HD (2300 Health Coverage) → DTP/REF/IDC
    ///       → LX/NM1 (2310 Provider)
    ///     → LX (2700 Reporting Category)
    ///       → N1 (2750 Reporting Detail) → REF/DTP
    ///   → SE
    /// </summary>
    private void ParseSegmentsIntoModel(Enrollment834Model model, List<string> segments, DelimiterContext delimiters)
    {
        MemberLoop? currentMember = null;
        HealthCoverageLoop? currentCoverage = null;
        ReportingCategoryLoop? currentReportingCategory = null;
        ReportingDetailLoop? currentReportingDetail = null;
        CoverageProviderLoop? currentProvider = null;
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

                    case "BGN":
                        // BGN is the transaction set purpose - store key data as references
                        // BGN02 = Transaction Set Reference Number, BGN03 = Date, BGN08 = Action Code
                        model.TransactionReferences.Add(new RefSegment
                        {
                            RawElements = elements,
                            ReferenceIdQualifier = "BGN",
                            ReferenceId = elements.ElementAtOrDefault(2) ?? "",
                        });
                        break;

                    case "N1":
                        var entityCode = elements.ElementAtOrDefault(1) ?? "";
                        switch (entityCode)
                        {
                            case "P5": // Sponsor (Plan Sponsor)
                                model.Sponsor.Name = ParseN1AsNm1(elements);
                                loopContext = "1000A";
                                currentMember = null;
                                currentCoverage = null;
                                break;
                            case "IN": // Insurer/Payer
                                model.Payer.Name = ParseN1AsNm1(elements);
                                loopContext = "1000B";
                                currentMember = null;
                                currentCoverage = null;
                                break;
                            default:
                                // Within Loop 2750 - Reporting Category Detail
                                if (currentReportingCategory != null && loopContext == "2700")
                                {
                                    currentReportingDetail = new ReportingDetailLoop
                                    {
                                        CategoryCode = entityCode,
                                        CategoryName = elements.ElementAtOrDefault(2) ?? "",
                                    };
                                    currentReportingCategory.Details.Add(currentReportingDetail);
                                    loopContext = "2750";
                                }
                                break;
                        }
                        break;

                    case "N3":
                        var n3 = N3Segment.Parse(elements);
                        switch (loopContext)
                        {
                            case "1000A": model.Sponsor.Address = n3; break;
                            case "1000B": model.Payer.Address = n3; break;
                            case "2100A":
                                if (currentMember != null) currentMember.MemberAddress = n3;
                                break;
                            case "2100X":
                                var lastAddl = currentMember?.AdditionalNames.LastOrDefault();
                                if (lastAddl != null) lastAddl.Address = n3;
                                break;
                            case "2310":
                                if (currentProvider != null) currentProvider.Address = n3;
                                break;
                        }
                        break;

                    case "N4":
                        var n4 = N4Segment.Parse(elements);
                        switch (loopContext)
                        {
                            case "1000A": model.Sponsor.CityStateZip = n4; break;
                            case "1000B": model.Payer.CityStateZip = n4; break;
                            case "2100A":
                                if (currentMember != null) currentMember.MemberCityStateZip = n4;
                                break;
                            case "2100X":
                                var lastAddl = currentMember?.AdditionalNames.LastOrDefault();
                                if (lastAddl != null) lastAddl.CityStateZip = n4;
                                break;
                            case "2310":
                                if (currentProvider != null) currentProvider.CityStateZip = n4;
                                break;
                        }
                        break;

                    case "INS":
                        currentMember = new MemberLoop
                        {
                            InsuredBenefit = InsSegment.Parse(elements),
                        };
                        model.Members.Add(currentMember);
                        currentCoverage = null;
                        currentReportingCategory = null;
                        currentReportingDetail = null;
                        currentProvider = null;
                        loopContext = "2000";
                        break;

                    case "NM1":
                        if (currentMember == null) break;
                        var nm1 = Nm1Segment.Parse(elements);
                        switch (nm1.EntityIdentifierCode)
                        {
                            case "IL": // Insured/Member
                            case "70": // Prior Incorrect Insured
                                currentMember.MemberName = nm1;
                                loopContext = "2100A";
                                break;
                            case "P3": // Primary Care Provider (Loop 2310)
                            case "Y2": // Managed Care Organization
                                if (currentCoverage != null)
                                {
                                    currentProvider = new CoverageProviderLoop { Name = nm1 };
                                    currentCoverage.Providers.Add(currentProvider);
                                    loopContext = "2310";
                                }
                                break;
                            default:
                                // 2100C-G: Additional names (31=Mailing, 36=Employer, S3=School, etc.)
                                currentMember.AdditionalNames.Add(new AdditionalNameLoop { Name = nm1 });
                                loopContext = "2100X";
                                break;
                        }
                        break;

                    case "DMG":
                        if (currentMember != null && loopContext == "2100A")
                            currentMember.Demographics = DmgSegment.Parse(elements);
                        break;

                    case "PER":
                        if (currentMember != null && loopContext == "2100A")
                            currentMember.ContactInfo = PerSegment.Parse(elements);
                        break;

                    case "REF":
                        var refSeg = RefSegment.Parse(elements);
                        if (currentReportingDetail != null && loopContext == "2750")
                            currentReportingDetail.References.Add(refSeg);
                        else if (currentCoverage != null && loopContext == "2300")
                            currentCoverage.References.Add(refSeg);
                        else if (currentProvider != null && loopContext == "2310")
                            currentProvider.References.Add(refSeg);
                        else if (currentMember != null && loopContext == "2100A")
                            currentMember.MemberReferences.Add(refSeg);
                        else if (currentMember != null && loopContext == "2000")
                            currentMember.References.Add(refSeg);
                        else if (loopContext is "1000A" or "1000B" or "HEADER")
                            model.TransactionReferences.Add(refSeg);
                        break;

                    case "DTP":
                        var dtp = DtpSegment.Parse(elements);
                        if (currentReportingDetail != null && loopContext == "2750")
                            currentReportingDetail.Dates.Add(dtp);
                        else if (currentCoverage != null && loopContext == "2300")
                            currentCoverage.Dates.Add(dtp);
                        else if (currentMember != null && (loopContext == "2000" || loopContext == "2100A"))
                            currentMember.Dates.Add(dtp);
                        else
                            model.TransactionDates.Add(dtp);
                        break;

                    case "HD":
                        if (currentMember != null)
                        {
                            currentCoverage = new HealthCoverageLoop
                            {
                                HealthCoverage = HdSegment.Parse(elements),
                            };
                            currentMember.HealthCoverages.Add(currentCoverage);
                            currentProvider = null;
                            currentReportingCategory = null;
                            loopContext = "2300";
                        }
                        break;

                    case "IDC":
                        if (currentCoverage != null)
                            currentCoverage.IdentificationCards.Add(IdcSegment.Parse(elements));
                        break;

                    case "EC":
                        if (currentMember != null)
                            currentMember.EmploymentClass = EcSegment.Parse(elements);
                        break;

                    case "ICM":
                        if (currentMember != null)
                            currentMember.Income = IcmSegment.Parse(elements);
                        break;

                    case "LX":
                        // In 834, LX starts Loop 2700 (Reporting Categories).
                        // Loop 2310 providers are triggered by NM1 entity codes, not LX.
                        if (currentMember != null)
                        {
                            currentReportingCategory = new ReportingCategoryLoop();
                            if (int.TryParse(elements.ElementAtOrDefault(1), out var lineNum))
                                currentReportingCategory.LineNumber = lineNum;
                            currentMember.ReportingCategories.Add(currentReportingCategory);
                            currentCoverage = null;
                            currentProvider = null;
                            currentReportingDetail = null;
                            loopContext = "2700";
                        }
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
    /// 834 uses N1 for sponsor/payer (not NM1). Map to Nm1Segment for consistency.
    /// </summary>
    private static Nm1Segment ParseN1AsNm1(string[] elements)
    {
        return new Nm1Segment
        {
            RawElements = elements,
            EntityIdentifierCode = elements.ElementAtOrDefault(1) ?? "",
            EntityTypeQualifier = "2",
            LastName = elements.ElementAtOrDefault(2) ?? "",
            IdCodeQualifier = elements.ElementAtOrDefault(3) ?? "",
            IdCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }

    private void AddWarning(Enrollment834Model model, string rawSegment, string description)
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
