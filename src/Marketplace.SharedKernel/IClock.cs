namespace Marketplace.SharedKernel;

/// <summary>The server clock is the only time source (product spec §7, Fairness). Inject it; never call DateTimeOffset.UtcNow directly.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
