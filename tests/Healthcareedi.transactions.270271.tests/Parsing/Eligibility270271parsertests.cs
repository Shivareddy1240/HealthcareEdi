using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Eligibility270271.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.Eligibility270271.Tests.Parsing;

public class Eligibility270ParserTests
{
    private readonly string _sampleFile;
    private readonly Eligibility270271Parser _parser;

    public Eligibility270ParserTests()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_270.x12");
        _sampleFile = File.ReadAllText(path);
        _parser = new Eligibility270271Parser();
    }

    [Fact]
    public void Parse270_ReturnsOneTransaction()
    {
        var result = _parser.Parse270File(_sampleFile);

        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void Parse270_Envelope_ParsedCorrectly()
    {
        var result = _parser.Parse270File(_sampleFile);

        result.FunctionalGroupHeader!.FunctionalIdentifierCode.Should().Be("HS");
        result.InterchangeHeader!.SenderId.Should().Be("PROVIDER");
    }

    [Fact]
    public void Parse270_InformationSource_Payer()
    {
        var result = _parser.Parse270File(_sampleFile);
        var model = result.Transactions[0];

        model.PayerName.Should().Be("ACME HEALTH PLAN");
        model.InformationSource.SourceId.Should().Be("ACME001");
    }

    [Fact]
    public void Parse270_InformationReceiver_Provider()
    {
        var result = _parser.Parse270File(_sampleFile);
        var model = result.Transactions[0];

        model.ProviderName.Should().Be("SMITH, ROBERT");
        model.ProviderNpi.Should().Be("1234567890");
    }

    [Fact]
    public void Parse270_Subscriber_ParsedCorrectly()
    {
        var result = _parser.Parse270File(_sampleFile);
        var model = result.Transactions[0];

        model.Subscribers.Should().HaveCount(1);
        var sub = model.Subscribers[0];
        sub.SubscriberName.Should().Be("DOE, JOHN M");
        sub.MemberId.Should().Be("MBR123456");
        sub.DateOfBirth.Should().Be("19850315");
        sub.Gender.Should().Be("M");
    }

    [Fact]
    public void Parse270_Inquiries_ThreeServiceTypes()
    {
        var result = _parser.Parse270File(_sampleFile);
        var sub = result.Transactions[0].Subscribers[0];

        sub.Inquiries.Should().HaveCount(3);
        sub.Inquiries[0].ServiceTypeCode.Should().Be("30"); // Health Plan
        sub.Inquiries[1].ServiceTypeCode.Should().Be("1");  // Medical Care
        sub.Inquiries[2].ServiceTypeCode.Should().Be("35"); // Dental
    }

    [Fact]
    public void Parse270_InquiredServiceTypes_Convenience()
    {
        var result = _parser.Parse270File(_sampleFile);
        var model = result.Transactions[0];

        model.InquiredServiceTypes.Should().HaveCount(3);
        model.InquiredServiceTypes.Should().Contain("30");
    }

    [Fact]
    public void Parse270_PhiRedaction()
    {
        var result = _parser.Parse270File(_sampleFile);
        var model = result.Transactions[0];

        model.RedactPhi();

        model.Subscribers[0].Name.LastName.Should().StartWith("D").And.Contain("*");
        model.Subscribers[0].Name.IdCode.Should().Contain("*");
        model.Subscribers[0].Demographics!.DateOfBirth.Should().Be("********");
    }
}

public class Eligibility271ParserTests
{
    private readonly string _sampleFile;
    private readonly Eligibility270271Parser _parser;

