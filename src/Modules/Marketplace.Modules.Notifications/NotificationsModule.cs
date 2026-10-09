using Marketplace.Modules.Notifications.Persistence;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Marketplace.Modules.Notifications;

/// <summary>Notifications: email, push and in-app notifications (feature design §10).</summary>
public sealed class NotificationsModule : IModule
{
    public const string Schema = "notifications";

    public string Name => Schema;

    public void AddServices(IServiceCollection services, IConfiguration configuration) =>
        services.AddModuleDbContext<NotificationsDbContext>(Schema);
}