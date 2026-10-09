using Marketplace.SharedKernel.Persistence;

namespace Marketplace.Modules.Notifications.Persistence;

/// <summary>Lets dotnet ef build <see cref="NotificationsDbContext"/> at design time.</summary>
internal sealed class NotificationsDbContextFactory : ModuleDesignTimeFactory<NotificationsDbContext>
{
    protected override string Schema => NotificationsModule.Schema;
}