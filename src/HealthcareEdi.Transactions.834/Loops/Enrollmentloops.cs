using HealthcareEdi.Core.Attributes;
using HealthcareEdi.Core.Envelopes;
using HealthcareEdi.Core.Segments;
using HealthcareEdi.Transactions.Enrollment834.Segments;

namespace HealthcareEdi.Transactions.Enrollment834.Loops;

/// <summary>Loop 1000A - Sponsor Name (Employer/TPA)</summary>
[EdiLoop("1000A")]
public class SponsorLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }

    public string SponsorName => Name.FullName;
    public string? SponsorId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

/// <summary>Loop 1000B - Payer (Insurance Carrier)</summary>
[EdiLoop("1000B")]
public class EnrollmentPayerLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }

    public string PayerName => Name.FullName;
    public string? PayerId => !string.IsNullOrEmpty(Name.IdCode) ? Name.IdCode : null;
}

/// <summary>Loop 2000 - Member Level Detail. The primary member record.</summary>
[EdiLoop("2000")]
public class MemberLoop
{
    /// <summary>INS - The anchor segment defining the enrollment action.</summary>
    public InsSegment InsuredBenefit { get; set; } = new();

    /// <summary>REF segments at member level (SSN, Group#, Policy#, etc.).</summary>
    public List<RefSegment> References { get; set; } = [];

    /// <summary>DTP segments at member level (coverage dates, hire date, etc.).</summary>
    public List<DtpSegment> Dates { get; set; } = [];

    // ── Loop 2100A - Member Name ────────────────────────────────
    public Nm1Segment MemberName { get; set; } = new();
    public N3Segment? MemberAddress { get; set; }
    public N4Segment? MemberCityStateZip { get; set; }
    public DmgSegment? Demographics { get; set; }
    public PerSegment? ContactInfo { get; set; }
    public List<RefSegment> MemberReferences { get; set; } = [];

    // ── Loop 2100C-G - Additional Names (Mailing, Employer, School, etc.) ──
    public List<AdditionalNameLoop> AdditionalNames { get; set; } = [];

    // ── Loop 2300 - Health Coverage ─────────────────────────────
    public List<HealthCoverageLoop> HealthCoverages { get; set; } = [];

    // ── Loop 2700/2750 - Reporting Categories ───────────────────
    public List<ReportingCategoryLoop> ReportingCategories { get; set; } = [];

    // ── Employment Info ─────────────────────────────────────────
    public EcSegment? EmploymentClass { get; set; }
    public IcmSegment? Income { get; set; }

    // ── Convenience Properties ──────────────────────────────────

    public bool IsSubscriber => InsuredBenefit.IsSubscriber;
    public bool IsDependent => !InsuredBenefit.IsSubscriber;
    public string MaintenanceAction => InsuredBenefit.MaintenanceAction;
    public string MaintenanceTypeCode => InsuredBenefit.MaintenanceTypeCode;
    public string Relationship => InsuredBenefit.Relationship;
    public string FullName => MemberName.FullName;

    /// <summary>SSN from REF*0F.</summary>
    public string? Ssn => GetReference("0F")?.ReferenceId;

    /// <summary>Member Policy Number from REF*1L.</summary>
    public string? PolicyNumber => GetReference("1L")?.ReferenceId;

    /// <summary>Subscriber ID from REF*0F or NM109.</summary>
    public string? SubscriberId => !string.IsNullOrEmpty(MemberName.IdCode)
        ? MemberName.IdCode
        : Ssn;

    /// <summary>Group/Policy number from REF*1L.</summary>
    public string? GroupNumber => GetReference("1L")?.ReferenceId;

    /// <summary>Date of birth from demographics.</summary>
    public string? DateOfBirth => Demographics?.DateOfBirth;

    /// <summary>Gender code from demographics.</summary>
    public string? Gender => Demographics?.GenderCode;

