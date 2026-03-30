using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Eligibility270271.Loops;

namespace HealthcareEdi.Transactions.Eligibility270271.Models;

/// <summary>
/// Base class shared by 270 and 271 models.
/// </summary>
public abstract class EligibilityBaseModel : EdiTransactionBase
{
    /// <summary>Loop 2000A - Information Source (Payer).</summary>
    public InfoSourceLoop InformationSource { get; set; } = new();

    /// <summary>Loop 2000B - Information Receiver (Provider).</summary>
    public InfoReceiverLoop InformationReceiver { get; set; } = new();

    /// <summary>Loop 2000C - Subscribers.</summary>
    public List<EligibilitySubscriberLoop> Subscribers { get; set; } = [];

    /// <summary>Loop 2000D - Dependents.</summary>
    public List<EligibilityDependentLoop> Dependents { get; set; } = [];

    /// <summary>Transaction-level dates.</summary>
    public List<DtpSegment> TransactionDates { get; set; } = [];

    // ── Convenience ─────────────────────────────────────────────

    public string PayerName => InformationSource.SourceName;
    public string ProviderName => InformationReceiver.ReceiverName;
    public string? ProviderNpi => InformationReceiver.Npi;

    public override void RedactPhi()
    {
        base.RedactPhi();

        foreach (var sub in Subscribers)
        {
            sub.Name.LastName = MaskValue(sub.Name.LastName);
            sub.Name.FirstName = MaskValue(sub.Name.FirstName);
            sub.Name.IdCode = MaskValue(sub.Name.IdCode);
            if (sub.Address != null)
                sub.Address.AddressLine1 = MaskValue(sub.Address.AddressLine1);
            if (sub.Demographics != null)
                sub.Demographics.DateOfBirth = FullRedact(sub.Demographics.DateOfBirth);

            foreach (var r in sub.References.Where(r => r.ReferenceIdQualifier == "0F"))
                r.ReferenceId = FullRedact(r.ReferenceId);
        }

        foreach (var dep in Dependents)
        {
            dep.Name.LastName = MaskValue(dep.Name.LastName);
            dep.Name.FirstName = MaskValue(dep.Name.FirstName);
            dep.Name.IdCode = MaskValue(dep.Name.IdCode);
            if (dep.Address != null)
                dep.Address.AddressLine1 = MaskValue(dep.Address.AddressLine1);
            if (dep.Demographics != null)
                dep.Demographics.DateOfBirth = FullRedact(dep.Demographics.DateOfBirth);
        }
    }
}

/// <summary>
/// 270 - Health Care Eligibility Benefit Inquiry.
/// Implementation Guide: 005010X279A1.
/// Sent by provider to payer to check patient eligibility.
/// </summary>
public class Eligibility270Model : EligibilityBaseModel
{
    /// <summary>All service type codes being inquired about (from EQ segments).</summary>
    public IEnumerable<string> InquiredServiceTypes =>
        Subscribers.SelectMany(s => s.Inquiries.Select(eq => eq.ServiceTypeCode))
        .Concat(Dependents.SelectMany(d => d.Inquiries.Select(eq => eq.ServiceTypeCode)))
        .Where(s => !string.IsNullOrEmpty(s))
        .Distinct();
}

/// <summary>
/// 271 - Health Care Eligibility Benefit Response.
/// Implementation Guide: 005010X279A1.
/// Payer response with benefit/eligibility details.
/// </summary>
public class Eligibility271Model : EligibilityBaseModel
{
    // ── Convenience for 271-specific queries ─────────────────────

    /// <summary>All benefit records across all subscribers and dependents.</summary>
    public IEnumerable<BenefitLoop> AllBenefits =>
        Subscribers.SelectMany(s => s.Benefits)
        .Concat(Dependents.SelectMany(d => d.Benefits));

    /// <summary>True if any subscriber/dependent has active coverage (EB01=1).</summary>
    public bool HasActiveCoverage => AllBenefits.Any(b => b.IsActive);

    /// <summary>Active coverage benefits only.</summary>
    public IEnumerable<BenefitLoop> ActiveCoverageBenefits => AllBenefits.Where(b => b.IsActive);

    /// <summary>Deductible benefits (EB01=C).</summary>
    public IEnumerable<BenefitLoop> Deductibles => AllBenefits.Where(b => b.EligibilityBenefit.IsDeductible);

    /// <summary>Copay benefits (EB01=B).</summary>
    public IEnumerable<BenefitLoop> Copays => AllBenefits.Where(b => b.EligibilityBenefit.IsCopay);

    /// <summary>Coinsurance benefits (EB01=A).</summary>
    public IEnumerable<BenefitLoop> Coinsurance => AllBenefits.Where(b => b.EligibilityBenefit.IsCoinsurance);

    /// <summary>Out-of-pocket / stop loss benefits (EB01=G).</summary>
    public IEnumerable<BenefitLoop> OutOfPocketMaximums => AllBenefits.Where(b => b.EligibilityBenefit.IsOutOfPocket);

    /// <summary>Non-covered services (EB01=I).</summary>
    public IEnumerable<BenefitLoop> NonCoveredServices => AllBenefits.Where(b => b.EligibilityBenefit.IsNonCovered);

    /// <summary>Gets all benefits for a specific service type code.</summary>
    public IEnumerable<BenefitLoop> GetBenefitsByServiceType(string serviceTypeCode)
        => AllBenefits.Where(b => b.ServiceType == serviceTypeCode);

    /// <summary>Gets all in-network benefits.</summary>
    public IEnumerable<BenefitLoop> InNetworkBenefits => AllBenefits.Where(b => b.IsInNetwork);

    /// <summary>Gets all out-of-network benefits.</summary>
    public IEnumerable<BenefitLoop> OutOfNetworkBenefits => AllBenefits.Where(b => b.IsOutOfNetwork);
}
