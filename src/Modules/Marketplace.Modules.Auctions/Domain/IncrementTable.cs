namespace Marketplace.Modules.Auctions.Domain;

/// <summary>Default bid increments by current price (feature design §3.1).</summary>
internal static class IncrementTable
{
    private static readonly (decimal Below, decimal Increment)[] Steps =
    [
        (1m, 0.05m),
        (5m, 0.25m),
        (25m, 0.50m),
        (100m, 1m),
        (250m, 2.50m),
        (500m, 5m),
        (1_000m, 10m),
        (2_500m, 25m),
        (5_000m, 50m),
    ];

    public static decimal For(decimal price)
    {
        foreach (var (below, increment) in Steps)
            if (price < below)
                return increment;
        return 100m;
    }
}
