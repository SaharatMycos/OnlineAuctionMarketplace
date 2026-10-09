using Marketplace.Bootstrap;
using Marketplace.SharedKernel;
using Marketplace.SharedKernel.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Marketplace.ArchitectureTests;

/// <summary>
/// Code-first rules: one DbContext per module, owning only its schema, with migrations that match the model.
/// None of these tests touch a database.
/// </summary>
public class PersistenceTests
{
    public static TheoryData<string> Modules
    {
        get
        {
            var data = new TheoryData<string>();
            foreach (var module in ModuleCatalog.All)
                data.Add(module.Name);
            return data;
        }
    }

    private static IModule Module(string name) => ModuleCatalog.All.Single(m => m.Name == name);

    private static Type DbContextType(IModule module) =>
        Assert.Single(module.GetType().Assembly.GetTypes(), t => t.IsSubclassOf(typeof(ModuleDbContext)) && !t.IsAbstract);

    /// <summary>Builds the context the same way dotnet ef does, through the module's design-time factory.</summary>
    private static ModuleDbContext CreateDbContext(IModule module)
    {
        var contextType = DbContextType(module);
        var factoryInterface = typeof(IDesignTimeDbContextFactory<>).MakeGenericType(contextType);
        var factoryType = Assert.Single(module.GetType().Assembly.GetTypes(), t => factoryInterface.IsAssignableFrom(t) && !t.IsAbstract);
        var factory = Activator.CreateInstance(factoryType, nonPublic: true)!;
        return (ModuleDbContext)factoryInterface.GetMethod("CreateDbContext")!.Invoke(factory, [Array.Empty<string>()])!;
    }

    [Theory, MemberData(nameof(Modules))]
    public void Each_module_has_one_DbContext_with_its_own_schema(string name)
    {
        using var context = CreateDbContext(Module(name));

        Assert.Equal(name, context.Schema);
    }

    [Theory, MemberData(nameof(Modules))]
    public void Every_table_lives_in_the_module_schema(string name)
    {
        using var context = CreateDbContext(Module(name));

        var foreign = context.Model.GetEntityTypes()
            .Where(e => e.GetTableName() is not null && e.GetSchema() != name)
            .Select(e => $"{e.GetSchema()}.{e.GetTableName()} ({e.ClrType.Name})");

        Assert.Empty(foreign);
    }

    [Theory, MemberData(nameof(Modules))]
    public void Every_module_has_an_outbox(string name)
    {
        using var context = CreateDbContext(Module(name));

        Assert.Contains(context.Model.GetEntityTypes(), e => e.GetTableName() == "outbox_message");
    }

    [Theory, MemberData(nameof(Modules))]
    public void Migrations_exist_and_match_the_model(string name)
    {
        using var context = CreateDbContext(Module(name));

        Assert.NotEmpty(context.Database.GetMigrations());
        Assert.False(context.Database.HasPendingModelChanges(),
            $"The {name} model has changes without a migration. Run: dotnet ef migrations add <Name> --project src/Modules/Marketplace.Modules.{Module(name).GetType().Name[..^"Module".Length]} --startup-project src/Marketplace.Api");
    }
}
