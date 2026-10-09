using System.Text.Json;
using System.Text.Json.Serialization;
using Marketplace.Api;
using Marketplace.Bootstrap;
using Marketplace.SharedKernel.Auth;
using Marketplace.SharedKernel.Configuration;
using Marketplace.SharedKernel.Health;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

DotEnv.Load();
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddMarketplace(builder.Configuration);
builder.Services.AddMarketplaceAuthentication();
builder.Services.ConfigureHttpJsonOptions(o =>
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)));
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ProblemExceptionHandler>();
builder.Services.AddHealthChecks()
    .AddCheck<PostgresHealthCheck>("postgres", tags: ["ready"]);
builder.Services.AddCors(options => options.AddDefaultPolicy(policy => policy
    .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [])
    .AllowAnyHeader()
    .AllowAnyMethod()));

var app = builder.Build();

// The api process owns migrations; realtime and worker never run them.
if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
    await app.Services.MigrateDatabaseAsync();

app.UseExceptionHandler();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = c => c.Tags.Contains("ready") });
app.MapGet("/v1/modules", () => ModuleCatalog.All.Select(m => m.Name)).WithTags("Platform");
app.MapGet("/v1/time", (Marketplace.SharedKernel.IClock clock) => new { serverTime = clock.UtcNow }).WithTags("Platform");
app.MapMarketplaceModules();

app.Run();

public partial class Program { }
