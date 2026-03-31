using HealthcareEdi.Transactions.Acknowledgments.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Transactions.Acknowledgments.Tests.Parsing;

public class Acknowledgment999ParserTests
{
    private readonly string _sampleFile;
    private readonly AcknowledgmentParser _parser;

    public Acknowledgment999ParserTests()
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SampleFiles", "Sample_999.x12");
        _sampleFile = File.ReadAllText(path);
        _parser = new AcknowledgmentParser();
    }

    [Fact]
    public void Parse999_ReturnsOneTransaction()
    {
        var result = _parser.Parse999File(_sampleFile);
        result.Transactions.Should().HaveCount(1);
        result.FailedTransactions.Should().BeEmpty();
    }

    [Fact]
    public void Parse999_GroupResponse_Parsed()
    {
        var result = _parser.Parse999File(_sampleFile);
        var model = result.Transactions[0];

        model.AcknowledgedGroupType.Should().Be("HC");
        model.AcknowledgedGroupControlNumber.Should().Be("101");
        model.GroupResponse.VersionReleaseCode.Should().Be("005010X222A1");
    }

    [Fact]
    public void Parse999_GroupTrailer_Parsed()
    {
        var result = _parser.Parse999File(_sampleFile);
        var model = result.Transactions[0];

        model.IsGroupAccepted.Should().BeFalse();
        model.GroupTrailer.Should().NotBeNull();
        model.GroupTrailer!.IsPartiallyAccepted.Should().BeTrue();
        model.TotalTransactions.Should().Be(3);
        model.AcceptedTransactions.Should().Be(2);
    }

    [Fact]
    public void Parse999_ThreeTransactionAcknowledgments()
    {
        var result = _parser.Parse999File(_sampleFile);
        var model = result.Transactions[0];

        model.TransactionAcknowledgments.Should().HaveCount(3);
    }

    [Fact]
    public void Parse999_FirstTransaction_Accepted()
    {
        var result = _parser.Parse999File(_sampleFile);
        var txn = result.Transactions[0].TransactionAcknowledgments[0];

        txn.TransactionId.Should().Be("837");
        txn.ControlNumber.Should().Be("000000001");
        txn.IsAccepted.Should().BeTrue();
        txn.Status.Should().Be("Accepted");
    }

    [Fact]
    public void Parse999_SecondTransaction_Rejected()
    {
        var result = _parser.Parse999File(_sampleFile);
        var txn = result.Transactions[0].TransactionAcknowledgments[1];

        txn.ControlNumber.Should().Be("000000002");
        txn.IsRejected.Should().BeTrue();
        txn.Status.Should().Be("Rejected");
        txn.ErrorSegments.Should().HaveCount(2); // IK3 + IK4
    }

    [Fact]
    public void Parse999_ThirdTransaction_Accepted()
    {
        var result = _parser.Parse999File(_sampleFile);
        var txn = result.Transactions[0].TransactionAcknowledgments[2];

        txn.IsAccepted.Should().BeTrue();
    }

    [Fact]
    public void Parse999_ConvenienceFilters()
    {
        var result = _parser.Parse999File(_sampleFile);
        var model = result.Transactions[0];

        model.AcceptedTransactionSets.Should().HaveCount(2);
        model.RejectedTransactions.Should().HaveCount(1);
        model.RejectedTransactions.First().ControlNumber.Should().Be("000000002");
    }

    [Fact]
    public void Parse999_Envelope_Parsed()
    {
        var result = _parser.Parse999File(_sampleFile);

        result.InterchangeHeader!.SenderId.Should().Be("RECEIVER");
        result.FunctionalGroupHeader!.FunctionalIdentifierCode.Should().Be("FA");
    }
}
