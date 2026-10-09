using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Messaging.Persistence;

/// <summary>
/// Code-first model of the "messaging" schema. Add DbSets here and IEntityTypeConfiguration classes
/// anywhere in this assembly, then: dotnet ef migrations add &lt;Name&gt; (see the skill's "Add a migration").
/// </summary>
internal sealed class MessagingDbContext(DbContextOptions<MessagingDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => MessagingModule.Schema;
}