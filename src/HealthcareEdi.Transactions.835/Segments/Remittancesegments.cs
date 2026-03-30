using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Core.Segments;

namespace HealthcareEdi.Transactions.Remittance835.Segments;

/// <summary>
/// BPR - Financial Information. The most critical segment in an 835.
/// Carries payment method, amount, and EFT bank routing/account details.
/// </summary>
[EdiSegment("BPR")]
public class BprSegment : EdiSegmentBase
{
    public string TransactionHandlingCode { get; set; } = string.Empty;   // BPR01 (I=Remittance, H=Notification Only)
    public decimal TotalPaymentAmount { get; set; }                        // BPR02
    public string CreditDebitFlag { get; set; } = string.Empty;           // BPR03 (C=Credit, D=Debit)
    public string PaymentMethodCode { get; set; } = string.Empty;         // BPR04 (ACH=EFT, CHK=Check, NON=Non-payment)
    public string PaymentFormatCode { get; set; } = string.Empty;         // BPR05
    public string SenderDfiQualifier { get; set; } = string.Empty;        // BPR06
    public string SenderDfiNumber { get; set; } = string.Empty;           // BPR07 (Payer bank routing)
    public string SenderAccountQualifier { get; set; } = string.Empty;    // BPR08
    public string SenderAccountNumber { get; set; } = string.Empty;       // BPR09 (Payer bank account)
    public string ReceiverDfiQualifier { get; set; } = string.Empty;      // BPR12
    public string ReceiverDfiNumber { get; set; } = string.Empty;         // BPR13 (Provider bank routing)
    public string ReceiverAccountQualifier { get; set; } = string.Empty;  // BPR14
    public string ReceiverAccountNumber { get; set; } = string.Empty;     // BPR15 (Provider bank account)
    public string PaymentDate { get; set; } = string.Empty;               // BPR16

    /// <summary>True if payment method is EFT (ACH).</summary>
    public bool IsEft => PaymentMethodCode == "ACH";

    /// <summary>True if this is a zero-pay / non-payment remittance.</summary>
    public bool IsNonPayment => PaymentMethodCode == "NON" || TotalPaymentAmount == 0;

    public static BprSegment Parse(string[] elements)
    {
        var seg = new BprSegment
        {
            RawElements = elements,
            TransactionHandlingCode = elements.ElementAtOrDefault(1) ?? "",
            CreditDebitFlag = elements.ElementAtOrDefault(3) ?? "",
            PaymentMethodCode = elements.ElementAtOrDefault(4) ?? "",
            PaymentFormatCode = elements.ElementAtOrDefault(5) ?? "",
            SenderDfiQualifier = elements.ElementAtOrDefault(6) ?? "",
            SenderDfiNumber = elements.ElementAtOrDefault(7) ?? "",
            SenderAccountQualifier = elements.ElementAtOrDefault(8) ?? "",
            SenderAccountNumber = elements.ElementAtOrDefault(9) ?? "",
            ReceiverDfiQualifier = elements.ElementAtOrDefault(11) ?? "",
            ReceiverDfiNumber = elements.ElementAtOrDefault(12) ?? "",
            ReceiverAccountQualifier = elements.ElementAtOrDefault(13) ?? "",
            ReceiverAccountNumber = elements.ElementAtOrDefault(14) ?? "",
            PaymentDate = elements.ElementAtOrDefault(15) ?? "",
        };

        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var amount))
            seg.TotalPaymentAmount = amount;

        return seg;
    }
}

/// <summary>
/// TRN - Reassociation Trace Number. Required for matching 835 to the physical payment.
/// </summary>
[EdiSegment("TRN")]
public class TrnSegment : EdiSegmentBase
{
    public string TraceTypeCode { get; set; } = string.Empty;             // TRN01 (1=Current Transaction)
    public string TraceNumber { get; set; } = string.Empty;               // TRN02 (Check number or EFT trace)
    public string OriginatingCompanyId { get; set; } = string.Empty;      // TRN03
    public string OriginatingCompanySupplementalCode { get; set; } = string.Empty; // TRN04

