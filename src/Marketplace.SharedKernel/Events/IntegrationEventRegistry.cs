using System.Reflection;

namespace Marketplace.SharedKernel.Events;

/// <summary>Maps wire names to event types, discovered from the given assemblies.</summary>
public sealed class IntegrationEventRegistry
{
    private readonly Dictionary<string, Type> _byName = new(StringComparer.Ordinal);

    public IntegrationEventRegistry(IEnumerable<Assembly> assemblies)
    {
        foreach (var type in assemblies.Distinct().SelectMany(a => a.GetTypes()))
        {
            if (type.GetCustomAttribute<IntegrationEventAttribute>() is not { } attribute)
                continue;
            if (!typeof(IIntegrationEvent).IsAssignableFrom(type))
                throw new InvalidOperationException($"{type} has [IntegrationEvent] but doesn't implement IIntegrationEvent.");
            if (!_byName.TryAdd(attribute.Name, type))
                throw new InvalidOperationException($"Integration event name '{attribute.Name}' is used by {_byName[attribute.Name]} and {type}.");
        }
    }

    public IReadOnlyCollection<string> Names => _byName.Keys;

    public Type? Resolve(string name) => _byName.GetValueOrDefault(name);

    public static string NameOf(Type eventType) =>
        eventType.GetCustomAttribute<IntegrationEventAttribute>()?.Name
        ?? throw new InvalidOperationException($"{eventType} is missing [IntegrationEvent(\"module.event_name.v1\")].");
}
