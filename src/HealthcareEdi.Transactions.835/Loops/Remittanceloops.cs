using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Remittance835.Segments;

namespace HealthcareEdi.Transactions.Remittance835.Loops;

/// <summary>Loop 1000A - Payer Identification</summary>
[EdiLoop("1000A")]
public class PayerLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public List<RefSegment> References { get; set; } = [];
    public PerSegment? Contact { get; set; }

    /// <summary>Payer name (organization name from NM103).</summary>
    public string PayerName => Name.FullName;

    /// <summary>Payer ID from NM109.</summary>
    public string? PayerId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

/// <summary>Loop 1000B - Payee Identification (Provider/Billing Entity)</summary>
[EdiLoop("1000B")]
public class PayeeLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public List<RefSegment> References { get; set; } = [];

    /// <summary>Payee name (provider/organization name).</summary>
    public string PayeeName => Name.FullName;

    /// <summary>Payee NPI (if NM108=XX) or Tax ID.</summary>
    public string? PayeeNpi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;

    /// <summary>Gets a reference by qualifier (e.g., TJ=Tax ID, PQ=Payee ID).</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);
}

/// <summary>Loop 2100 - Claim Payment Information</summary>
[EdiLoop("2100")]
public class RemittanceClaimLoop
{
    public ClpSegment ClaimPayment { get; set; } = new();
    public List<CasSegment> Adjustments { get; set; } = [];
    public List<Nm1Segment> Names { get; set; } = [];       // Patient, Insured, Corrected, etc.
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];

    // Nested service lines (Loop 2110)
    public List<RemittanceServiceLineLoop> ServiceLines { get; set; } = [];

    // ── Convenience Properties ──────────────────────────────────

    public string PatientControlNumber => ClaimPayment.PatientControlNumber;
    public decimal ChargeAmount => ClaimPayment.TotalClaimChargeAmount;
    public decimal PaymentAmount => ClaimPayment.ClaimPaymentAmount;
    public string ClaimStatusCode => ClaimPayment.ClaimStatusCode;
    public bool IsDenied => ClaimPayment.IsDenied;
    public bool IsReversal => ClaimPayment.IsReversal;
    public string PayerClaimControlNumber => ClaimPayment.PayerClaimControlNumber;

    /// <summary>Patient name from NM1 with entity code QC.</summary>
    public Nm1Segment? PatientName => Names.FirstOrDefault(n => n.EntityIdentifierCode == "QC");

    /// <summary>Insured/subscriber name from NM1 with entity code IL.</summary>
    public Nm1Segment? InsuredName => Names.FirstOrDefault(n => n.EntityIdentifierCode == "IL");

    /// <summary>Corrected patient name from NM1 with entity code 74.</summary>
    public Nm1Segment? CorrectedPatientName => Names.FirstOrDefault(n => n.EntityIdentifierCode == "74");

    /// <summary>All claim-level adjustments flattened into individual reason/amount details.</summary>
    public IEnumerable<AdjustmentDetail> AllAdjustments => Adjustments.SelectMany(c => c.Adjustments);

    /// <summary>Total claim-level adjustment amount.</summary>
    public decimal TotalAdjustmentAmount => Adjustments.Sum(c => c.TotalAdjustmentAmount);

    /// <summary>Contractual obligation adjustments (CO group code).</summary>
    public IEnumerable<AdjustmentDetail> ContractualAdjustments => AllAdjustments.Where(a => a.IsContractual);

    /// <summary>Patient responsibility adjustments (PR group code).</summary>
    public IEnumerable<AdjustmentDetail> PatientResponsibilityAdjustments => AllAdjustments.Where(a => a.IsPatientResponsibility);

    /// <summary>Gets REF by qualifier.</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);

    /// <summary>References grouped by qualifier.</summary>
    private IReadOnlyDictionary<string, List<RefSegment>>? _refByQualifier;
    public IReadOnlyDictionary<string, List<RefSegment>> ReferenceByQualifier
        => _refByQualifier ??= References
            .GroupBy(r => r.ReferenceIdQualifier)
            .ToDictionary(g => g.Key, g => g.ToList());
}

/// <summary>Loop 2110 - Service Payment Information (Line Level)</summary>
[EdiLoop("2110")]
public class RemittanceServiceLineLoop
{
    public SvcSegment ServicePayment { get; set; } = new();
    public List<CasSegment> Adjustments { get; set; } = [];
    public List<RefSegment> References { get; set; } = [];
    public List<DtpSegment> Dates { get; set; } = [];
    public List<LqSegment> RemarkCodes { get; set; } = [];

    // ── Convenience Properties ──────────────────────────────────

    public string ProcedureCode => ServicePayment.Procedure.ProcedureCode;
    public decimal ChargeAmount => ServicePayment.LineItemChargeAmount;
    public decimal PaymentAmount => ServicePayment.LineItemPaymentAmount;
    public decimal AdjustmentAmount => ServicePayment.LineAdjustmentAmount;

    /// <summary>True if the payer changed the procedure code.</summary>
    public bool ProcedureCodeChanged => ServicePayment.OriginalProcedure != null
        && ServicePayment.OriginalProcedure.ProcedureCode != ServicePayment.Procedure.ProcedureCode;

    /// <summary>All service-level adjustments flattened.</summary>
    public IEnumerable<AdjustmentDetail> AllAdjustments => Adjustments.SelectMany(c => c.Adjustments);

    /// <summary>All remark codes (RARC) for this line.</summary>
    public IEnumerable<string> AllRemarkCodes => RemarkCodes.Select(lq => lq.RemarkCode);

    /// <summary>Service date from DTP qualifier 472.</summary>
    public string? ServiceDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "472")?.DateTimePeriod;
}
