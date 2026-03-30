using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Eligibility270271.Segments;

namespace HealthcareEdi.Transactions.Eligibility270271.Loops;

/// <summary>Loop 2000A - Information Source (Payer)</summary>
[EdiLoop("2000A")]
public class InfoSourceLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();

    // 2100A - Information Source Name
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public PerSegment? Contact { get; set; }

    public string SourceName => Name.FullName;
    public string? SourceId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

/// <summary>Loop 2000B - Information Receiver (Provider requesting eligibility)</summary>
[EdiLoop("2000B")]
public class InfoReceiverLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();

    // 2100B - Information Receiver Name
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public PrvSegment? ProviderInfo { get; set; }

    public string ReceiverName => Name.FullName;
    public string? Npi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;
}

/// <summary>Loop 2000C - Subscriber Level</summary>
[EdiLoop("2000C")]
public class EligibilitySubscriberLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();

    // 2100C - Subscriber Name
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public DmgSegment? Demographics { get; set; }
    public List<DtpSegment> Dates { get; set; } = [];

    // 270: Inquiry details
    public List<EqSegment> Inquiries { get; set; } = [];

    // 271: Benefit/eligibility responses
    public List<BenefitLoop> Benefits { get; set; } = [];

    // ── Convenience ─────────────────────────────────────────────

    public string SubscriberName => Name.FullName;
    public string? MemberId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
    public string? DateOfBirth => Demographics?.DateOfBirth;
    public string? Gender => Demographics?.GenderCode;

    /// <summary>True if subscriber has dependent(s) below in hierarchy.</summary>
    public bool HasDependents => HierarchicalLevel.HierarchicalChildCode == "1";

    /// <summary>Gets REF by qualifier.</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);
}

/// <summary>Loop 2000D - Dependent Level</summary>
[EdiLoop("2000D")]
public class EligibilityDependentLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();

    // 2100D - Dependent Name
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public DmgSegment? Demographics { get; set; }
    public List<DtpSegment> Dates { get; set; } = [];
    public string RelationshipCode { get; set; } = string.Empty; // From INS02

    // 270: Inquiry details
    public List<EqSegment> Inquiries { get; set; } = [];

    // 271: Benefit/eligibility responses
    public List<BenefitLoop> Benefits { get; set; } = [];

    // ── Convenience ─────────────────────────────────────────────

    public string DependentName => Name.FullName;
    public string? DateOfBirth => Demographics?.DateOfBirth;
    public string? Gender => Demographics?.GenderCode;

    public string Relationship => RelationshipCode switch
    {
        "01" => "Spouse",
        "19" => "Child",
        "20" => "Employee",
        "21" => "Unknown",
        "34" => "Other Adult",
        "39" => "Organ Donor",
        "53" => "Life Partner",
        _ => $"Other ({RelationshipCode})"
    };
}

/// <summary>Loop 2110 - Eligibility/Benefit Information (271 response)</summary>
[EdiLoop("2110")]
public class BenefitLoop
{
    public EbSegment EligibilityBenefit { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];
    public List<MsgSegment> Messages { get; set; } = [];
    public List<IiiSegment> AdditionalInfo { get; set; } = [];

    // Nested provider for this benefit (Loop 2120)
    public Nm1Segment? BenefitRelatedEntity { get; set; }

    // ── Convenience ─────────────────────────────────────────────

    public string BenefitCode => EligibilityBenefit.EligibilityOrBenefitCode;
    public string BenefitDescription => EligibilityBenefit.BenefitDescription;
    public string ServiceType => EligibilityBenefit.ServiceTypeCode;
    public string ServiceTypeDescription => EligibilityBenefit.ServiceTypeDescription;
    public decimal Amount => EligibilityBenefit.MonetaryAmount;
    public decimal Percent => EligibilityBenefit.Percent;
    public string TimePeriod => EligibilityBenefit.TimePeriodDescription;
    public string CoverageLevel => EligibilityBenefit.CoverageLevelCode;
    public bool IsInNetwork => EligibilityBenefit.IsInNetwork;
    public bool IsOutOfNetwork => EligibilityBenefit.IsOutOfNetwork;
    public bool IsActive => EligibilityBenefit.IsActive;

    /// <summary>Plan effective date (DTP*291).</summary>
    public string? PlanBeginDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "291")?.DateTimePeriod;

    /// <summary>Plan end date (DTP*292).</summary>
    public string? PlanEndDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "292")?.DateTimePeriod;

    /// <summary>Eligibility date (DTP*307).</summary>
    public string? EligibilityDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "307")?.DateTimePeriod;

    /// <summary>Benefit begin date (DTP*346).</summary>
    public string? BenefitBeginDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "346")?.DateTimePeriod;

    /// <summary>All free-form message text concatenated.</summary>
    public string AllMessages => string.Join(" ", Messages.Select(m => m.FreeFormMessageText));
}
