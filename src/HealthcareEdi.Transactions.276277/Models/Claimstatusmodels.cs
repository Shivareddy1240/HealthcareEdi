using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Transactions.ClaimStatus276277.Loops;

namespace HealthcareEdi.Transactions.ClaimStatus276277.Models;

public abstract class ClaimStatusBaseModel : EdiTransactionBase
{
    public ClaimStatusPayerLoop Payer { get; set; } = new();
    public ClaimStatusProviderLoop Provider { get; set; } = new();
    public List<ClaimStatusSubscriberLoop> Subscribers { get; set; } = [];
    public List<ClaimStatusPatientLoop> Patients { get; set; } = [];

    /// <summary>All claim status details across subscribers and patients.</summary>
    public IEnumerable<ClaimStatusDetailLoop> AllClaimStatuses =>
        Subscribers.SelectMany(s => s.ClaimStatuses)
        .Concat(Patients.SelectMany(p => p.ClaimStatuses));

    public override void RedactPhi()
    {
        base.RedactPhi();
        foreach (var sub in Subscribers)
        {
            sub.Name.LastName = MaskValue(sub.Name.LastName);
            sub.Name.FirstName = MaskValue(sub.Name.FirstName);
            sub.Name.IdCode = MaskValue(sub.Name.IdCode);
            if (sub.Demographics != null)
                sub.Demographics.DateOfBirth = FullRedact(sub.Demographics.DateOfBirth);
        }
        foreach (var pat in Patients)
        {
            pat.Name.LastName = MaskValue(pat.Name.LastName);
            pat.Name.FirstName = MaskValue(pat.Name.FirstName);
            if (pat.Demographics != null)
                pat.Demographics.DateOfBirth = FullRedact(pat.Demographics.DateOfBirth);
        }
    }
}

/// <summary>276 - Claim Status Inquiry. Implementation Guide: 005010X212.</summary>
public class ClaimStatus276Model : ClaimStatusBaseModel { }

/// <summary>277 - Claim Status Response. Implementation Guide: 005010X212.</summary>
public class ClaimStatus277Model : ClaimStatusBaseModel
{
    public IEnumerable<ClaimStatusDetailLoop> FinalizedClaims => AllClaimStatuses.Where(c => c.IsFinalized);
    public IEnumerable<ClaimStatusDetailLoop> PendingClaims => AllClaimStatuses.Where(c => c.IsPending);
    public IEnumerable<ClaimStatusDetailLoop> DeniedClaims => AllClaimStatuses.Where(c => c.IsDenied);
    public IEnumerable<ClaimStatusDetailLoop> PaidClaims => AllClaimStatuses.Where(c => c.IsPaid);
}
