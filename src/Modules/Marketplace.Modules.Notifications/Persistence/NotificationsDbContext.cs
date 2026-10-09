using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Notifications.Persistence;

/// <summary>
/// Code-first model of the "notifications" schema. Add DbSets here and IEntityTypeConfiguration classes
/// anywhere in this assembly, then: dotnet ef migrations add &lt;Name&gt; (see the skill's "Add a migration").
/// </summary>
internal sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => NotificationsModule.Schema;
}