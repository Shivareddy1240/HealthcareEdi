using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Claim837.Loops;

namespace HealthcareEdi.Transactions.Claim837.Models;

/// <summary>
/// Enum representing the 837 claim variant.
/// </summary>
public enum Claim837Variant
{
    Professional,   // 837P - SV1
    Institutional,  // 837I - SV2
    Dental          // 837D - SV3
}

/// <summary>
/// Base model shared by all 837 variants (P, I, D).
/// </summary>
public abstract class Claim837BaseModel : EdiTransactionBase
{
    public Claim837Variant Variant { get; set; }

    // ── Header Loops ────────────────────────────────────────────
    public SubmitterLoop Submitter { get; set; } = new();
    public ReceiverLoop Receiver { get; set; } = new();

    // ── Hierarchical Structure ──────────────────────────────────
    public BillingProviderLoop BillingProvider { get; set; } = new();
    public SubscriberLoop Subscriber { get; set; } = new();
    public PatientLoop? Patient { get; set; }  // Only when patient ≠ subscriber

    // ── Claims ──────────────────────────────────────────────────
    public List<ClaimLoop> Claims { get; set; } = [];

    // ── Convenience ─────────────────────────────────────────────
    public int ClaimCount => Claims.Count;
    public decimal TotalCharges => Claims.Sum(c => c.TotalCharge);
    public string? BillingProviderNpi => BillingProvider.Npi;

    /// <summary>Patient name (from Patient loop if present, else Subscriber).</summary>
    public string PatientName => Patient?.PatientName.FullName
        ?? Subscriber.SubscriberName.FullName;

    public override void RedactPhi()
    {
        base.RedactPhi();

        // Submitter
        Submitter.Name.LastName = MaskValue(Submitter.Name.LastName);
        Submitter.Name.FirstName = MaskValue(Submitter.Name.FirstName);

        // Subscriber
        Subscriber.SubscriberName.LastName = MaskValue(Subscriber.SubscriberName.LastName);
        Subscriber.SubscriberName.FirstName = MaskValue(Subscriber.SubscriberName.FirstName);
        Subscriber.SubscriberName.IdCode = MaskValue(Subscriber.SubscriberName.IdCode);
        if (Subscriber.SubscriberAddress != null)
        {
            Subscriber.SubscriberAddress.AddressLine1 = MaskValue(Subscriber.SubscriberAddress.AddressLine1);
            Subscriber.SubscriberAddress.AddressLine2 = MaskValue(Subscriber.SubscriberAddress.AddressLine2);
        }
        if (Subscriber.Demographics != null)
            Subscriber.Demographics.DateOfBirth = FullRedact(Subscriber.Demographics.DateOfBirth);

        // Patient (if separate from subscriber)
        if (Patient != null)
        {
            Patient.PatientName.LastName = MaskValue(Patient.PatientName.LastName);
            Patient.PatientName.FirstName = MaskValue(Patient.PatientName.FirstName);
            if (Patient.PatientAddress != null)
                Patient.PatientAddress.AddressLine1 = MaskValue(Patient.PatientAddress.AddressLine1);
            if (Patient.Demographics != null)
                Patient.Demographics.DateOfBirth = FullRedact(Patient.Demographics.DateOfBirth);
        }

        // Claims - redact account numbers
        foreach (var claim in Claims)
        {
            claim.ClaimInfo.PatientAccountNumber = MaskValue(claim.ClaimInfo.PatientAccountNumber);
        }
    }
}

/// <summary>
/// 837P - Professional Health Care Claim.
/// Implementation Guide: 005010X222A1.
/// </summary>
public class Claim837PModel : Claim837BaseModel
{
    public Claim837PModel() { Variant = Claim837Variant.Professional; }
}

/// <summary>
/// 837I - Institutional Health Care Claim.
/// Implementation Guide: 005010X223A2.
/// </summary>
public class Claim837IModel : Claim837BaseModel
{
    public Claim837IModel() { Variant = Claim837Variant.Institutional; }
}

/// <summary>
/// 837D - Dental Health Care Claim.
/// Implementation Guide: 005010X224A2.
/// </summary>
public class Claim837DModel : Claim837BaseModel
{
    public Claim837DModel() { Variant = Claim837Variant.Dental; }
}
