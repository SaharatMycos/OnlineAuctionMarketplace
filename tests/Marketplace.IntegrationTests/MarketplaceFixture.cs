using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Events;
using Marketplace.SharedKernel.Jobs;
using Marketplace.SharedKernel.Outbox;
using Marketplace.SharedKernel.Realtime;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Marketplace.IntegrationTests;

/// <summary>Controllable server clock: the only time source, so tests can move to an auction's end.</summary>
public sealed class FakeClock : IClock
{
    private DateTimeOffset _now = DateTimeOffset.UtcNow;
    public DateTimeOffset UtcNow => _now;
    public void Advance(TimeSpan by) => _now += by;
    public void Set(DateTimeOffset to) => _now = to;
}

/// <summary>Collects what the outbox relay would push to browsers.</summary>
public sealed class CapturingRealtimePublisher : IRealtimePublisher
{
    public ConcurrentQueue<RealtimePush> Pushes { get; } = new();

    public Task PublishAsync(RealtimePush push, CancellationToken ct)
    {
        Pushes.Enqueue(push);
        return Task.CompletedTask;
    }
}

/// <summary>
/// The api in-process against a real PostGIS container (migrations applied on start). Background
/// work is driven explicitly: <see cref="DrainOutboxAsync"/> relays events, <see cref="CloseDueAuctionsAsync"/>
/// runs the close job.
/// </summary>
public sealed class MarketplaceFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-0123456789";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
        .WithImage("postgis/postgis:17-3.5")
        .WithDatabase("marketplace_tests")
        .WithUsername("marketplace")
        .WithPassword("marketplace")
        .Build();

    public FakeClock Clock { get; } = new();
    public CapturingRealtimePublisher Realtime { get; } = new();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        _ = Server; // boots the host, which applies migrations
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningKey", SigningKey);
        builder.UseSetting("Auth:DevTokens", "true");
        builder.ConfigureTestServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IClock>(Clock));
            services.Replace(ServiceDescriptor.Singleton<IRealtimePublisher>(Realtime));
        });
    }

    public Task DrainOutboxAsync() => Services.GetRequiredService<OutboxProcessor>().DrainAsync();

    public async Task<int> CloseDueAuctionsAsync()
    {
        var job = Services.GetServices<IBackgroundJob>().Single(j => j.Name == "auctions.close");
        var closed = await job.RunOnceAsync(CancellationToken.None);
        await DrainOutboxAsync();
        return closed;
    }

    public async Task<HttpClient> SignInAsync(string name)
    {
        var anonymous = CreateClient();
        var response = await anonymous.PostAsJsonAsync("/v1/dev/token", new { email = $"{name}@example.test" });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.GetProperty("token").GetString());
        return client;
    }

    public static readonly JsonSerializerOptions Json = EventJson.Options;
}

[CollectionDefinition(Name)]
public sealed class MarketplaceCollection : ICollectionFixture<MarketplaceFixture>
{
    public const string Name = "marketplace";
}
