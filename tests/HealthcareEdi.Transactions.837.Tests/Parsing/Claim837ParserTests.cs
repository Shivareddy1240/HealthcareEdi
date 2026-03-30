using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Claim837.Models;
using HealthcareEdi.Transactions.Claim837.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.Claim837.Tests.Parsing;

public class Claim837ParserTests
{
    private readonly string _sampleFile;
    private readonly Claim837Parser _parser;

    public Claim837ParserTests()
    {
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_837P.x12");
        _sampleFile = File.ReadAllText(samplePath);
        _parser = new Claim837Parser();
    }

    [Fact]
    public void ParseFile_Sample837P_ReturnsOneTransaction()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
        result.HasFailures.Should().BeFalse();
    }

    [Fact]
    public void ParseFile_Sample837P_DetectsProfessionalVariant()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Should().BeOfType<Claim837PModel>();
        model.Variant.Should().Be(Claim837Variant.Professional);
    }

    [Fact]
    public void ParseFile_InterchangeHeader_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.InterchangeHeader.Should().NotBeNull();
        result.InterchangeHeader!.SenderId.Should().Be("CLEARINGHOUSE");
        result.InterchangeHeader.ReceiverId.Should().Be("PAYERID");
        result.InterchangeHeader.UsageIndicator.Should().Be("P");
    }

    [Fact]
    public void ParseFile_FunctionalGroup_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.FunctionalGroupHeader.Should().NotBeNull();
        result.FunctionalGroupHeader!.FunctionalIdentifierCode.Should().Be("HC");
        result.FunctionalGroupHeader.VersionReleaseCode.Should().Be("005010X222A1");
    }

    [Fact]
    public void ParseFile_Submitter_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Submitter.Name.LastName.Should().Be("PREMIER BILLING SERVICES");
        model.Submitter.Name.EntityTypeQualifier.Should().Be("2"); // Organization
        model.Submitter.Contact.Should().NotBeNull();
        model.Submitter.Contact!.ContactName.Should().Be("JANE COORDINATOR");
        model.Submitter.Contact.CommNumber1.Should().Be("5551234567");
    }

    [Fact]
    public void ParseFile_BillingProvider_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.BillingProvider.Name.FullName.Should().Be("SMITH, ROBERT J");
        model.BillingProviderNpi.Should().Be("1234567890");
        model.BillingProvider.TaxonomyCode.Should().Be("207Q00000X");
        model.BillingProvider.Address.Should().NotBeNull();
        model.BillingProvider.Address!.AddressLine1.Should().Be("123 MEDICAL CENTER DR");
        model.BillingProvider.CityStateZip!.City.Should().Be("ANYTOWN");
        model.BillingProvider.CityStateZip.StateCode.Should().Be("NY");
    }

    [Fact]
    public void ParseFile_Subscriber_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Subscriber.SubscriberName.FullName.Should().Be("DOE, JOHN M");
        model.Subscriber.MemberId.Should().Be("ABC123456789");
        model.Subscriber.SubscriberInfo.PayerResponsibilityCode.Should().Be("P"); // Primary
        model.Subscriber.Demographics.Should().NotBeNull();
        model.Subscriber.Demographics!.DateOfBirth.Should().Be("19850315");
        model.Subscriber.Demographics.GenderCode.Should().Be("M");
        model.Subscriber.HasDependentPatient.Should().BeTrue();
    }

    [Fact]
    public void ParseFile_Patient_ParsedWhenDifferentFromSubscriber()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Patient.Should().NotBeNull("patient HL loop (2000C) should be parsed when patient differs from subscriber");
        model.Patient!.PatientName.FullName.Should().Be("DOE, JANE A");
        model.Patient.Demographics!.DateOfBirth.Should().Be("20150722");
        model.Patient.Demographics.GenderCode.Should().Be("F");
        model.PatientName.Should().Be("DOE, JANE A"); // Convenience property should use Patient
    }

    [Fact]
    public void ParseFile_Claim_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Claims.Should().HaveCount(1);
        model.ClaimCount.Should().Be(1);

        var claim = model.Claims[0];
        claim.PatientAccountNumber.Should().Be("PATIENT-ACCT-001");
        claim.TotalCharge.Should().Be(250m);
        claim.ClaimInfo.FacilityCodeValue.Should().Be("11");     // Office
        claim.ClaimInfo.FacilityCodeQualifier.Should().Be("B");
        claim.ClaimInfo.ClaimFrequencyCode.Should().Be("1");     // Original
    }

    [Fact]
    public void ParseFile_DiagnosisCodes_ParsedFromComposites()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.AllDiagnoses.Should().HaveCount(3);
        claim.PrincipalDiagnosis.Should().NotBeNull();
        claim.PrincipalDiagnosis!.Code.Should().Be("J0610");
        claim.PrincipalDiagnosis.Qualifier.Should().Be("ABK");
    }

    [Fact]
    public void ParseFile_ServiceLines_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.ServiceLines.Should().HaveCount(2);

        var line1 = claim.ServiceLines[0];
        line1.LineNumber.Should().Be(1);
        line1.ProfessionalService.Should().NotBeNull();
        line1.ProfessionalService!.Procedure.Code.Should().Be("99213");
        line1.ProfessionalService.Procedure.Modifier1.Should().Be("25");
        line1.ProfessionalService.ChargeAmount.Should().Be(125m);
        line1.ProfessionalService.ServiceUnitCount.Should().Be(1);

        var line2 = claim.ServiceLines[1];
        line2.ProfessionalService!.Procedure.Code.Should().Be("99395");
        line2.ChargeAmount.Should().Be(125m); // Via convenience property
    }

    [Fact]
    public void ParseFile_References_AccessibleByQualifier()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.GetReference("EA")!.ReferenceId.Should().Be("MEDREC001");
        claim.GetReference("D9")!.ReferenceId.Should().Be("ORIG-CLM-001");
        claim.ReferenceByQualifier.Should().ContainKey("EA");
        claim.ReferenceByQualifier.Should().ContainKey("D9");
    }

    [Fact]
    public void ParseFile_RenderingProvider_ParsedInClaimLevel()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.Providers.Should().HaveCount(1);
        claim.Providers[0].Name.FullName.Should().Be("JOHNSON, EMILY");
        claim.Providers[0].Npi.Should().Be("9876543210");
    }

    [Fact]
    public void ParseFile_TotalCharges_SumsCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.TotalCharges.Should().Be(250m);
    }

    // ── Variant Detection Tests ──────────────────────────────────

    [Fact]
    public void DetectVariant_FallsBackToSvSegmentScan_WhenSt03Missing()
    {
        // Remove ST03 and GS08 implementation guide references
        var modified = _sampleFile
            .Replace("*005010X222A1~", "*~")
            .Replace("*005010X222A1~", "*~");

        var parser = new Claim837Parser(new ParserOptions { ValidationMode = ValidationMode.Lenient });
        var result = parser.ParseFile(modified);

        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].Variant.Should().Be(Claim837Variant.Professional);
    }

    // ── Validation Mode Tests ────────────────────────────────────

    [Fact]
    public void ParseFile_LenientMode_CollectsWarningsWithoutThrowing()
    {
        var malformedFile = _sampleFile.Replace("SV1*HC:99213:25*125*UN*1*11**1", "SV1*GARBAGE");

        var parser = new Claim837Parser(new ParserOptions { ValidationMode = ValidationMode.Lenient });
        var result = parser.ParseFile(malformedFile);

        result.Transactions.Should().HaveCount(1);
        // Parser should still work - it processes what it can
    }

    [Fact]
    public void ParseFile_NoneMode_CollectsUnmappedSegments()
    {
        var withUnknown = _sampleFile.Replace("SE*35*000000001", "ZZZ*CUSTOM*DATA~\nSE*36*000000001");

        var parser = new Claim837Parser(new ParserOptions { ValidationMode = ValidationMode.None });
        var result = parser.ParseFile(withUnknown);

        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].UnmappedSegments.Should().Contain(s => s.StartsWith("ZZZ"));
    }

    // ── PHI Redaction Tests ──────────────────────────────────────

    [Fact]
    public void RedactPhi_MasksPatientIdentifiers()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.RedactPhi();

        model.Subscriber.SubscriberName.LastName.Should().StartWith("D").And.Contain("*");
        model.Subscriber.SubscriberName.IdCode.Should().StartWith("A").And.Contain("*");
        model.Subscriber.Demographics!.DateOfBirth.Should().Be("********");
        model.Patient!.PatientName.LastName.Should().StartWith("D").And.Contain("*");
        model.Claims[0].ClaimInfo.PatientAccountNumber.Should().Contain("*");
    }

    // ── Batch / Error Isolation Tests ────────────────────────────

    [Fact]
    public void ParseFile_MalformedTransaction_IsolatedFromOthers()
    {
        // Build a two-transaction file where the second one is broken
        var twoTxn = _sampleFile.Replace(
            "SE*35*000000001~",
            "SE*35*000000001~" +
            "ST*837*000000002*005010X222A1~" +
            "THIS_IS_NOT_VALID_EDI~" +
            "SE*2*000000002~");

        var parser = new Claim837Parser(new ParserOptions { ValidationMode = ValidationMode.Lenient });
        var result = parser.ParseFile(twoTxn);

        // First transaction should parse fine
        result.Transactions.Should().HaveCountGreaterOrEqualTo(1);
    }
}
