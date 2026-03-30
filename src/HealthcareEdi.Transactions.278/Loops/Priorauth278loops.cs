using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Core.Validation;
using HealthcareEdi.Transactions.PriorAuth278.Segments;
using System.Diagnostics;

namespace HealthcareEdi.Transactions.PriorAuth278.Loops;

[EdiLoop("2000A")]
public class ReviewPayerLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public string PayerName => Name.FullName;
}

[EdiLoop("2000B")]
public class ReviewProviderLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public PrvSegment? ProviderInfo { get; set; }
    public string ProviderName => Name.FullName;
    public string? Npi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;
}

[EdiLoop("2000C")]
public class ReviewSubscriberLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public DmgSegment? Demographics { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public string SubscriberName => Name.FullName;
    public string? MemberId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

[EdiLoop("2000E")]
public class ReviewPatientLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public DmgSegment? Demographics { get; set; }
    public string PatientName => Name.FullName;
}

/// <summary>Loop 2000F - Service Review (contains UM, HCR, HI, DTP)</summary>
[EdiLoop("2000F")]
public class ServiceReviewLoop
{
    public UmSegment? UtilizationManagement { get; set; }
    public HcrSegment? ReviewDecision { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];

    /// <summary>Diagnosis codes from HI segments.</summary>
    public List<string> DiagnosisCodes { get; set; } = [];

    /// <summary>Authorization/certification number from HCR02.</summary>
    public string? AuthorizationNumber => ReviewDecision?.ReviewIdentificationNumber;

    /// <summary>Payer decision text.</summary>
    public string? Decision => ReviewDecision?.Decision;

    /// <summary>True if authorized.</summary>
    public bool IsCertified => ReviewDecision?.IsCertified ?? false;

    /// <summary>True if denied.</summary>
    public bool IsDenied => ReviewDecision?.IsDenied ?? false;
}
