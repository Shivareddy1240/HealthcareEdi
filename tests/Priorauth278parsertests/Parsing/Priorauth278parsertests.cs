using HealthcareEdi.Transactions.PriorAuth278.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.PriorAuth278.Tests.Parsing;

public class PriorAuth278ParserTests
{
    private readonly string _sampleFile;
    private readonly PriorAuth278Parser _parser;

    public PriorAuth278ParserTests()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_278_Response.x12");
        _sampleFile = File.ReadAllText(path);
        _parser = new PriorAuth278Parser();
    }

    [Fact]
    public void ParseResponse_ReturnsOneTransaction()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_Payer_Parsed()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        result.Transactions[0].Payer.PayerName.Should().Be("ACME HEALTH PLAN");
    }

    [Fact]
    public void ParseResponse_Provider_WithNpiAndTaxonomy()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        var model = result.Transactions[0];
        model.Provider.Npi.Should().Be("1234567890");
        model.Provider.ProviderInfo!.TaxonomyCode.Should().Be("207Q00000X");
    }

    [Fact]
    public void ParseResponse_Subscriber_Parsed()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        var model = result.Transactions[0];
        model.Subscriber!.SubscriberName.Should().Be("DOE, JOHN");
        model.Subscriber.MemberId.Should().Be("MBR123456");
    }

    [Fact]
    public void ParseResponse_ServiceReview_Certified()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        var model = result.Transactions[0];

        model.ServiceReviews.Should().HaveCount(1);
        var review = model.ServiceReviews[0];
        review.IsCertified.Should().BeTrue();
        review.AuthorizationNumber.Should().Be("AUTH2023090100001");
        review.Decision.Should().Be("Certified in Total");
        review.UtilizationManagement!.RequestCategory.Should().Be("Health Services Review");
        review.UtilizationManagement.CertificationType.Should().Be("Initial");
        review.DiagnosisCodes.Should().Contain("M5451");
    }

    [Fact]
    public void ParseResponse_CertifiedReviews_Convenience()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        var model = result.Transactions[0];
        model.CertifiedReviews.Should().HaveCount(1);
        model.DeniedReviews.Should().BeEmpty();
    }

    [Fact]
    public void ParseResponse_PhiRedaction()
    {
        var result = _parser.ParseResponseFile(_sampleFile);
        var model = result.Transactions[0];
        model.RedactPhi();
        model.Subscriber!.Name.LastName.Should().Contain("*");
        model.Subscriber.Demographics!.DateOfBirth.Should().Be("********");
    }
}
