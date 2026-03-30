using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.Remittance835.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.Remittance835.Tests.Parsing;

public class Remittance835ParserTests
{
    private readonly string _sampleFile;
    private readonly Remittance835Parser _parser;

    public Remittance835ParserTests()
    {
        var samplePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_835.x12");
        _sampleFile = File.ReadAllText(samplePath);
        _parser = new Remittance835Parser();
    }

    // ── Basic Parsing ────────────────────────────────────────────

    [Fact]
    public void ParseFile_Sample835_ReturnsOneTransaction()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void ParseFile_ParseDuration_IsRecorded()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.ParseDurationMs.Should().BeGreaterOrEqualTo(0);
    }

    // ── Financial Information (BPR) ──────────────────────────────

    [Fact]
    public void ParseFile_BPR_PaymentAmountParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.TotalPaymentAmount.Should().Be(1875.50m);
        model.FinancialInformation.CreditDebitFlag.Should().Be("C");
    }

    [Fact]
    public void ParseFile_BPR_EftDetailsParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.IsEft.Should().BeTrue();
        model.PaymentMethod.Should().Be("ACH");
        model.FinancialInformation.ReceiverDfiNumber.Should().Be("011000015");
        model.FinancialInformation.ReceiverAccountNumber.Should().Be("987654321");
    }

    [Fact]
    public void ParseFile_BPR_PaymentDateParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.PaymentDate.Should().Be("20230720");
    }

    // ── Trace Number (TRN) ───────────────────────────────────────

    [Fact]
    public void ParseFile_TRN_TraceNumberParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.CheckOrTraceNumber.Should().Be("TRACE20230720001");
        model.TraceNumber.OriginatingCompanyId.Should().Be("1234567890");
    }

    // ── Payer (1000A) ────────────────────────────────────────────

    [Fact]
    public void ParseFile_Payer_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Payer.PayerName.Should().Be("ACME HEALTH PLAN");
        model.Payer.PayerId.Should().Be("12345");
        model.Payer.Address.Should().NotBeNull();
        model.Payer.Address!.AddressLine1.Should().Be("100 INSURANCE BLVD");
        model.Payer.CityStateZip!.City.Should().Be("PAYERTOWN");
        model.Payer.CityStateZip.StateCode.Should().Be("CA");
        model.Payer.Contact.Should().NotBeNull();
        model.Payer.Contact!.CommNumber1.Should().Be("8005551234");
    }

    // ── Payee (1000B) ────────────────────────────────────────────

    [Fact]
    public void ParseFile_Payee_ParsedCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Payee.PayeeName.Should().Be("DR SMITH MEDICAL GROUP");
        model.Payee.PayeeNpi.Should().Be("1234567890");
        model.Payee.Address!.AddressLine1.Should().Be("456 PROVIDER AVE");
        model.Payee.GetReference("TJ")!.ReferenceId.Should().Be("987654321");
    }

    // ── Claims (2100) ────────────────────────────────────────────

    [Fact]
    public void ParseFile_Claims_ThreeClaimsParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.ClaimCount.Should().Be(3);
    }

    [Fact]
    public void ParseFile_Claim1_PaymentDetailsParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.PatientControlNumber.Should().Be("PATIENT-001");
        claim.ChargeAmount.Should().Be(500m);
        claim.PaymentAmount.Should().Be(350m);
        claim.ClaimPayment.PatientResponsibilityAmount.Should().Be(50m);
        claim.ClaimPayment.PayerClaimControlNumber.Should().Be("PAYERCN001");
        claim.ClaimStatusCode.Should().Be("1"); // Processed as primary
    }

    [Fact]
    public void ParseFile_Claim1_PatientNameParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.PatientName.Should().NotBeNull();
        claim.PatientName!.FullName.Should().Be("DOE, JOHN");
        claim.InsuredName.Should().NotBeNull();
        claim.InsuredName!.IdCode.Should().Be("MEM001");
    }

    [Fact]
    public void ParseFile_Claim1_ReferencesParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.GetReference("EA")!.ReferenceId.Should().Be("MEDREC001");
        claim.GetReference("D9")!.ReferenceId.Should().Be("ORIGCLAIM001");
    }

    // ── CAS Adjustments ──────────────────────────────────────────

    [Fact]
    public void ParseFile_Claim1_ClaimLevelAdjustments()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.Adjustments.Should().HaveCount(2);

        // CO*45*100 - Contractual obligation, charges exceed fee schedule
        claim.ContractualAdjustments.Should().HaveCount(1);
        claim.ContractualAdjustments.First().ReasonCode.Should().Be("45");
        claim.ContractualAdjustments.First().Amount.Should().Be(100m);

        // PR*1*30*2*20 - Patient responsibility: deductible + coinsurance
        claim.PatientResponsibilityAdjustments.Should().HaveCount(2);
        claim.PatientResponsibilityAdjustments.First().ReasonCode.Should().Be("1");  // Deductible
        claim.PatientResponsibilityAdjustments.First().Amount.Should().Be(30m);
        claim.PatientResponsibilityAdjustments.Last().ReasonCode.Should().Be("2");   // Coinsurance
        claim.PatientResponsibilityAdjustments.Last().Amount.Should().Be(20m);
    }

    [Fact]
    public void ParseFile_CAS_MultipleTripletsParsedCorrectly()
    {
        // The PR CAS segment has two reason/amount pairs: PR*1*30*2*20
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        var prCas = claim.Adjustments.First(c => c.ClaimAdjustmentGroupCode == "PR");
        prCas.Adjustments.Should().HaveCount(2);
        prCas.TotalAdjustmentAmount.Should().Be(50m); // 30 + 20
    }

    // ── Denied Claims ────────────────────────────────────────────

    [Fact]
    public void ParseFile_DeniedClaim_StatusCode4()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.DeniedClaims.Should().HaveCount(1);
        var denied = model.DeniedClaims.First();
        denied.PatientControlNumber.Should().Be("PATIENT-003");
        denied.PaymentAmount.Should().Be(0m);
        denied.IsDenied.Should().BeTrue();
    }

    // ── Service Lines (2110) ─────────────────────────────────────

    [Fact]
    public void ParseFile_Claim1_TwoServiceLines()
    {
        var result = _parser.ParseFile(_sampleFile);
        var claim = result.Transactions[0].Claims[0];

        claim.ServiceLines.Should().HaveCount(2);

        var line1 = claim.ServiceLines[0];
        line1.ProcedureCode.Should().Be("99213");
        line1.ServicePayment.Procedure.Modifier1.Should().Be("25");
        line1.ChargeAmount.Should().Be(250m);
        line1.PaymentAmount.Should().Be(175m);
        line1.ServiceDate.Should().Be("20230615");
    }

    [Fact]
    public void ParseFile_ServiceLine_AdjustmentsParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var line1 = result.Transactions[0].Claims[0].ServiceLines[0];

        line1.Adjustments.Should().HaveCount(2);
        line1.AllAdjustments.Should().HaveCount(3); // CO has 1, PR has 2
    }

    [Fact]
    public void ParseFile_ServiceLine_RemarkCodesParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var line1 = result.Transactions[0].Claims[0].ServiceLines[0];

        line1.RemarkCodes.Should().HaveCount(1);
        line1.AllRemarkCodes.Should().Contain("N130");
    }

    // ── Provider Adjustments (PLB) ───────────────────────────────

    [Fact]
    public void ParseFile_PLB_ProviderAdjustmentsParsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.ProviderAdjustments.Should().HaveCount(1);
        var plb = model.ProviderAdjustments[0];

        plb.ProviderIdentifier.Should().Be("987654321");
        plb.Adjustments.Should().HaveCount(2);

        // 72:OVERPAY001*-125.50 (Overpayment recovery, negative = money back to payer)
        plb.Adjustments[0].AdjustmentReasonCode.Should().Be("72");
        plb.Adjustments[0].ReferenceId.Should().Be("OVERPAY001");
        plb.Adjustments[0].Amount.Should().Be(-125.50m);

        // L6:INT001*1.00 (Interest)
        plb.Adjustments[1].AdjustmentReasonCode.Should().Be("L6");
        plb.Adjustments[1].Amount.Should().Be(1.00m);
    }

    [Fact]
    public void ParseFile_TotalProviderAdjustments_Calculated()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.TotalProviderAdjustments.Should().Be(-124.50m); // -125.50 + 1.00
    }

    // ── Summary / Convenience Properties ─────────────────────────

    [Fact]
    public void ParseFile_TotalCharges_SumsAcrossClaims()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.TotalCharges.Should().Be(1500m); // 500 + 800 + 200
    }

    [Fact]
    public void ParseFile_TotalClaimPayments_SumsAcrossClaims()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.TotalClaimPayments.Should().Be(1000m); // 350 + 650 + 0
    }

    [Fact]
    public void ParseFile_PaidClaims_FilteredCorrectly()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.PaidClaims.Should().HaveCount(2);
        model.DeniedClaims.Should().HaveCount(1);
    }

    // ── PHI Redaction ────────────────────────────────────────────

    [Fact]
    public void RedactPhi_MasksPatientAndBankInfo()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.RedactPhi();

        // Bank accounts fully redacted
        model.FinancialInformation.ReceiverAccountNumber.Should().Be("*********");
        model.FinancialInformation.SenderAccountNumber.Should().Be("*********");

        // Patient names masked
        var claim1 = model.Claims[0];
        claim1.PatientName!.LastName.Should().StartWith("D").And.Contain("*");
        claim1.PatientName!.IdCode.Should().Contain("*");

        // Patient control numbers masked
        claim1.ClaimPayment.PatientControlNumber.Should().Contain("*");

        // Provider tax ID masked
        model.ProviderAdjustments[0].ProviderIdentifier.Should().Contain("*");
    }

    // ── Validation Modes ─────────────────────────────────────────

    [Fact]
    public void ParseFile_NoneMode_CollectsUnmappedSegments()
    {
        var withUnknown = _sampleFile.Replace("SE*52*000000001", "ZZZ*CUSTOM*DATA~\nSE*53*000000001");

        var parser = new Remittance835Parser(new ParserOptions { ValidationMode = ValidationMode.None });
        var result = parser.ParseFile(withUnknown);

        result.Transactions.Should().HaveCount(1);
        result.Transactions[0].UnmappedSegments.Should().Contain(s => s.StartsWith("ZZZ"));
    }

    // ── Envelope Data ────────────────────────────────────────────

    [Fact]
    public void ParseFile_InterchangeAndGroup_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);

        result.InterchangeHeader!.SenderId.Should().Be("ACMEPAYER");
        result.FunctionalGroupHeader!.FunctionalIdentifierCode.Should().Be("HP");
        result.FunctionalGroupHeader.VersionReleaseCode.Should().Be("005010X221A1");
    }
}
