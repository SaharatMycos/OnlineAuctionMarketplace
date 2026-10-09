using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Marketplace.Modules.Admin.Persistence;

/// <summary>
/// Code-first model of the "admin" schema. Add DbSets here and IEntityTypeConfiguration classes
/// anywhere in this assembly, then: dotnet ef migrations add &lt;Name&gt; (see the skill's "Add a migration").
/// </summary>
internal sealed class AdminDbContext(DbContextOptions<AdminDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => AdminModule.Schema;
}