    /// <summary>Gets REF by qualifier.</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier)
           ?? MemberReferences.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);

    /// <summary>Gets DTP by qualifier.</summary>
    public DtpSegment? GetDate(string qualifier)
        => Dates.FirstOrDefault(d => d.DateTimeQualifier == qualifier);

    /// <summary>Hire date from DTP*336.</summary>
    public string? HireDate => GetDate("336")?.DateTimePeriod;

    /// <summary>Coverage effective date from first HD coverage DTP*348.</summary>
    public string? CoverageEffectiveDate => HealthCoverages
        .SelectMany(hc => hc.Dates)
        .FirstOrDefault(d => d.DateTimeQualifier == "348")?.DateTimePeriod;
}

/// <summary>Loop 2100C-G - Additional Member Name/Address (Mailing, Employer, School, Custodial Parent)</summary>
[EdiLoop("2100")]
public class AdditionalNameLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public DmgSegment? Demographics { get; set; }
    public List<RefSegment> References { get; set; } = [];

    /// <summary>Entity code determines the role: 31=Mailing, 36=Employer, S3=School, 6Y=Case Manager, etc.</summary>
    public string EntityType => Name.EntityIdentifierCode;
}

/// <summary>Loop 2300 - Health Coverage (one per benefit plan for this member)</summary>
[EdiLoop("2300")]
public class HealthCoverageLoop
{
    public HdSegment HealthCoverage { get; set; } = new();
    public List<DtpSegment> Dates { get; set; } = [];
    public List<RefSegment> References { get; set; } = [];
    public List<IdcSegment> IdentificationCards { get; set; } = [];

    // Loop 2310 - Provider Info nested under coverage
    public List<CoverageProviderLoop> Providers { get; set; } = [];

    // ── Convenience ─────────────────────────────────────────────

    public string InsuranceType => HealthCoverage.InsuranceType;
    public string InsuranceLineCode => HealthCoverage.InsuranceLineCode;
    public string PlanName => HealthCoverage.PlanCoverageDescription;
    public string CoverageLevel => HealthCoverage.CoverageLevel;
    public string MaintenanceTypeCode => HealthCoverage.MaintenanceTypeCode;

    /// <summary>Benefit begin date (DTP*348).</summary>
    public string? EffectiveDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "348")?.DateTimePeriod;

    /// <summary>Benefit end date (DTP*349).</summary>
    public string? TerminationDate => Dates.FirstOrDefault(d => d.DateTimeQualifier == "349")?.DateTimePeriod;

    /// <summary>Gets REF by qualifier.</summary>
    public RefSegment? GetReference(string qualifier)
        => References.FirstOrDefault(r => r.ReferenceIdQualifier == qualifier);
}

/// <summary>Loop 2310 - Provider Information within a coverage</summary>
[EdiLoop("2310")]
public class CoverageProviderLoop
{
    public Nm1Segment Name { get; set; } = new();
    public N3Segment? Address { get; set; }
    public N4Segment? CityStateZip { get; set; }
    public List<RefSegment> References { get; set; } = [];

    public string? Npi => Name.IdCodeQualifier == "XX" ? Name.IdCode : null;
}

/// <summary>Loop 2700 - Additional Reporting Categories</summary>
[EdiLoop("2700")]
public class ReportingCategoryLoop
{
    public int LineNumber { get; set; }  // LX01

    // Loop 2750 - Reporting Category Detail
    public List<ReportingDetailLoop> Details { get; set; } = [];
}

/// <summary>Loop 2750 - Reporting Category Detail (employer-defined custom fields)</summary>
[EdiLoop("2750")]
public class ReportingDetailLoop
{
    /// <summary>N1 entity code and name for the reporting category.</summary>
    public string CategoryCode { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>REF segments carrying the actual values.</summary>
    public List<RefSegment> References { get; set; } = [];

    /// <summary>DTP segments for date-based reporting values.</summary>
    public List<DtpSegment> Dates { get; set; } = [];

    /// <summary>First REF value (most common access pattern for single-value categories).</summary>
    public string? Value => References.FirstOrDefault()?.ReferenceId;
}
