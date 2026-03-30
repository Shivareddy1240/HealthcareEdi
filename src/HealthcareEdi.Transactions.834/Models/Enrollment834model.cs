using HealthcareEdi.Core.Models.Base;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Enrollment834.Loops;

namespace HealthcareEdi.Transactions.Enrollment834.Models;

/// <summary>
/// 834 - Benefit Enrollment and Maintenance.
/// Implementation Guide: 005010X220A1.
/// Used for employer/TPA → payer enrollment, disenrollment, and maintenance.
/// </summary>
public class Enrollment834Model : EdiTransactionBase
{
    // ── Transaction Header ──────────────────────────────────────
    /// <summary>REF segments at transaction level.</summary>
    public List<RefSegment> TransactionReferences { get; set; } = [];

    /// <summary>DTP segments at transaction level.</summary>
    public List<DtpSegment> TransactionDates { get; set; } = [];

    // ── Header Loops ────────────────────────────────────────────
    /// <summary>Loop 1000A - Sponsor (Employer/TPA).</summary>
    public SponsorLoop Sponsor { get; set; } = new();

    /// <summary>Loop 1000B - Payer (Insurance Carrier).</summary>
    public EnrollmentPayerLoop Payer { get; set; } = new();

    // ── Members ─────────────────────────────────────────────────
    /// <summary>Loop 2000 - All member records in this enrollment file.</summary>
    public List<MemberLoop> Members { get; set; } = [];

    // ── Convenience Properties ──────────────────────────────────

    /// <summary>Total number of members in this transaction.</summary>
    public int MemberCount => Members.Count;

    /// <summary>Members being added (INS03=021).</summary>
    public IEnumerable<MemberLoop> Additions => Members.Where(m => m.InsuredBenefit.IsAdd);

    /// <summary>Members being terminated (INS03=024).</summary>
    public IEnumerable<MemberLoop> Terminations => Members.Where(m => m.InsuredBenefit.IsTermination);

    /// <summary>Members being changed (INS03=001).</summary>
    public IEnumerable<MemberLoop> Changes => Members.Where(m => m.InsuredBenefit.IsChange);

    /// <summary>Members being reinstated (INS03=025).</summary>
    public IEnumerable<MemberLoop> Reinstatements => Members.Where(m => m.InsuredBenefit.IsReinstatement);

    /// <summary>Only subscribers (INS01=Y), excluding dependents.</summary>
    public IEnumerable<MemberLoop> Subscribers => Members.Where(m => m.IsSubscriber);

    /// <summary>Only dependents (INS01=N).</summary>
    public IEnumerable<MemberLoop> Dependents => Members.Where(m => m.IsDependent);

    /// <summary>Gets a transaction-level REF by qualifier.</summary>
    public RefSegment? GetTransactionReference(string qualifier)
        => TransactionReferences.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);

    /// <summary>File effective date from DTP*007 at transaction level.</summary>
    public string? FileEffectiveDate => TransactionDates
        .FirstOrDefault(d => d.DateTimeQualifier == "007")?.DateTimePeriod;

    // ── PHI Redaction ───────────────────────────────────────────
    public override void RedactPhi()
    {
        base.RedactPhi();

        foreach (var member in Members)
        {
            // Names
            member.MemberName.LastName = MaskValue(member.MemberName.LastName);
            member.MemberName.FirstName = MaskValue(member.MemberName.FirstName);
            member.MemberName.MiddleName = MaskValue(member.MemberName.MiddleName);
            member.MemberName.IdCode = MaskValue(member.MemberName.IdCode);

            // Address
            if (member.MemberAddress != null)
            {
                member.MemberAddress.AddressLine1 = MaskValue(member.MemberAddress.AddressLine1);
                member.MemberAddress.AddressLine2 = MaskValue(member.MemberAddress.AddressLine2);
            }

            // Demographics
            if (member.Demographics != null)
                member.Demographics.DateOfBirth = FullRedact(member.Demographics.DateOfBirth);

            // SSN in references (REF*0F)
            foreach (var r in member.References.Where(r => r.ReferenceIdQualifier == "0F"))
                r.ReferenceId = FullRedact(r.ReferenceId);
            foreach (var r in member.MemberReferences.Where(r => r.ReferenceIdQualifier == "0F"))
                r.ReferenceId = FullRedact(r.ReferenceId);

            // Additional names
            foreach (var addl in member.AdditionalNames)
            {
                addl.Name.LastName = MaskValue(addl.Name.LastName);
                addl.Name.FirstName = MaskValue(addl.Name.FirstName);
                if (addl.Address != null)
                    addl.Address.AddressLine1 = MaskValue(addl.Address.AddressLine1);
            }

            // Contact info
            if (member.ContactInfo != null)
                member.ContactInfo.ContactName = MaskValue(member.ContactInfo.ContactName);
        }
    }
}
