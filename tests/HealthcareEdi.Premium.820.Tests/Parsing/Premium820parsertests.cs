using HealthcareEdi.Transactions.Premium820.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.Premium820.Tests.Parsing;

public class Premium820ParserTests
{
    private readonly string _sampleFile;
    private readonly Premium820Parser _parser;

    public Premium820ParserTests()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_820.x12");
        _sampleFile = File.ReadAllText(path);
        _parser = new Premium820Parser();
    }

    [Fact]
    public void ParseFile_ReturnsOneTransaction()
    {
        var result = _parser.ParseFile(_sampleFile);
        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void ParseFile_FinancialInfo_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.TotalPremiumAmount.Should().Be(25750.00m);
        model.PaymentMethod.Should().Be("ACH");
        model.FinancialInformation.IsEft.Should().BeTrue();
    }

    [Fact]
    public void ParseFile_TraceNumber_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        result.Transactions[0].TraceNumber.Should().Be("PREMPAY20230901001");
    }

    [Fact]
    public void ParseFile_Remitter_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Remitter.RemitterName.Should().Be("ACME CORPORATION");
        model.Remitter.Address!.AddressLine1.Should().Be("500 CORPORATE PLAZA");
        model.Remitter.CityStateZip!.StateCode.Should().Be("TX");
    }

    [Fact]
    public void ParseFile_Receiver_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Receiver.ReceiverName.Should().Be("UNITED HEALTH PLANS");
    }

    [Fact]
    public void ParseFile_TwoOrganizations()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.Organizations.Should().HaveCount(2);
    }

    [Fact]
    public void ParseFile_Org1_TwoPremiumDetails()
    {
        var result = _parser.ParseFile(_sampleFile);
        var org1 = result.Transactions[0].Organizations[0];

        org1.PremiumDetails.Should().HaveCount(2);
        org1.PremiumDetails[0].PremiumAmount.Should().Be(12500.00m);
        org1.PremiumDetails[0].Remittance!.ReferenceId.Should().Be("INV202309-001");
        org1.PremiumDetails[1].PremiumAmount.Should().Be(8250.00m);
        org1.TotalPremium.Should().Be(20750.00m);
    }

    [Fact]
    public void ParseFile_Org2_OnePremiumDetail()
    {
        var result = _parser.ParseFile(_sampleFile);
        var org2 = result.Transactions[0].Organizations[1];

        org2.PremiumDetails.Should().HaveCount(1);
        org2.PremiumDetails[0].PremiumAmount.Should().Be(5000.00m);
    }

    [Fact]
    public void ParseFile_AllPremiumDetails_Flattened()
    {
        var result = _parser.ParseFile(_sampleFile);
        var model = result.Transactions[0];

        model.AllPremiumDetails.Should().HaveCount(3);
    }

    [Fact]
    public void ParseFile_PolicyReferences_Parsed()
    {
        var result = _parser.ParseFile(_sampleFile);
        var detail = result.Transactions[0].Organizations[0].PremiumDetails[0];

        detail.MemberPolicyNumber.Should().Be("GROUPNUM001");
    }
}
