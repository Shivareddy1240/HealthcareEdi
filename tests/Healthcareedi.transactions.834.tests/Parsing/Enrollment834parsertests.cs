using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Enrollment834.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.Enrollment834.Tests.Parsing;

public class Enrollment834ParserTests
{
    private readonly string _sampleFile;
    private readonly Enrollment834Parser _parser;

    public Enrollment834ParserTests()
    {
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_834.x12");
        _sampleFile = File.ReadAllText(samplePath);
        _parser = new Enrollment834Parser();
    }

    // ── Basic Parsing ────────────────────────────────────────────

    [Fact]
    public void ParseFile_Sample834_ReturnsOneTransaction()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void ParseFile_Envelope_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.InterchangeHeader!.SenderId.Should().Be("EMPLOYER");
        result.FunctionalGroupHeader!.FunctionalIdentifierCode.Should().Be("BE");
        result.FunctionalGroupHeader.VersionReleaseCode.Should().Be("005010X220A1");
    }

    // ── Transaction Header ───────────────────────────────────────

    [Fact]
    public void ParseFile_TransactionReferences_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.GetTransactionReference("38")!.ReferenceId.Should().Be("GROUPNUM001");
        model.GetTransactionReference("BGN")!.ReferenceId.Should().Be("ENROLLBATCH2023080100");
    }

    [Fact]
    public void ParseFile_FileEffectiveDate_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.FileEffectiveDate.Should().Be("20230801");
    }

    // ── Sponsor (1000A) ──────────────────────────────────────────

    [Fact]
    public void ParseFile_Sponsor_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Sponsor.SponsorName.Should().Be("ACME CORPORATION");
        model.Sponsor.SponsorId.Should().Be("111222333");
        model.Sponsor.Address!.AddressLine1.Should().Be("500 CORPORATE PLAZA");
        model.Sponsor.CityStateZip!.City.Should().Be("BIGCITY");
        model.Sponsor.CityStateZip.StateCode.Should().Be("TX");
    }

    // ── Payer (1000B) ────────────────────────────────────────────

    [Fact]
    public void ParseFile_Payer_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Payer.PayerName.Should().Be("UNITED HEALTH PLANS");
        model.Payer.PayerId.Should().Be("444555666");
    }

    // ── Members ──────────────────────────────────────────────────

    [Fact]
    public void ParseFile_Members_ThreeMembersParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.MemberCount.Should().Be(3);
    }

    // ── Member 1: Subscriber Addition ────────────────────────────

    [Fact]
    public void ParseFile_Member1_SubscriberAddition()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.IsSubscriber.Should().BeTrue();
        member.IsDependent.Should().BeFalse();
        member.InsuredBenefit.IsAdd.Should().BeTrue();
        member.MaintenanceAction.Should().Be("Addition");
        member.Relationship.Should().Be("Self");
    }

    [Fact]
    public void ParseFile_Member1_NameAndDemographics()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.FullName.Should().Be("JOHNSON, MICHAEL R");
        member.DateOfBirth.Should().Be("19850622");
        member.Gender.Should().Be("M");
        member.SubscriberId.Should().Be("MBR0001");
    }

    [Fact]
    public void ParseFile_Member1_Address()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.MemberAddress!.AddressLine1.Should().Be("789 ELM STREET");
        member.MemberAddress.AddressLine2.Should().Be("APT 4B");
        member.MemberCityStateZip!.City.Should().Be("ANYTOWN");
        member.MemberCityStateZip.StateCode.Should().Be("TX");
        member.MemberCityStateZip.PostalCode.Should().Be("75001");
    }

    [Fact]
    public void ParseFile_Member1_SsnAndReferences()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.Ssn.Should().Be("123456789");
        member.GroupNumber.Should().Be("GROUPNUM001");
    }

    [Fact]
    public void ParseFile_Member1_ContactInfo()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.ContactInfo.Should().NotBeNull();
        member.ContactInfo!.ContactName.Should().Be("MICHAEL JOHNSON");
        member.ContactInfo.CommQualifier1.Should().Be("TE");
        member.ContactInfo.CommNumber1.Should().Be("2145551234");
        member.ContactInfo.CommQualifier2.Should().Be("EM");
        member.ContactInfo.CommNumber2.Should().Be("mjohnson@email.com");
    }

    [Fact]
    public void ParseFile_Member1_HireDate()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.HireDate.Should().Be("20200115");
    }

    [Fact]
    public void ParseFile_Member1_EmploymentInfo()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.EmploymentClass.Should().NotBeNull();
        member.EmploymentClass!.EmploymentClassCode1.Should().Be("01");
        member.Income.Should().NotBeNull();
        member.Income!.WageAmount.Should().Be(75000m);
        member.Income.FrequencyCode.Should().Be("6"); // Annual
    }

    // ── Health Coverage ──────────────────────────────────────────

    [Fact]
    public void ParseFile_Member1_ThreeCoverages()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.HealthCoverages.Should().HaveCount(3);
    }

    [Fact]
    public void ParseFile_Member1_HealthCoverage()
    {
        var result = _parser.ParseFile(_sampleFile);
        var hlt = result.Transactions[0].Members[0].HealthCoverages[0];

        hlt.InsuranceType.Should().Be("Health");
        hlt.InsuranceLineCode.Should().Be("HLT");
        hlt.PlanName.Should().Be("PLAN-GOLD-2023");
        hlt.CoverageLevel.Should().Be("Family");
        hlt.EffectiveDate.Should().Be("20230801");
        hlt.TerminationDate.Should().Be("20241231");
    }

    [Fact]
    public void ParseFile_Member1_DentalCoverage()
    {
        var result = _parser.ParseFile(_sampleFile);
        var den = result.Transactions[0].Members[0].HealthCoverages[1];

        den.InsuranceType.Should().Be("Dental");
        den.PlanName.Should().Be("PLAN-DENTAL-STD");
        den.CoverageLevel.Should().Be("Family");
        den.EffectiveDate.Should().Be("20230801");
    }

    [Fact]
    public void ParseFile_Member1_VisionCoverage()
    {
        var result = _parser.ParseFile(_sampleFile);
        var vis = result.Transactions[0].Members[0].HealthCoverages[2];

        vis.InsuranceType.Should().Be("Vision");
        vis.CoverageLevel.Should().Be("Employee Only");
    }

    [Fact]
    public void ParseFile_Member1_IdCard()
    {
        var result = _parser.ParseFile(_sampleFile);
        var hlt = result.Transactions[0].Members[0].HealthCoverages[0];

        hlt.IdentificationCards.Should().HaveCount(1);
        hlt.IdentificationCards[0].IdentificationCardTypeCode.Should().Be("H");
    }

    // ── Reporting Categories (Loop 2700/2750) ────────────────────

    [Fact]
    public void ParseFile_Member1_ReportingCategories()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[0];

        member.ReportingCategories.Should().HaveCount(2);

        var cat1 = member.ReportingCategories[0];
        cat1.LineNumber.Should().Be(1);
        cat1.Details.Should().HaveCount(1);
        cat1.Details[0].CategoryName.Should().Be("BENEFIT CLASS");
        cat1.Details[0].References.Should().HaveCount(2);
        cat1.Details[0].Value.Should().Be("CLASS-A");

        var cat2 = member.ReportingCategories[1];
        cat2.LineNumber.Should().Be(2);
        cat2.Details[0].CategoryName.Should().Be("DEPARTMENT");
        cat2.Details[0].Value.Should().Be("DEPT-ENGINEERING");
    }

    // ── Member 2: Dependent (Spouse) ─────────────────────────────

    [Fact]
    public void ParseFile_Member2_DependentSpouse()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[1];

        member.IsSubscriber.Should().BeFalse();
        member.IsDependent.Should().BeTrue();
        member.Relationship.Should().Be("Spouse");
        member.FullName.Should().Be("JOHNSON, SARAH L");
        member.Gender.Should().Be("F");
        member.Ssn.Should().Be("987654321");
    }

    [Fact]
    public void ParseFile_Member2_InheritsCoverages()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[1];

        member.HealthCoverages.Should().HaveCount(2); // HLT + DEN (no vision for dependent)
    }

    // ── Member 3: Termination ────────────────────────────────────

    [Fact]
    public void ParseFile_Member3_Termination()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[2];

        member.IsSubscriber.Should().BeTrue();
        member.InsuredBenefit.IsTermination.Should().BeTrue();
        member.MaintenanceAction.Should().Be("Cancellation/Termination");
        member.FullName.Should().Be("WILLIAMS, DAVID");
    }

    [Fact]
    public void ParseFile_Member3_TerminationCoverageWithEndDate()
    {
        var result = _parser.ParseFile(_sampleFile);
        var member = result.Transactions[0].Members[2];

        member.HealthCoverages.Should().HaveCount(1);
        var coverage = member.HealthCoverages[0];
        coverage.MaintenanceTypeCode.Should().Be("024"); // Cancellation
        coverage.EffectiveDate.Should().Be("20220101");
        coverage.TerminationDate.Should().Be("20230731");
    }

    // ── Convenience Filters ──────────────────────────────────────

    [Fact]
    public void ParseFile_AdditionsFilter()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Additions.Should().HaveCount(2); // Member 1 + Member 2
    }

    [Fact]
    public void ParseFile_TerminationsFilter()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Terminations.Should().HaveCount(1);
        model.Terminations.First().FullName.Should().Be("WILLIAMS, DAVID");
    }

    [Fact]
    public void ParseFile_SubscribersFilter()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Subscribers.Should().HaveCount(2); // JOHNSON and WILLIAMS
        model.Dependents.Should().HaveCount(1);  // SARAH
    }

    // ── PHI Redaction ────────────────────────────────────────────

    [Fact]
    public void RedactPhi_MasksMemberIdentifiers()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.RedactPhi();

        var member = model.Members[0];
        member.MemberName.LastName.Should().StartWith("J").And.Contain("*");
        member.MemberName.FirstName.Should().StartWith("M").And.Contain("*");
        member.MemberName.IdCode.Should().Contain("*");
        member.MemberAddress!.AddressLine1.Should().Contain("*");
        member.Demographics!.DateOfBirth.Should().Be("********");

        // SSN fully redacted
        member.References.First(r => r.ReferenceIdQualifier == "0F").ReferenceId.Should().Be("*********");

        // Contact info masked
        member.ContactInfo!.ContactName.Should().Contain("*");
    }

    // ── Validation Modes ─────────────────────────────────────────

    [Fact]
    public void ParseFile_NoneMode_CollectsUnmapped()
    {
        var withUnknown = _sampleFile.Replace("SE*55*000000001", "ZZZ*UNKNOWN~\nSE*56*000000001");
        var parser = new Enrollment834Parser(new ParserOptions { ValidationMode = ValidationMode.None });

        var result = parser.ParseFile(withUnknown);

        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].UnmappedSegments.Should().Contain(s => s.StartsWith("ZZZ"));
    }
}
