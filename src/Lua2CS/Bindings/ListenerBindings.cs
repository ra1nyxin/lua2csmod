using System.Linq.Expressions;
using System.Reflection;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using NLua;

namespace Lua2CS.Bindings;

public sealed class ListenerBindings
{
    private static readonly MethodInfo RegisterMethod = typeof(BasePlugin)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(BasePlugin.RegisterListener) && method.IsGenericMethodDefinition);

    private static readonly MethodInfo RemoveMethod = typeof(BasePlugin)
        .GetMethods(BindingFlags.Instance | BindingFlags.Public)
        .Single(method => method.Name == nameof(BasePlugin.RemoveListener)
                          && method.IsGenericMethodDefinition
                          && method.GetParameters().Length == 1);

    private static readonly IReadOnlyDictionary<string, Type> ListenerTypes = BuildListenerTypes();
    private readonly BasePlugin _host;

    public ListenerBindings(BasePlugin host) => _host = host;

    public void Validate(ListenerRegistration registration)
        => ValidateRegistration(registration);

    internal static void ValidateRegistration(ListenerRegistration registration)
    {
        var type = ResolveListenerType(registration.ListenerName);
        var returnType = type.GetMethod("Invoke")!.ReturnType;
        if (returnType != typeof(void) && returnType != typeof(HookResult))
        {
            throw new NotSupportedException($"Listener {registration.ListenerName} has unsupported return type {returnType.Name}.");
        }
    }

    public IRegistrationHandle Activate(LuaPlugin plugin, ListenerRegistration registration)
    {
        var listenerType = ResolveListenerType(registration.ListenerName);
        var handler = CreateHandler(listenerType, plugin, registration.Callback, registration.ListenerName);
        RegisterMethod.MakeGenericMethod(listenerType).Invoke(_host, [handler]);

        return new RegistrationHandle(registration.Id, () =>
            RemoveMethod.MakeGenericMethod(listenerType).Invoke(_host, [handler]));
    }

    public static IReadOnlyCollection<string> Names => ListenerTypes.Values
        .Select(type => type.GetCustomAttribute<ListenerNameAttribute>()?.Name)
        .OfType<string>()
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private Delegate CreateHandler(Type delegateType, LuaPlugin plugin, LuaFunction callback, string listenerName)
    {
        var invoke = delegateType.GetMethod("Invoke")!;
        var parameters = invoke.GetParameters()
            .Select(parameter => Expression.Parameter(parameter.ParameterType, parameter.Name))
            .ToArray();
        var arguments = Expression.NewArrayInit(typeof(object), parameters.Select(parameter => Expression.Convert(parameter, typeof(object))));

        Expression body;
        if (invoke.ReturnType == typeof(HookResult))
        {
            body = Expression.Call(
                Expression.Constant(this),
                nameof(DispatchReturning),
                Type.EmptyTypes,
                Expression.Constant(plugin),
                Expression.Constant(callback),
                Expression.Constant(listenerName),
                arguments);
        }
        else
        {
            body = Expression.Call(
                Expression.Constant(this),
                nameof(DispatchVoid),
                Type.EmptyTypes,
                Expression.Constant(plugin),
                Expression.Constant(callback),
                Expression.Constant(listenerName),
                arguments);
        }

        return Expression.Lambda(delegateType, body, parameters).Compile();
    }

    public HookResult DispatchReturning(LuaPlugin plugin, LuaFunction callback, string listenerName, object?[] arguments)
    {
        using var mapped = plugin.Api.MapArguments(arguments);
        var result = plugin.Invoke($"监听器 {listenerName}", callback, mapped.Values).FirstOrDefault();
        return plugin.Api.ParseHookResult(result);
    }

    public void DispatchVoid(LuaPlugin plugin, LuaFunction callback, string listenerName, object?[] arguments)
    {
        using var mapped = plugin.Api.MapArguments(arguments);
        plugin.Invoke($"监听器 {listenerName}", callback, mapped.Values);
    }

    private static Type ResolveListenerType(string name)
    {
        var key = Normalize(name);
        return ListenerTypes.TryGetValue(key, out var listenerType)
            ? listenerType
            : throw new InvalidDataException($"Unknown CounterStrikeSharp listener '{name}'.");
    }

    private static IReadOnlyDictionary<string, Type> BuildListenerTypes()
    {
        var result = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (var type in typeof(Listeners).GetNestedTypes(BindingFlags.Public))
        {
            var attribute = type.GetCustomAttribute<ListenerNameAttribute>();
            if (attribute is null) continue;
            result[Normalize(attribute.Name)] = type;
            result[Normalize(type.Name)] = type;
        }
        return result;
    }

    private static string Normalize(string value) => value.Trim().Replace("_", string.Empty).ToLowerInvariant();
}
