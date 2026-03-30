using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.ClaimStatus276277.Segments;

namespace HealthcareEdi.Transactions.ClaimStatus276277.Loops;

/// <summary>Loop 2000A - Payer/Info Source</summary>
[EdiLoop("2000A")]
public class ClaimStatusPayerLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public string PayerName => Name.FullName;
    public string? PayerId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

/// <summary>Loop 2000B - Provider/Info Receiver</summary>
[EdiLoop("2000B")]
public class ClaimStatusProviderLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public List<RefSegment> References { get; set; } = [];
    public string ProviderName => Name.FullName;
    public string? Npi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;
}

/// <summary>Loop 2000C - Subscriber (Patient may be at 2000D)</summary>
[EdiLoop("2000C")]
public class ClaimStatusSubscriberLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public DmgSegment? Demographics { get; set; }
    public List<RefSegment> References { get; set; } = [];

    /// <summary>Loop 2200C - Claim status detail for this subscriber.</summary>
    public List<ClaimStatusDetailLoop> ClaimStatuses { get; set; } = [];

    public string SubscriberName => Name.FullName;
    public string? MemberId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

/// <summary>Loop 2000D - Patient (when different from subscriber)</summary>
[EdiLoop("2000D")]
public class ClaimStatusPatientLoop
{
    public HlSegment HierarchicalLevel { get; set; } = new();
    public Nm1Segment Name { get; set; } = new();
    public DmgSegment? Demographics { get; set; }
    public List<RefSegment> References { get; set; } = [];

    /// <summary>Loop 2200D - Claim status detail for this patient.</summary>
    public List<ClaimStatusDetailLoop> ClaimStatuses { get; set; } = [];

    public string PatientName => Name.FullName;
}

/// <summary>Loop 2200 - Claim Status Detail (contains TRN, STC, REF, DTP)</summary>
[EdiLoop("2200")]
public class ClaimStatusDetailLoop
{
    /// <summary>TRN - Trace number identifying the claim being inquired about.</summary>
    public string TraceType { get; set; } = string.Empty;
    public string TraceNumber { get; set; } = string.Empty;
    public string OriginatingCompanyId { get; set; } = string.Empty;

    /// <summary>STC segments - Status information (277 response only).</summary>
    public List<StcSegment> StatusCodes { get; set; } = [];

    /// <summary>References (payer claim number, provider control number, etc.).</summary>
    public List<RefSegment> References { get; set; } = [];

    /// <summary>Dates (service date, claim received date, etc.).</summary>
    public List<DtpSegment> Dates { get; set; } = [];

    /// <summary>Charge amount from CLM or AMT.</summary>
    public decimal ChargeAmount { get; set; }

    // ── Convenience ─────────────────────────────────────────────

    /// <summary>Most recent/primary status.</summary>
    public StcSegment? PrimaryStatus => StatusCodes.FirstOrDefault();

    /// <summary>Category of the primary status.</summary>
    public string? StatusCategory => PrimaryStatus?.StatusInformation.CategoryDescription;

    /// <summary>True if claim is finalized.</summary>
    public bool IsFinalized => PrimaryStatus?.StatusInformation.IsFinalized ?? false;

    /// <summary>True if claim is pending.</summary>
    public bool IsPending => PrimaryStatus?.StatusInformation.IsPending ?? false;

    /// <summary>True if claim was denied.</summary>
    public bool IsDenied => PrimaryStatus?.StatusInformation.IsDenied ?? false;

    /// <summary>True if claim was paid.</summary>
    public bool IsPaid => PrimaryStatus?.StatusInformation.IsPaid ?? false;

    /// <summary>Gets REF by qualifier.</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);

    /// <summary>Service date from DTP*472.</summary>
    public string? ServiceDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "472")?.DateTimePeriod;
}
