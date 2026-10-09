using Marketplace.Bootstrap;
using Marketplace.Realtime;
using Marketplace.SharedKernel.Auth;
using Marketplace.SharedKernel.Configuration;
using Marketplace.SharedKernel.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarketplace(builder.Configuration);
builder.Services.AddMarketplaceAuthentication();
builder.Services.AddRedis();
builder.Services.AddSignalR();
builder.Services.AddHostedService<RedisRealtimeSubscriber>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapHub<AuctionHub>("/hubs/auctions");

app.Run();
