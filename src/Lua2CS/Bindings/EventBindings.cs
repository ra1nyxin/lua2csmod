using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Events;
using NLua;

namespace Lua2CS.Bindings;

public sealed class EventBindings
{
    private static readonly MethodInfo RegisterMethod = typeof(BasePlugin)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(BasePlugin.RegisterEventHandler) && method.IsGenericMethodDefinition);

    private static readonly MethodInfo DeregisterMethod = typeof(BasePlugin)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(BasePlugin.DeregisterEventHandler)
                          && method.IsGenericMethodDefinition
                          && method.GetParameters().Length == 2);

    private static readonly IReadOnlyDictionary<string, Type> EventTypes = BuildEventTypes();
    private readonly BasePlugin _host;

    public EventBindings(BasePlugin host) => _host = host;

    public void Validate(EventRegistration registration) => ValidateRegistration(registration);

    internal static void ValidateRegistration(EventRegistration registration) => ResolveEventType(registration.EventName);

    public IRegistrationHandle Activate(LuaPlugin plugin, EventRegistration registration)
    {
        var eventType = ResolveEventType(registration.EventName);
        var handler = CreateHandler(eventType, plugin, registration.Callback, registration.Mode);
        RegisterMethod.MakeGenericMethod(eventType).Invoke(_host, [handler, registration.Mode]);

        return new RegistrationHandle(registration.Id, () =>
            DeregisterMethod.MakeGenericMethod(eventType).Invoke(_host, [handler, registration.Mode]));
    }

    public static IReadOnlyCollection<string> Names => EventTypes.Values
        .Select(type => type.GetCustomAttribute<EventNameAttribute>()?.Name)
        .OfType<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private Delegate CreateHandler(Type eventType, LuaPlugin plugin, LuaFunction callback, HookMode mode)
    {
        var method = GetType().GetMethod(nameof(CreateTypedHandler), BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Delegate)method.MakeGenericMethod(eventType).Invoke(this, [plugin, callback, mode])!;
    }

    private BasePlugin.GameEventHandler<T> CreateTypedHandler<T>(LuaPlugin plugin, LuaFunction callback, HookMode mode)
        where T : GameEvent
    {
        return (gameEvent, info) =>
        {
            using var snapshot = plugin.Api.CreateEventSnapshot(gameEvent, info, mode == HookMode.Pre);
            var result = plugin.Invoke(callback, snapshot.Event, snapshot.Info).FirstOrDefault();
            snapshot.Apply();
            return plugin.Api.ParseHookResult(result);
        };
    }

    private static Type ResolveEventType(string name)
    {
        var key = Normalize(name);
        return EventTypes.TryGetValue(key, out var eventType)
            ? eventType
            : throw new InvalidDataException($"Unknown CounterStrikeSharp event '{name}'.");
    }

    private static IReadOnlyDictionary<string, Type> BuildEventTypes()
    {
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(GameEvent).Assembly.GetTypes().Where(type => type.IsSubclassOf(typeof(GameEvent))))
        {
            var attribute = type.GetCustomAttribute<EventNameAttribute>();
            if (attribute is null) continue;
            result[Normalize(attribute.Name)] = type;
            result[Normalize(type.Name)] = type;
        }
        return result;
    }

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty).ToLowerInvariant();
}
