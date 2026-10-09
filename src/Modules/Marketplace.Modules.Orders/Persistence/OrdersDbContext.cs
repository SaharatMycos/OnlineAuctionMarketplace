using Marketplace.Modules.Orders.Domain;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Marketplace.Modules.Orders.Persistence;

/// <summary>Code-first model of the "orders" schema.</summary>
internal sealed class OrdersDbContext(DbContextOptions<OrdersDbContext> options) : ModuleDbContext(options)
{
    public const string AgreementNumberSequence = "agreement_number_seq";

    public override string Schema => OrdersModule.Schema;

    public DbSet<Order> Orders => Set<Order>();

    public async Task<string> NextAgreementNumberAsync(CancellationToken ct)
    {
        var next = await Database.SqlQuery<long>($"SELECT nextval('orders.agreement_number_seq') AS \"Value\"").SingleAsync(ct);
        return $"AG-{next}";
    }

    public Task<Order?> FindOrderAsync(Guid id, CancellationToken ct) =>
        Orders.Include(o => o.Agreement).Include(o => o.Events).AsSplitQuery().SingleOrDefaultAsync(o => o.Id == id, ct);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasSequence<long>(AgreementNumberSequence).StartsAt(1001);
    }
}

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> e)
    {
        e.ToTable("order");
        e.HasKey(o => o.Id);
        e.HasIndex(o => o.AuctionId).IsUnique();
        e.HasIndex(o => o.BuyerId);
        e.HasIndex(o => o.SellerId);
        e.Property(o => o.Title).HasMaxLength(80);
        e.Property(o => o.BuyerHandle).HasMaxLength(30);
        e.Property(o => o.SellerHandle).HasMaxLength(30);
        e.Property(o => o.Currency).HasMaxLength(3);
        e.Property(o => o.Delivery).HasConversion<string>().HasMaxLength(10);
        e.Property(o => o.Status).HasConversion<string>().HasMaxLength(20);
        e.Property(o => o.ShippingCost).HasPrecision(12, 2);
        e.Ignore(o => o.Total);
        e.Ignore(o => o.IsAgreed);
        e.HasOne(o => o.Agreement).WithOne().HasForeignKey<DealAgreement>(a => a.OrderId);
        e.HasMany(o => o.Events).WithOne().HasForeignKey(ev => ev.OrderId);
        e.Navigation(o => o.Events).UsePropertyAccessMode(PropertyAccessMode.Property);
    }
}

internal sealed class DealAgreementConfiguration : IEntityTypeConfiguration<DealAgreement>
{
    public void Configure(EntityTypeBuilder<DealAgreement> e)
    {
        e.ToTable("deal_agreement");
        e.HasKey(a => a.Id);
        // Ids are generated in code (Guid v7), so EF must insert, not update, new rows reached through navigations.
        e.Property(a => a.Id).ValueGeneratedNever();
        e.HasIndex(a => a.Number).IsUnique();
        e.Property(a => a.Number).HasMaxLength(20);
        e.Property(a => a.AgreedPrice).HasPrecision(12, 2);
        e.Property(a => a.Handover).HasConversion<string>().HasMaxLength(10);
        e.Property(a => a.PaymentMethod).HasConversion<string>().HasMaxLength(15);
        e.Property(a => a.Notes).HasMaxLength(1000);
        e.Property(a => a.AreaName).HasMaxLength(100);
        // Optimistic concurrency: two parties editing at once can't silently overwrite each other.
        e.Property(a => a.Version).IsConcurrencyToken();
    }
}

internal sealed class DealEventConfiguration : IEntityTypeConfiguration<DealEvent>
{
    public void Configure(EntityTypeBuilder<DealEvent> e)
    {
        e.ToTable("deal_event");
        e.HasKey(ev => ev.Id);
        // Appended through Order.Events: a code-generated key must mean "new row" (see DealAgreement).
        e.Property(ev => ev.Id).ValueGeneratedNever();
        e.HasIndex(ev => new { ev.OrderId, ev.At });
        e.Property(ev => ev.Action).HasMaxLength(30);
        e.Property(ev => ev.Detail).HasMaxLength(500);
    }
}
