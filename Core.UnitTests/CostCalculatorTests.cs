using ClaudeMonitor.Core.Sessions;
using Xunit;

namespace ClaudeMonitor.Core.Tests;

public class CostCalculatorTests
{
    [Theory]
    [InlineData("claude-opus-4-6", 15.0, 75.0)]
    [InlineData("OPUS-4-5", 15.0, 75.0)]
    [InlineData("claude-sonnet-4-6", 3.0, 15.0)]
    [InlineData("claude-haiku-4-5", 0.80, 4.0)]
    public void PriceFor_KnownFamilies_ReturnsExpectedRates(string model, double expectedInput, double expectedOutput)
    {
        var p = CostCalculator.PriceFor(model);
        Assert.Equal(expectedInput, p.InputPerMillion);
        Assert.Equal(expectedOutput, p.OutputPerMillion);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("gpt-4o")]
    [InlineData("some-other-model")]
    public void PriceFor_UnknownOrEmpty_ReturnsZero(string? model)
    {
        var p = CostCalculator.PriceFor(model);
        Assert.Equal(0, p.InputPerMillion);
        Assert.Equal(0, p.OutputPerMillion);
        Assert.Equal(0, p.CacheWritePerMillion);
        Assert.Equal(0, p.CacheReadPerMillion);
    }

    [Fact]
    public void ComputeUsd_SumsAllFourCategoriesWithPerMillionDivision()
    {
        var pricing = CostCalculator.PriceFor("claude-opus-4-6");
        // 1_000_000 tokens of each category → whole-dollar components.
        var usd = CostCalculator.ComputeUsd(pricing, 1_000_000, 1_000_000, 1_000_000, 1_000_000);
        Assert.Equal(15 + 75 + 18.75 + 1.50, usd, 4);
    }

    [Fact]
    public void ComputeUsd_Zeroed_ReturnsZero()
    {
        var pricing = CostCalculator.PriceFor("claude-sonnet-4-6");
        Assert.Equal(0, CostCalculator.ComputeUsd(pricing, 0, 0, 0, 0));
    }

    [Fact]
    public void ComputeUsd_FractionalTokens_DividesCorrectly()
    {
        var pricing = CostCalculator.PriceFor("claude-sonnet-4-6");
        // 500 input tokens at $3/M = $0.0015
        var usd = CostCalculator.ComputeUsd(pricing, 500, 0, 0, 0);
        Assert.Equal(0.0015, usd, 6);
    }
}
