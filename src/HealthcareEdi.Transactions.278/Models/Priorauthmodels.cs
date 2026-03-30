namespace HealthcareEdi.Transactions.PriorAuth278.Models;
using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Transactions.PriorAuth278.Loops;


public abstract class PriorAuth278BaseModel : EdiTransactionBase
{
    public ReviewPayerLoop Payer { get; set; } = new();
    public ReviewProviderLoop Provider { get; set; } = new();
    public ReviewSubscriberLoop? Subscriber { get; set; }
    public ReviewPatientLoop? Patient { get; set; }
    public List<ServiceReviewLoop> ServiceReviews { get; set; } = [];

    public string PatientName => Patient?.PatientName ?? Subscriber?.SubscriberName ?? "";

    public override void RedactPhi()
    {
        base.RedactPhi();
        if (Subscriber != null)
        {
            Subscriber.Name.LastName = MaskValue(Subscriber.Name.LastName);
            Subscriber.Name.FirstName = MaskValue(Subscriber.Name.FirstName);
            Subscriber.Name.IdCode = MaskValue(Subscriber.Name.IdCode);
            if (Subscriber.Demographics != null) Subscriber.Demographics.DateOfBirth = FullRedact(Subscriber.Demographics.DateOfBirth);
        }
        if (Patient != null)
        {
            Patient.Name.LastName = MaskValue(Patient.Name.LastName);
            Patient.Name.FirstName = MaskValue(Patient.Name.FirstName);
            if (Patient.Demographics != null) Patient.Demographics.DateOfBirth = FullRedact(Patient.Demographics.DateOfBirth);
        }
    }
}

/// <summary>278 Request - Prior Authorization Request.</summary>
public class PriorAuth278RequestModel : PriorAuth278BaseModel { }

/// <summary>278 Response - Prior Authorization Response with decisions.</summary>
public class PriorAuth278ResponseModel : PriorAuth278BaseModel
{
    public IEnumerable<ServiceReviewLoop> CertifiedReviews => ServiceReviews.Where(r => r.IsCertified);
    public IEnumerable<ServiceReviewLoop> DeniedReviews => ServiceReviews.Where(r => r.IsDenied);
}
