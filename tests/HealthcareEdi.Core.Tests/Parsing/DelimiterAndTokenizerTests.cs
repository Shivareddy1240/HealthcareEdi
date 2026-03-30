using HealthcareEdi.Core.Parsing;
using Xunit;
using FluentAssertions;

namespace HealthcareEdi.Core.Tests.Parsing;

public class DelimiterContextTests
{
    // Standard ISA segment (106 characters, delimiters: * ~ :)
    private const string StandardIsa =
        "ISA*00*          *00*          *ZZ*SENDER         *ZZ*RECEIVER       *230101*1200*^*00501*000000001*0*P*:~";

    [Fact]
    public void DetectFromIsa_StandardDelimiters_ParsesCorrectly()
    {
        var ctx = DelimiterContext.DetectFromIsa(StandardIsa.AsSpan());

        ctx.ElementSeparator.Should().Be('*');
        ctx.SegmentTerminator.Should().Be('~');
        ctx.ComponentSeparator.Should().Be(':');
        ctx.RepetitionSeparator.Should().Be('^');
    }

    [Fact]
    public void DetectFromIsa_CustomDelimiters_ParsesCorrectly()
    {
        // Some legacy systems use | for elements and # for segments
        var customIsa =
            "ISA|00|          |00|          |ZZ|SENDER         |ZZ|RECEIVER       |230101|1200|^|00501|000000001|0|P|:#";

        var ctx = DelimiterContext.DetectFromIsa(customIsa.AsSpan());

        ctx.ElementSeparator.Should().Be('|');
        ctx.SegmentTerminator.Should().Be('#');
        ctx.ComponentSeparator.Should().Be(':');
    }

    [Fact]
    public void DetectFromIsa_TooShort_ThrowsEdiParseException()
    {
        var shortIsa = "ISA*00*too_short";

        var act = () => DelimiterContext.DetectFromIsa(shortIsa.AsSpan());

        act.Should().Throw<EdiParseException>()
            .WithMessage("*106 characters*");
    }

    [Fact]
    public void SplitElements_SplitsCorrectly()
    {
        var ctx = DelimiterContext.DetectFromIsa(StandardIsa.AsSpan());

        var elements = ctx.SplitElements("NM1*85*1*SMITH*JOHN****XX*1234567890");

        elements.Should().HaveCount(10);
        elements[0].Should().Be("NM1");
        elements[1].Should().Be("85");
        elements[3].Should().Be("SMITH");
        elements[9].Should().Be("1234567890");
    }

    [Fact]
    public void SplitComponents_SplitsCompositeElements()
    {
        var ctx = DelimiterContext.DetectFromIsa(StandardIsa.AsSpan());

        var components = ctx.SplitComponents("HC:99213:25");

        components.Should().HaveCount(3);
        components[0].Should().Be("HC");
        components[1].Should().Be("99213");
        components[2].Should().Be("25");
    }
}

public class EdiTokenizerTests
{
    private const string MinimalFile =
        "ISA*00*          *00*          *ZZ*SENDER         *ZZ*RECEIVER       *230101*1200*^*00501*000000001*0*P*:~" +
        "GS*HC*SENDER*RECEIVER*20230101*1200*1*X*005010X222A1~" +
        "ST*837*0001*005010X222A1~" +
        "BHT*0019*00*12345*20230101*1200*CH~" +
        "SE*3*0001~" +
        "GE*1*1~" +
        "IEA*1*000000001~";

    [Fact]
    public void Tokenize_MinimalFile_ExtractsOneTransaction()
    {
        var tokenizer = new EdiTokenizer();

        var result = tokenizer.Tokenize(MinimalFile);

        result.Transactions.Should().HaveCount(1);
        result.Delimiters.ElementSeparator.Should().Be('*');
        result.IsaElements.Should().NotBeNull();
        result.GsElements.Should().NotBeNull();
    }

    [Fact]
    public void Tokenize_EmptyContent_ThrowsEdiParseException()
    {
        var tokenizer = new EdiTokenizer();

        var act = () => tokenizer.Tokenize("");

        act.Should().Throw<EdiParseException>();
    }

    [Fact]
    public void Tokenize_WithBom_HandlesGracefully()
    {
        var withBom = "\uFEFF" + MinimalFile;
        var tokenizer = new EdiTokenizer();

        var result = tokenizer.Tokenize(withBom);

        result.Transactions.Should().HaveCount(1);
    }

    [Fact]
    public void Tokenize_MultipleTransactions_GroupsCorrectly()
    {
        var multiTxn =
            "ISA*00*          *00*          *ZZ*SENDER         *ZZ*RECEIVER       *230101*1200*^*00501*000000001*0*P*:~" +
            "GS*HC*SENDER*RECEIVER*20230101*1200*1*X*005010X222A1~" +
            "ST*837*0001*005010X222A1~" +
            "BHT*0019*00*111*20230101*1200*CH~" +
            "SE*3*0001~" +
            "ST*837*0002*005010X222A1~" +
            "BHT*0019*00*222*20230101*1200*CH~" +
            "SE*3*0002~" +
            "GE*2*1~" +
            "IEA*1*000000001~";

        var tokenizer = new EdiTokenizer();
        var result = tokenizer.Tokenize(multiTxn);

        result.Transactions.Should().HaveCount(2);
    }
}
