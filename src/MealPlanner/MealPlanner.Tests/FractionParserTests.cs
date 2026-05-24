using MealPlanner.Services;

namespace MealPlanner.Tests;

[TestFixture]
public class FractionParserTests
{
    [TestCase("0.5",  0.5f)]
    [TestCase("1.75", 1.75f)]
    [TestCase("3",    3.0f)]
    [TestCase("0",    0.0f)]
    [TestCase("1",    1.0f)]
    public void ParseAmount_Decimal_ReturnsCorrectFloat(string input, float expected)
        => Assert.That(FractionParser.ParseAmount(input), Is.EqualTo(expected).Within(0.001f));

    [TestCase("1/2", 0.5f)]
    [TestCase("1/4", 0.25f)]
    [TestCase("3/4", 0.75f)]
    [TestCase("2/3", 0.6667f)]
    [TestCase("1/8", 0.125f)]
    public void ParseAmount_Fraction_ReturnsCorrectFloat(string input, float expected)
        => Assert.That(FractionParser.ParseAmount(input), Is.EqualTo(expected).Within(0.001f));

    [TestCase("1 1/2", 1.5f)]
    [TestCase("2 1/4", 2.25f)]
    [TestCase("3 2/3", 3.6667f)]
    [TestCase("1 1/8", 1.125f)]
    public void ParseAmount_MixedNumber_ReturnsCorrectFloat(string input, float expected)
        => Assert.That(FractionParser.ParseAmount(input), Is.EqualTo(expected).Within(0.001f));

    [TestCase("abc")]
    [TestCase("")]
    [TestCase(null)]
    [TestCase("1/0")]
    [TestCase("-1")]
    public void ParseAmount_Invalid_ReturnsNull(string? input)
        => Assert.That(FractionParser.ParseAmount(input), Is.Null);

    [TestCase(0.5f,  "1/2")]
    [TestCase(0.25f, "1/4")]
    [TestCase(0.75f, "3/4")]
    [TestCase(1.5f,  "1 1/2")]
    [TestCase(2.25f, "2 1/4")]
    [TestCase(1.0f,  "1")]
    [TestCase(3.0f,  "3")]
    [TestCase(0.0f,  "0")]
    [TestCase(0.125f, "1/8")]
    [TestCase(0.6667f, "2/3")]
    public void FormatAmount_ReturnsReadableString(float input, string expected)
        => Assert.That(FractionParser.FormatAmount(input), Is.EqualTo(expected));

    [Test]
    public void FormatAmount_UnrecognisedDecimal_FallsBackToDecimalString()
    {
        // 0.1 is not within tolerance of any common fraction
        string result = FractionParser.FormatAmount(0.1f);
        Assert.That(result, Does.Not.Contain("/"));
    }

    [Test]
    public void ParseAmount_ThenFormatAmount_RoundTrips()
    {
        foreach (string input in new[] { "1/2", "1 1/4", "3/4", "2", "0.5" })
        {
            float? parsed = FractionParser.ParseAmount(input);
            Assert.That(parsed, Is.Not.Null, $"ParseAmount failed for '{input}'");
            string formatted = FractionParser.FormatAmount(parsed!.Value);
            float? reparsed = FractionParser.ParseAmount(formatted);
            Assert.That(reparsed, Is.EqualTo(parsed).Within(0.001f),
                $"Round-trip failed for '{input}': parsed={parsed}, formatted='{formatted}', reparsed={reparsed}");
        }
    }
}
