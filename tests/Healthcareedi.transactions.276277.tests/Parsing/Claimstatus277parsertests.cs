using HealthcareEdi.Core.Parsing;
using HealthcareEdi.Transactions.ClaimStatus276277.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.ClaimStatus276277.Tests.Parsing;

public class ClaimStatus277ParserTests
{
    private readonly string _sampleFile;
    private readonly ClaimStatus276277Parser _parser;

    public ClaimStatus277ParserTests()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_277.x12");
        _sampleFile = File.ReadAllText(path);
        _parser = new ClaimStatus276277Parser();
    }

    [Fact]
    public void Parse277_ReturnsOneTransaction()
    {
        var result = _parser.Parse277File(_sampleFile);
        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void Parse277_Payer_Parsed()
    {
        var result = _parser.Parse277File(_sampleFile);
        result.Transactions[0].Payer.PayerName.Should().Be("ACME HEALTH PLAN");
    }

    [Fact]
    public void Parse277_Provider_Parsed()
    {
        var result = _parser.Parse277File(_sampleFile);
        result.Transactions[0].Provider.ProviderName.Should().Be("DR SMITH MEDICAL GROUP");
        result.Transactions[0].Provider.Npi.Should().Be("1234567890");
    }

    [Fact]
    public void Parse277_Subscriber_Parsed()
    {
        var result = _parser.Parse277File(_sampleFile);
        var sub = result.Transactions[0].Subscribers[0];
        sub.SubscriberName.Should().Be("DOE, JOHN");
        sub.MemberId.Should().Be("MBR123456");
    }

    [Fact]
    public void Parse277_ThreeClaimStatuses()
    {
        var result = _parser.Parse277File(_sampleFile);
        var sub = result.Transactions[0].Subscribers[0];
        sub.ClaimStatuses.Should().HaveCount(3);
    }

    [Fact]
    public void Parse277_PaidClaim_StatusParsed()
    {
        var result = _parser.Parse277File(_sampleFile);
        var claim = result.Transactions[0].Subscribers[0].ClaimStatuses[0];

        claim.TraceNumber.Should().Be("TRACE001");
        claim.PrimaryStatus.Should().NotBeNull();
        claim.PrimaryStatus!.StatusInformation.CategoryCode.Should().Be("F1");
        claim.StatusCategory.Should().Be("Finalized/Payment");
        claim.IsPaid.Should().BeTrue();
        claim.IsFinalized.Should().BeTrue();
        claim.PrimaryStatus.StatusInformation.StatusCode.Should().Be("20");
        claim.PrimaryStatus.TotalClaimChargeAmount.Should().Be(500m);
        claim.PrimaryStatus.ClaimPaymentAmount.Should().Be(350m);
    }

    [Fact]
    public void Parse277_PendingClaim()
    {
        var result = _parser.Parse277File(_sampleFile);
        var claim = result.Transactions[0].Subscribers[0].ClaimStatuses[1];

        claim.TraceNumber.Should().Be("TRACE002");
        claim.IsPending.Should().BeTrue();
        claim.StatusCategory.Should().Be("Pending - Payer Review");
    }

    [Fact]
    public void Parse277_DeniedClaim()
    {
        var result = _parser.Parse277File(_sampleFile);
        var claim = result.Transactions[0].Subscribers[0].ClaimStatuses[2];

        claim.TraceNumber.Should().Be("TRACE003");
        claim.IsDenied.Should().BeTrue();
        claim.StatusCategory.Should().Be("Finalized/Denial");
    }

    [Fact]
    public void Parse277_ConvenienceFilters()
    {
        var result = _parser.Parse277File(_sampleFile);
        var model = result.Transactions[0];

        model.PaidClaims.Should().HaveCount(1);
        model.PendingClaims.Should().HaveCount(1);
        model.DeniedClaims.Should().HaveCount(1);
        model.FinalizedClaims.Should().HaveCount(2);
    }

    [Fact]
    public void Parse277_References_Parsed()
    {
        var result = _parser.Parse277File(_sampleFile);
        var claim = result.Transactions[0].Subscribers[0].ClaimStatuses[0];

        claim.GetReference("1K")!.ReferenceId.Should().Be("PAYERCN001");
        claim.GetReference("D9")!.ReferenceId.Should().Be("ORIGCLAIM001");
    }

    [Fact]
    public void Parse277_PhiRedaction()
    {
        var result = _parser.Parse277File(_sampleFile);
        var model = result.Transactions[0];

        model.RedactPhi();

        model.Subscribers[0].Name.LastName.Should().StartWith("D").And.Contain("*");
        model.Subscribers[0].Name.IdCode.Should().Contain("*");
    }
}
