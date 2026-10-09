using Marketplace.Modules.Messaging.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Messaging;

/// <summary>Messaging: buyer-seller conversations about listings and orders.</summary>
public sealed class MessagingModule : IModule
{
    public const string Schema = "messaging";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<MessagingDbContext>(Schema);
}