using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Transactions.Remittance835.Loops;
using HealthcareEdi.Transactions.Remittance835.Segments;

namespace HealthcareEdi.Transactions.Remittance835.Models;

/// <summary>
/// 835 - Health Care Claim Payment/Advice (Remittance / ERA).
/// Implementation Guide: 005010X221A1.
/// </summary>
public class Remittance835Model : EdiTransactionBase
{
    // ── Transaction Header ──────────────────────────────────────
    /// <summary>BPR - Financial Information (payment method, amount, bank details).</summary>
    public BprSegment FinancialInformation { get; set; } = new();

    /// <summary>TRN - Reassociation Trace Number (check # or EFT trace).</summary>
    public TrnSegment TraceNumber { get; set; } = new();

    /// <summary>DTM at transaction level (e.g., production date).</summary>
    public List<Core.Segments.DtpSegment> TransactionDates { get; set; } = [];

    /// <summary>REF at transaction level.</summary>
    public List<Core.Segments.RefSegment> TransactionReferences { get; set; } = [];

    // ── Header Loops ────────────────────────────────────────────
    /// <summary>Loop 1000A - Payer Identification.</summary>
    public PayerLoop Payer { get; set; } = new();

    /// <summary>Loop 1000B - Payee Identification (Provider).</summary>
    public PayeeLoop Payee { get; set; } = new();

    // ── Claims ──────────────────────────────────────────────────
    /// <summary>Loop 2100 - All claims in this remittance.</summary>
    public List<RemittanceClaimLoop> Claims { get; set; } = [];

    // ── Provider Adjustments ────────────────────────────────────
    /// <summary>PLB - Provider-level adjustments (interest, forwarding balance, etc.).</summary>
    public List<PlbSegment> ProviderAdjustments { get; set; } = [];

    // ── Convenience Properties ──────────────────────────────────

    /// <summary>Total payment amount from BPR.</summary>
    public decimal TotalPaymentAmount => FinancialInformation.TotalPaymentAmount;

    /// <summary>Payment method (ACH, CHK, NON).</summary>
    public string PaymentMethod => FinancialInformation.PaymentMethodCode;

    /// <summary>True if payment is via EFT/ACH.</summary>
    public bool IsEft => FinancialInformation.IsEft;

    /// <summary>Check number or EFT trace number.</summary>
    public string CheckOrTraceNumber => TraceNumber.TraceNumber;

    /// <summary>Payment date from BPR.</summary>
    public string PaymentDate => FinancialInformation.PaymentDate;

    /// <summary>Number of claims in this remittance.</summary>
    public int ClaimCount => Claims.Count;

    /// <summary>Total charges across all claims.</summary>
    public decimal TotalCharges => Claims.Sum(c => c.ChargeAmount);

    /// <summary>Total paid across all claims (should match BPR02 minus PLB adjustments).</summary>
    public decimal TotalClaimPayments => Claims.Sum(c => c.PaymentAmount);

    /// <summary>Total provider-level adjustments.</summary>
    public decimal TotalProviderAdjustments => ProviderAdjustments.Sum(p => p.TotalAdjustmentAmount);

    /// <summary>Claims that were denied.</summary>
    public IEnumerable<RemittanceClaimLoop> DeniedClaims => Claims.Where(c => c.IsDenied);

    /// <summary>Claims that are reversals.</summary>
    public IEnumerable<RemittanceClaimLoop> ReversedClaims => Claims.Where(c => c.IsReversal);

    /// <summary>Claims that received payment > 0.</summary>
    public IEnumerable<RemittanceClaimLoop> PaidClaims => Claims.Where(c => c.PaymentAmount > 0);

    // ── PHI Redaction ───────────────────────────────────────────
    public override void RedactPhi()
    {
        base.RedactPhi();

        // Bank account numbers
        FinancialInformation.SenderAccountNumber = FullRedact(FinancialInformation.SenderAccountNumber);
        FinancialInformation.ReceiverAccountNumber = FullRedact(FinancialInformation.ReceiverAccountNumber);
        FinancialInformation.SenderDfiNumber = MaskValue(FinancialInformation.SenderDfiNumber);
        FinancialInformation.ReceiverDfiNumber = MaskValue(FinancialInformation.ReceiverDfiNumber);

        // Payer/Payee names
        Payer.Name.LastName = MaskValue(Payer.Name.LastName);
        Payee.Name.LastName = MaskValue(Payee.Name.LastName);
        Payee.Name.FirstName = MaskValue(Payee.Name.FirstName);

        // Claims - patient info
        foreach (var claim in Claims)
        {
            claim.ClaimPayment.PatientControlNumber = MaskValue(claim.ClaimPayment.PatientControlNumber);

            foreach (var nm1 in claim.Names)
            {
                nm1.LastName = MaskValue(nm1.LastName);
                nm1.FirstName = MaskValue(nm1.FirstName);
                nm1.IdCode = MaskValue(nm1.IdCode);
            }
        }

        // Provider adjustments - tax IDs
        foreach (var plb in ProviderAdjustments)
        {
            plb.ProviderIdentifier = MaskValue(plb.ProviderIdentifier);
        }
    }
}
