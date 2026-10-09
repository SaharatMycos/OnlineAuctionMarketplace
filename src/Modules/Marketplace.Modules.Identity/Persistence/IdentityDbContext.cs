using Marketplace.Modules.Identity.Domain;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Modules.Identity.Persistence;

/// <summary>Code-first model of the "identity" schema.</summary>
internal sealed class IdentityDbContext(DbContextOptions<IdentityDbContext> options) : ModuleDbContext(options)
{
    public override string Schema => IdentityModule.Schema;

    public DbSet<User> Users => Set<User>();
}

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> e)
    {
        e.ToTable("user");
        e.HasKey(u => u.Id);
        e.Property(u => u.Email).HasMaxLength(320);
        e.Property(u => u.DisplayName).HasMaxLength(100);
        e.Property(u => u.Handle).HasMaxLength(30);
        e.HasIndex(u => u.Email).IsUnique();
        e.HasIndex(u => u.Handle).IsUnique();
    }
}
