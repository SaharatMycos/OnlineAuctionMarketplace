using Marketplace.Bootstrap;
using Marketplace.SharedKernel;

namespace Marketplace.ArchitectureTests;

/// <summary>Enforces the module rules from system design §2 in CI.</summary>
public class ModuleBoundaryTests
{
    private static readonly HashSet<string> ModuleAssemblies =
        ModuleCatalog.All.Select(m => m.GetType().Assembly.GetName().Name!).ToHashSet();

    [Fact]
    public void Modules_do_not_reference_other_modules()
    {
        // A module may later reference another module's *.Contracts assembly, never its implementation.
        var violations =
            from module in ModuleCatalog.All
            let assembly = module.GetType().Assembly
            from reference in assembly.GetReferencedAssemblies()
            where ModuleAssemblies.Contains(reference.Name!) && reference.Name != assembly.GetName().Name
            select $"{assembly.GetName().Name} -> {reference.Name}";

        Assert.Empty(violations);
    }

    [Fact]
    public void Contracts_reference_only_the_shared_kernel()
    {
        var contracts = MarketplaceSetup.EventAssemblies()
            .Where(a => a.GetName().Name!.EndsWith(".Contracts", StringComparison.Ordinal))
            .ToList();
        Assert.NotEmpty(contracts);

        var violations =
            from assembly in contracts
            from reference in assembly.GetReferencedAssemblies()
            where reference.Name!.StartsWith("Marketplace.", StringComparison.Ordinal) && reference.Name != "Marketplace.SharedKernel"
            select $"{assembly.GetName().Name} -> {reference.Name}";

        Assert.Empty(violations);
    }

    [Fact]
    public void Integration_event_names_are_unique_and_versioned()
    {
        var registry = new Marketplace.SharedKernel.Events.IntegrationEventRegistry(MarketplaceSetup.EventAssemblies());
        Assert.NotEmpty(registry.Names);
        Assert.All(registry.Names, name => Assert.Matches(@"^[a-z]+\.[a-z_]+\.v\d+$", name));
    }

    [Fact]
    public void Shared_kernel_does_not_reference_modules()
    {
        var references = typeof(IModule).Assembly.GetReferencedAssemblies()
            .Select(r => r.Name!)
            .Where(n => n.StartsWith("Marketplace.Modules.", StringComparison.Ordinal) || n == "Marketplace.Bootstrap");

        Assert.Empty(references);
    }

    [Fact]
    public void Each_module_lives_in_its_own_assembly()
    {
        Assert.Equal(ModuleCatalog.All.Count, ModuleAssemblies.Count);
    }

    [Fact]
    public void Module_names_are_unique_lowercase_schema_names()
    {
        var names = ModuleCatalog.All.Select(m => m.Name).ToList();

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, n => Assert.Matches("^[a-z][a-z_]*$", n));
        Assert.DoesNotContain("public", names);
    }
}
