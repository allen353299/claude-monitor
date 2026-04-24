namespace ClaudeMonitor.Core.Sessions;

public readonly record struct ModelPricing(
    double InputPerMillion,
    double OutputPerMillion,
    double CacheWritePerMillion,
    double CacheReadPerMillion);

public static class CostCalculator
{
    private static readonly ModelPricing OpusPricing = new(15.0, 75.0, 18.75, 1.50);
    private static readonly ModelPricing SonnetPricing = new(3.0, 15.0, 3.75, 0.30);
    private static readonly ModelPricing HaikuPricing = new(0.80, 4.0, 1.00, 0.08);
    private static readonly ModelPricing UnknownPricing = new(0, 0, 0, 0);

    public static ModelPricing PriceFor(string? model)
    {
        if (string.IsNullOrWhiteSpace(model)) return UnknownPricing;
        var m = model.ToLowerInvariant();
        if (m.Contains("opus")) return OpusPricing;
        if (m.Contains("sonnet")) return SonnetPricing;
        if (m.Contains("haiku")) return HaikuPricing;
        return UnknownPricing;
    }

    public static double ComputeUsd(ModelPricing p, long input, long output, long cacheWrite, long cacheRead)
    {
        var usd = 0.0;
        usd += input * p.InputPerMillion / 1_000_000.0;
        usd += output * p.OutputPerMillion / 1_000_000.0;
        usd += cacheWrite * p.CacheWritePerMillion / 1_000_000.0;
        usd += cacheRead * p.CacheReadPerMillion / 1_000_000.0;
        return usd;
    }
}
