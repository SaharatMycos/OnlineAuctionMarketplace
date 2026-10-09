using Marketplace.Bootstrap;
using Marketplace.SharedKernel.Configuration;
using Marketplace.Worker;

DotEnv.Load();
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddMarketplace(builder.Configuration);
builder.Services.AddRedisRealtimePublisher();
builder.Services.AddHostedService<OutboxRelay>();
builder.Services.AddHostedService<JobRunner>();

builder.Build().Run();