    public static TrnSegment Parse(string[] elements)
    {
        return new TrnSegment
        {
            RawElements = elements,
            TraceTypeCode = elements.ElementAtOrDefault(1) ?? "",
            TraceNumber = elements.ElementAtOrDefault(2) ?? "",
            OriginatingCompanyId = elements.ElementAtOrDefault(3) ?? "",
            OriginatingCompanySupplementalCode = elements.ElementAtOrDefault(4) ?? "",
        };
    }
}

/// <summary>
/// CLP - Claim Payment Information. One per claim in an 835.
/// </summary>
[EdiSegment("CLP")]
public class ClpSegment : EdiSegmentBase
{
    public string PatientControlNumber { get; set; } = string.Empty;      // CLP01
    public string ClaimStatusCode { get; set; } = string.Empty;           // CLP02 (1=Processed Primary, 2=Processed Secondary, 4=Denied, 22=Reversal)
    public decimal TotalClaimChargeAmount { get; set; }                    // CLP03
    public decimal ClaimPaymentAmount { get; set; }                        // CLP04
    public decimal? PatientResponsibilityAmount { get; set; }              // CLP05
    public string ClaimFilingIndicatorCode { get; set; } = string.Empty;  // CLP06
    public string PayerClaimControlNumber { get; set; } = string.Empty;   // CLP07
    public string FacilityTypeCode { get; set; } = string.Empty;          // CLP08
    public string ClaimFrequencyCode { get; set; } = string.Empty;        // CLP09

    /// <summary>True if claim was denied (status code 4).</summary>
    public bool IsDenied => ClaimStatusCode == "4";

    /// <summary>True if this is a reversal/void (status code 22).</summary>
    public bool IsReversal => ClaimStatusCode == "22";

    /// <summary>Difference between charged and paid amounts.</summary>
    public decimal AdjustmentTotal => TotalClaimChargeAmount - ClaimPaymentAmount;

    public static ClpSegment Parse(string[] elements)
    {
        var seg = new ClpSegment
        {
            RawElements = elements,
            PatientControlNumber = elements.ElementAtOrDefault(1) ?? "",
            ClaimStatusCode = elements.ElementAtOrDefault(2) ?? "",
            ClaimFilingIndicatorCode = elements.ElementAtOrDefault(6) ?? "",
            PayerClaimControlNumber = elements.ElementAtOrDefault(7) ?? "",
            FacilityTypeCode = elements.ElementAtOrDefault(8) ?? "",
            ClaimFrequencyCode = elements.ElementAtOrDefault(9) ?? "",
        };

        if (decimal.TryParse(elements.ElementAtOrDefault(3), out var charge))
            seg.TotalClaimChargeAmount = charge;
        if (decimal.TryParse(elements.ElementAtOrDefault(4), out var paid))
            seg.ClaimPaymentAmount = paid;
        if (decimal.TryParse(elements.ElementAtOrDefault(5), out var patResp))
            seg.PatientResponsibilityAmount = patResp;

        return seg;
    }
}

/// <summary>
/// CAS - Claims Adjustment. Contains up to 6 adjustment reason/amount groups.
/// Can repeat multiple times per claim or service line.
/// </summary>
[EdiSegment("CAS")]
public class CasSegment : EdiSegmentBase
{
    public string ClaimAdjustmentGroupCode { get; set; } = string.Empty;  // CAS01 (CO, PR, OA, CR, PI)
    public List<AdjustmentDetail> Adjustments { get; set; } = [];

    /// <summary>Sum of all adjustment amounts in this CAS segment.</summary>
    public decimal TotalAdjustmentAmount => Adjustments.Sum(a => a.Amount);

    public static CasSegment Parse(string[] elements)
    {
        var seg = new CasSegment
        {
            RawElements = elements,
            ClaimAdjustmentGroupCode = elements.ElementAtOrDefault(1) ?? "",
        };

        // CAS has up to 6 triplets: ReasonCode, Amount, Quantity starting at element 2
        // Pattern: [2]=Reason1, [3]=Amount1, [4]=Qty1, [5]=Reason2, [6]=Amount2, [7]=Qty2, ...
        for (int i = 2; i < elements.Length && i <= 19; i += 3)
        {
            var reasonCode = elements.ElementAtOrDefault(i) ?? "";
            if (string.IsNullOrEmpty(reasonCode)) break;

            var adj = new AdjustmentDetail
            {
                ReasonCode = reasonCode,
                GroupCode = seg.ClaimAdjustmentGroupCode,
            };

            if (decimal.TryParse(elements.ElementAtOrDefault(i + 1), out var amount))
                adj.Amount = amount;
            if (decimal.TryParse(elements.ElementAtOrDefault(i + 2), out var qty))
                adj.Quantity = qty;

            seg.Adjustments.Add(adj);
        }

        return seg;
    }
}

