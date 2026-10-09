using Marketplace.Modules.Admin.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Admin;

/// <summary>Admin: moderation, user management, problem-report queue, fee configuration (feature design §15).</summary>
public sealed class AdminModule : IModule
{
    public const string Schema = "admin";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<AdminDbContext>(Schema);
}