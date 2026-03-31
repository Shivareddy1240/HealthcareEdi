using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Premium820.Loops;
using HealthcareEdi.Transactions.Premium820.Segments;

namespace HealthcareEdi.Transactions.Premium820.Models;

/// <summary>820 - Payroll Deducted Premium Payment. Implementation Guide: 005010X218.</summary>
public class PremiumPayment820Model : EdiTransactionBase
{
    public BprSegment820 FinancialInformation { get; set; } = new();
    public string TraceNumber { get; set; } = string.Empty;
    public List<RefSegment> TransactionReferences { get; set; } = [];
    public List<DtpSegment> TransactionDates { get; set; } = [];

    public PremiumRemitterLoop Remitter { get; set; } = new();
    public PremiumReceiverLoop Receiver { get; set; } = new();
    public List<OrganizationSummaryLoop> Organizations { get; set; } = [];

    public decimal TotalPremiumAmount => FinancialInformation.TotalPremiumAmount;
    public string PaymentMethod => FinancialInformation.PaymentMethodCode;
    public string PaymentDate => FinancialInformation.PaymentDate;

    public IEnumerable<PremiumDetailLoop> AllPremiumDetails =>
        Organizations.SelectMany(o => o.PremiumDetails);

    public override void RedactPhi()
    {
        base.RedactPhi();
        foreach (var org in Organizations)
            foreach (var detail in org.PremiumDetails)
                foreach (var r in detail.References.Where(r => r.ReferenceIdQualifier == "0F"))
                    r.ReferenceId = FullRedact(r.ReferenceId);
    }
}
