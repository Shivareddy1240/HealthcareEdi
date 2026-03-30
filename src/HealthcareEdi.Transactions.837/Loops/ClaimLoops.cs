using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Claim837.Segments;

namespace HealthcareEdi.Transactions.Claim837.Loops;

/// <summary>Loop 1000A - Submitter Name</summary>
[EdiLoop("1000A")]
public class SubmitterLoop
{
    public Nm1Segment Name { get; set; } = new();
    public PerSegment? Contact { get; set; }
}

/// <summary>Loop 1000B - Receiver Name</summary>
[EdiLoop("1000B")]
public class ReceiverLoop
{
    public Nm1Segment Name { get; set; } = new();
}

/// <summary>Loop 2000A - Billing Provider Hierarchical Level</summary>
[EdiLoop("2000A")]
public class BillingProviderLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public PrvSegment? ProviderInfo { get; set; }

    // Convenience
    public string? Npi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;
    public string? TaxonomyCode => ProviderInfo?.TaxonomyCode;
}

/// <summary>Loop 2000B - Subscriber Hierarchical Level</summary>
[EdiLoop("2000B")]
public class SubscriberLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public SbrSegment SubscriberInfo { get; set; } = new();

    // 2010BA - Subscriber Name
    public Nm1Segment SubscriberName { get; set; } = new();
    public N3Segment? SubscriberAddress { get; set; }
    public N4Segment? SubscriberCityStateZip { get; set; }
    public DmgSegment? Demographics { get; set; }
    public List<RefSegment> SubscriberReferences { get; set; } = [];

    // 2010BB - Payer Name
    public Nm1Segment PayerName { get; set; } = new();
    public List<RefSegment> PayerReferences { get; set; } = [];

    /// <summary>Whether this subscriber has a separate patient (HL04=1).</summary>
    public bool HasDependentPatient => HierarchicalLevel.HierarchicalChildCode == "1";

    /// <summary>Member ID from subscriber NM1.</summary>
    public string? MemberId => SubscriberName.IdCodeQualifier == "MI" ? SubscriberName.IdCode : null;
}

/// <summary>Loop 2000C - Patient Hierarchical Level (when patient differs from subscriber)</summary>
[EdiLoop("2000C")]
public class PatientLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment PatientName { get; set; } = new();
    public N3Segment? PatientAddress { get; set; }
    public N4Segment? PatientCityStateZip { get; set; }
    public DmgSegment? Demographics { get; set; }
    public List<RefSegment> References { get; set; } = [];
}

/// <summary>Loop 2300 - Claim Information</summary>
[EdiLoop("2300")]
public class ClaimLoop
{
    public ClmSegment ClaimInfo { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public List<HiSegment> DiagnosisCodes { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];
    public Cl1Segment? InstitutionalClaimCode { get; set; }  // 837I only

    // Nested loops
    public List<ProviderLoop> Providers { get; set; } = [];             // 2310A-E
    public List<OtherSubscriberLoop> OtherSubscribers { get; set; } = []; // 2320 (COB)
    public List<ServiceLineLoop> ServiceLines { get; set; } = [];       // 2400

    // ── Convenience properties ──────────────────────────────────
    public string PatientAccountNumber => ClaimInfo.PatientAccountNumber;
    public decimal TotalCharge => ClaimInfo.TotalClaimChargeAmount;

    /// <summary>All diagnosis codes flattened from all HI segments.</summary>
    public IEnumerable<DiagnosisCode> AllDiagnoses => DiagnosisCodes.SelectMany(hi => hi.DiagnosisCodes);

    /// <summary>Principal diagnosis (ABK/BK qualifier).</summary>
    public DiagnosisCode? PrincipalDiagnosis => AllDiagnoses.FirstOrDefault(d => d.IsPrincipal);

    /// <summary>Gets REF by qualifier.</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);

    /// <summary>References grouped by qualifier for dictionary-style access.</summary>
    private IReadOnlyDictionary<string, List<RefSegment>>? _refByQualifier;
    public IReadOnlyDictionary<string, List<RefSegment>> ReferenceByQualifier
        => _refByQualifier ??= References
            .GroupBy(r => r.ReferenceIdQualifier)
            .ToDictionary(g => g.Key, g => g.ToList());
}

/// <summary>Loop 2310A-E - Providers within a claim</summary>
[EdiLoop("2310")]
public class ProviderLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public PrvSegment? ProviderInfo { get; set; }

    public string? Npi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;
}

/// <summary>Loop 2320 - Other Subscriber Information (Coordination of Benefits)</summary>
[EdiLoop("2320")]
public class OtherSubscriberLoop
{
    public SbrSegment SubscriberInfo { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];

    // 2330A-F - Other Payer/Provider loops
    public List<OtherPayerLoop> OtherPayers { get; set; } = [];
}

/// <summary>Loop 2330A-F - Other Payer within COB</summary>
[EdiLoop("2330")]
public class OtherPayerLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];
}

/// <summary>Loop 2400 - Service Line</summary>
[EdiLoop("2400")]
public class ServiceLineLoop
{
    public int LineNumber { get; set; }    // LX01

    // Only one of these will be populated based on 837 variant
    public Sv1Segment? ProfessionalService { get; set; }    // 837P
    public Sv2Segment? InstitutionalService { get; set; }   // 837I
    public Sv3Segment? DentalService { get; set; }          // 837D

    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];

    // 2420A-F nested providers
    public List<ProviderLoop> RenderingProviders { get; set; } = [];

    // ── Convenience ─────────────────────────────────────────────
    public decimal ChargeAmount => ProfessionalService?.ChargeAmount
        ?? InstitutionalService?.ChargeAmount
        ?? DentalService?.ChargeAmount ?? 0;

    public string ProcedureCode => ProfessionalService?.Procedure.Code
        ?? InstitutionalService?.Procedure.Code
        ?? DentalService?.Procedure.Code ?? "";
}