/// <summary>
/// Individual adjustment reason/amount within a CAS segment.
/// </summary>
public class AdjustmentDetail
{
    /// <summary>Group code from parent CAS (CO=Contractual, PR=Patient Resp, OA=Other, CR=Correction, PI=Payor Initiated)</summary>
    public string GroupCode { get; set; } = string.Empty;

    /// <summary>CARC - Claim Adjustment Reason Code (e.g., 1=Deductible, 2=Coinsurance, 3=Copay, 45=Charge exceeds fee schedule)</summary>
    public string ReasonCode { get; set; } = string.Empty;

    /// <summary>Adjustment amount (positive = reduced from charge)</summary>
    public decimal Amount { get; set; }

    /// <summary>Quantity affected (optional)</summary>
    public decimal Quantity { get; set; }

    /// <summary>True if this is a contractual obligation adjustment.</summary>
    public bool IsContractual => GroupCode == "CO";

    /// <summary>True if this is a patient responsibility adjustment.</summary>
    public bool IsPatientResponsibility => GroupCode == "PR";

    public override string ToString() => $"{GroupCode}-{ReasonCode}: ${Amount:N2}";
}

/// <summary>
/// SVC - Service Payment Information. One per service line within a claim.
/// </summary>
[EdiSegment("SVC")]
public class SvcSegment : EdiSegmentBase
{
    public ServiceIdentifier Procedure { get; set; } = new();              // SVC01 (composite)
    public decimal LineItemChargeAmount { get; set; }                       // SVC02
    public decimal LineItemPaymentAmount { get; set; }                      // SVC03
    public string RevenueCode { get; set; } = string.Empty;                // SVC04
    public decimal UnitsOfServicePaid { get; set; }                         // SVC05
    public ServiceIdentifier? OriginalProcedure { get; set; }              // SVC06 (composite - original code if changed)
    public decimal OriginalUnitsOfService { get; set; }                     // SVC07

    /// <summary>Difference between charged and paid at line level.</summary>
    public decimal LineAdjustmentAmount => LineItemChargeAmount - LineItemPaymentAmount;

    public static SvcSegment Parse(string[] elements, DelimiterContext delimiters)
    {
        var seg = new SvcSegment { RawElements = elements };

        // SVC01 - Composite: Qualifier:ProcedureCode:Modifier1:Modifier2:...
        var svc01 = elements.ElementAtOrDefault(1) ?? "";
        if (!string.IsNullOrEmpty(svc01))
            seg.Procedure = ServiceIdentifier.FromComposite(svc01, delimiters);

        if (decimal.TryParse(elements.ElementAtOrDefault(2), out var charge))
            seg.LineItemChargeAmount = charge;
        if (decimal.TryParse(elements.ElementAtOrDefault(3), out var paid))
            seg.LineItemPaymentAmount = paid;

        seg.RevenueCode = elements.ElementAtOrDefault(4) ?? "";

        if (decimal.TryParse(elements.ElementAtOrDefault(5), out var units))
            seg.UnitsOfServicePaid = units;

        // SVC06 - Original procedure if payer changed the code
        var svc06 = elements.ElementAtOrDefault(6) ?? "";
        if (!string.IsNullOrEmpty(svc06))
            seg.OriginalProcedure = ServiceIdentifier.FromComposite(svc06, delimiters);

        if (decimal.TryParse(elements.ElementAtOrDefault(7), out var origUnits))
            seg.OriginalUnitsOfService = origUnits;

        return seg;
    }
}

/// <summary>
/// Strongly-typed service identifier from SVC composite elements.
/// </summary>
public class ServiceIdentifier
{
    /// <summary>Qualifier: HC=HCPCS, AD=ADA Dental, IV=ICD-10-PCS, NU=National Drug Code</summary>
    public string Qualifier { get; set; } = string.Empty;
    public string ProcedureCode { get; set; } = string.Empty;
    public string Modifier1 { get; set; } = string.Empty;
    public string Modifier2 { get; set; } = string.Empty;
    public string Modifier3 { get; set; } = string.Empty;
    public string Modifier4 { get; set; } = string.Empty;