    public Eligibility271ParserTests()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_271.x12");
        _sampleFile = File.ReadAllText(path);
        _parser = new Eligibility270271Parser();
    }

    [Fact]
    public void Parse271_ReturnsOneTransaction()
    {
        var result = _parser.Parse271File(_sampleFile);

        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void Parse271_Envelope_ParsedCorrectly()
    {
        var result = _parser.Parse271File(_sampleFile);

        result.FunctionalGroupHeader!.FunctionalIdentifierCode.Should().Be("HB");
    }

    // ── Information Source & Receiver ─────────────────────────────

    [Fact]
    public void Parse271_Payer_WithContact()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.PayerName.Should().Be("ACME HEALTH PLAN");
        model.InformationSource.Contact.Should().NotBeNull();
        model.InformationSource.Contact!.CommNumber1.Should().Be("8005559999");
    }

    [Fact]
    public void Parse271_Provider_WithNpi()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.ProviderNpi.Should().Be("1234567890");
    }

    // ── Subscriber ───────────────────────────────────────────────

    [Fact]
    public void Parse271_Subscriber_FullDetails()
    {
        var result = _parser.Parse271File(_sampleFile);
        var sub = result.Transactions[0].Subscribers[0];

        sub.SubscriberName.Should().Be("DOE, JOHN M");
        sub.MemberId.Should().Be("MBR123456");
        sub.Address!.AddressLine1.Should().Be("456 OAK STREET");
        sub.CityStateZip!.City.Should().Be("ANYTOWN");
        sub.DateOfBirth.Should().Be("19850315");
        sub.HasDependents.Should().BeTrue();
    }

    [Fact]
    public void Parse271_Subscriber_GroupReference()
    {
        var result = _parser.Parse271File(_sampleFile);
        var sub = result.Transactions[0].Subscribers[0];

        sub.GetReference("6P")!.ReferenceId.Should().Be("GRP001");
    }

    // ── Benefits: Active Coverage ────────────────────────────────

    [Fact]
    public void Parse271_HasActiveCoverage()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.HasActiveCoverage.Should().BeTrue();
    }

    [Fact]
    public void Parse271_ActiveCoverage_PlanDetails()
    {
        var result = _parser.Parse271File(_sampleFile);
        var active = result.Transactions[0].ActiveCoverageBenefits.First();

        active.BenefitDescription.Should().Be("Active Coverage");
        active.EligibilityBenefit.InsuranceTypeCode.Should().Be("HM");
        active.EligibilityBenefit.PlanCoverageDescription.Should().Be("GOLD PPO PLAN");
        active.PlanBeginDate.Should().Be("20230101");
        active.PlanEndDate.Should().Be("20231231");
    }

    [Fact]
    public void Parse271_ActiveCoverage_MessageText()
    {
        var result = _parser.Parse271File(_sampleFile);
        var active = result.Transactions[0].Subscribers[0].Benefits
            .First(b => b.IsActive);

        active.AllMessages.Should().Contain("MEMBER IS ELIGIBLE FOR BENEFITS");
    }

    // ── Benefits: Deductibles ────────────────────────────────────

    [Fact]
    public void Parse271_Deductibles_Parsed()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];
        var subDeductibles = model.Subscribers[0].Benefits.Where(b => b.EligibilityBenefit.IsDeductible).ToList();

        subDeductibles.Should().HaveCount(2); // Individual + Family

        var indDeductible = subDeductibles.First(d => d.CoverageLevel == "IND");
        indDeductible.Amount.Should().Be(500m);
        indDeductible.TimePeriod.Should().Be("Calendar Year");
        indDeductible.EligibilityBenefit.PlanCoverageDescription.Should().Be("CALENDAR YEAR DEDUCTIBLE");

        var famDeductible = subDeductibles.First(d => d.CoverageLevel == "FAM");
        famDeductible.Amount.Should().Be(1500m);
    }

    // ── Benefits: Copays ─────────────────────────────────────────

    [Fact]
    public void Parse271_Copays_Parsed()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];
        var copays = model.Copays.ToList();

        copays.Should().HaveCountGreaterOrEqualTo(2);

        var pcpCopay = copays.First(c => c.ServiceType == "98");
        pcpCopay.Amount.Should().Be(25m);
        pcpCopay.EligibilityBenefit.ServiceTypeDescription.Should().Be("Professional (Physician) Visit - Office");

        var erCopay = copays.First(c => c.ServiceType == "86");
        erCopay.Amount.Should().Be(150m);
        erCopay.EligibilityBenefit.ServiceTypeDescription.Should().Be("Emergency Services");
    }

    // ── Benefits: Coinsurance ────────────────────────────────────

    [Fact]
    public void Parse271_Coinsurance_InAndOutOfNetwork()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];
        var coins = model.Coinsurance.ToList();

        coins.Should().HaveCount(2);

        var inNetwork = coins.First(c => c.IsInNetwork);
        inNetwork.Percent.Should().Be(0.20m); // 20%

        var outOfNetwork = coins.First(c => c.IsOutOfNetwork);
        outOfNetwork.Percent.Should().Be(0.40m); // 40%
    }

    // ── Benefits: Out-of-Pocket Max ──────────────────────────────

    [Fact]
    public void Parse271_OutOfPocketMax_Parsed()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];
        var oopMax = model.OutOfPocketMaximums.ToList();

        oopMax.Should().HaveCount(2);

        var indOop = oopMax.First(o => o.CoverageLevel == "IND");
        indOop.Amount.Should().Be(6000m);

        var famOop = oopMax.First(o => o.CoverageLevel == "FAM");
        famOop.Amount.Should().Be(12000m);
    }

    // ── Benefits: Non-Covered ────────────────────────────────────

    [Fact]
    public void Parse271_NonCovered_DentalExcluded()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.NonCoveredServices.Should().HaveCount(1);
        var nc = model.NonCoveredServices.First();
        nc.ServiceType.Should().Be("35"); // Dental
        nc.EligibilityBenefit.ServiceTypeDescription.Should().Be("Dental Care");
    }

    [Fact]
    public void Parse271_NonCovered_MessageText()
    {
        var result = _parser.Parse271File(_sampleFile);
        var nc = result.Transactions[0].NonCoveredServices.First();

        nc.AllMessages.Should().Contain("DENTAL SERVICES ARE NOT COVERED");
    }

    // ── Benefits: Pharmacy Copays ────────────────────────────────

    [Fact]
    public void Parse271_PharmacyCopays_GenericAndBrand()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];
        var rxCopays = model.GetBenefitsByServiceType("88").Where(b => b.EligibilityBenefit.IsCopay).ToList();

        rxCopays.Should().HaveCount(2);
        rxCopays.Should().Contain(c => c.Amount == 10m); // Generic
        rxCopays.Should().Contain(c => c.Amount == 35m); // Brand
    }

    // ── Dependent ────────────────────────────────────────────────

    [Fact]
    public void Parse271_Dependent_ParsedCorrectly()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.Dependents.Should().HaveCount(1);
        var dep = model.Dependents[0];
        dep.DependentName.Should().Be("DOE, JANE A");
        dep.DateOfBirth.Should().Be("20150722");
        dep.Gender.Should().Be("F");
        dep.Relationship.Should().Be("Child");
    }

    [Fact]
    public void Parse271_Dependent_HasOwnBenefits()
    {
        var result = _parser.Parse271File(_sampleFile);
        var dep = result.Transactions[0].Dependents[0];

        dep.Benefits.Should().HaveCountGreaterOrEqualTo(2);
        dep.Benefits.Should().Contain(b => b.IsActive);
        dep.Benefits.Should().Contain(b => b.EligibilityBenefit.IsDeductible);
    }

    // ── Convenience Filters ──────────────────────────────────────

    [Fact]
    public void Parse271_InNetworkBenefits_Filtered()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.InNetworkBenefits.Should().HaveCountGreaterOrEqualTo(1);
        model.InNetworkBenefits.All(b => b.IsInNetwork).Should().BeTrue();
    }

    [Fact]
    public void Parse271_GetBenefitsByServiceType()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        var healthPlanBenefits = model.GetBenefitsByServiceType("30").ToList();
        healthPlanBenefits.Should().HaveCountGreaterOrEqualTo(4); // Active + deductibles + coinsurance + OOP
    }

    // ── PHI Redaction ────────────────────────────────────────────

    [Fact]
    public void Parse271_PhiRedaction()
    {
        var result = _parser.Parse271File(_sampleFile);
        var model = result.Transactions[0];

        model.RedactPhi();

        model.Subscribers[0].Name.LastName.Should().StartWith("D").And.Contain("*");
        model.Subscribers[0].Demographics!.DateOfBirth.Should().Be("********");
        model.Dependents[0].Name.LastName.Should().StartWith("D").And.Contain("*");
        model.Dependents[0].Demographics!.DateOfBirth.Should().Be("********");
    }

    // ── Validation ───────────────────────────────────────────────

    [Fact]
    public void Parse271_NoneMode_CollectsUnmapped()
    {
        var withUnknown = _sampleFile.Replace("SE*42*000000001", "ZZZ*TEST~\nSE*43*000000001");
        var parser = new Eligibility270271Parser(new ParserOptions { ValidationMode = ValidationMode.None });

        var result = parser.Parse271File(withUnknown);

        result.Transactions[0].UnmappedSegments.Should().Contain(s => s.StartsWith("ZZZ"));
    }
}