    public string[] Modifiers => new[] { Modifier1, Modifier2, Modifier3, Modifier4 }
        .Where(m => !string.IsNullOrEmpty(m)).ToArray();

    public override string ToString() => $"{Qualifier}:{ProcedureCode}" +
        (Modifiers.Length > 0 ? $":{string.Join(":", Modifiers)}" : "");

    public static ServiceIdentifier FromComposite(string composite, DelimiterContext delimiters)
    {
        var components = delimiters.SplitComponents(composite);
        return new ServiceIdentifier
        {
            Qualifier = components.ElementAtOrDefault(0) ?? "",
            ProcedureCode = components.ElementAtOrDefault(1) ?? "",
            Modifier1 = components.ElementAtOrDefault(2) ?? "",
            Modifier2 = components.ElementAtOrDefault(3) ?? "",
            Modifier3 = components.ElementAtOrDefault(4) ?? "",
            Modifier4 = components.ElementAtOrDefault(5) ?? "",
        };
    }
}

/// <summary>
/// PLB - Provider Level Balance. Provider-level adjustments at the summary/trailer level.
/// </summary>
[EdiSegment("PLB")]
public class PlbSegment : EdiSegmentBase
{
    public string ProviderIdentifier { get; set; } = string.Empty;    // PLB01 (Provider Tax ID)
    public string FiscalPeriodDate { get; set; } = string.Empty;      // PLB02
    public List<ProviderAdjustment> Adjustments { get; set; } = [];

    /// <summary>Net provider-level adjustment total.</summary>
    public decimal TotalAdjustmentAmount => Adjustments.Sum(a => a.Amount);

    public static PlbSegment Parse(string[] elements, DelimiterContext delimiters)
    {
        var seg = new PlbSegment
        {
            RawElements = elements,
            ProviderIdentifier = elements.ElementAtOrDefault(1) ?? "",
            FiscalPeriodDate = elements.ElementAtOrDefault(2) ?? "",
        };

        // PLB has pairs: [3]=ReasonCode(composite), [4]=Amount, [5]=ReasonCode, [6]=Amount, ...
        for (int i = 3; i < elements.Length; i += 2)
        {
            var reasonComposite = elements.ElementAtOrDefault(i) ?? "";
            if (string.IsNullOrEmpty(reasonComposite)) break;

            var components = delimiters.SplitComponents(reasonComposite);
            var adj = new ProviderAdjustment
            {
                AdjustmentReasonCode = components.ElementAtOrDefault(0) ?? "",
                ReferenceId = components.ElementAtOrDefault(1) ?? "",
            };

            if (decimal.TryParse(elements.ElementAtOrDefault(i + 1), out var amount))
                adj.Amount = amount;

            seg.Adjustments.Add(adj);
        }

        return seg;
    }
}

/// <summary>
/// Provider-level adjustment from PLB segment.
/// </summary>
public class ProviderAdjustment
{
    /// <summary>Adjustment reason (e.g., 72=Authorized Return, FB=Forward Balance, L6=Interest)</summary>
    public string AdjustmentReasonCode { get; set; } = string.Empty;
    public string ReferenceId { get; set; } = string.Empty;
    public decimal Amount { get; set; }

    public override string ToString() => $"{AdjustmentReasonCode}: ${Amount:N2}";
}

/// <summary>
/// LQ - Health Care Remark Code. Provides additional explanation for adjustments.
/// </summary>
[EdiSegment("LQ")]
public class LqSegment : EdiSegmentBase
{
    public string CodeListQualifier { get; set; } = string.Empty;     // LQ01 (HE=Remittance Advice Remark Code)
    public string RemarkCode { get; set; } = string.Empty;            // LQ02 (RARC code, e.g., N130, MA18)

    public static LqSegment Parse(string[] elements)
    {
        return new LqSegment
        {
            RawElements = elements,
            CodeListQualifier = elements.ElementAtOrDefault(1) ?? "",
            RemarkCode = elements.ElementAtOrDefault(2) ?? "",
        };
    }
}
