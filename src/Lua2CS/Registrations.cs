using CounterStrikeSharp.API.Core;
using Lua2CS.Bindings;
using NLua;

namespace Lua2CS;

public abstract record RegistrationDefinition(long Id);

public sealed record EventRegistration(
    long Id,
    string EventName,
    HookMode Mode,
    LuaFunction Callback) : RegistrationDefinition(Id);

public sealed record ListenerRegistration(
    long Id,
    string ListenerName,
    LuaFunction Callback) : RegistrationDefinition(Id);

public sealed record CommandRegistration(
    long Id,
    string Name,
    string Description,
    string Permission,
    bool AllowConsole,
    int MinArgs,
    string Usage,
    LuaFunction Callback) : RegistrationDefinition(Id);

public sealed record CommandListenerRegistration(
    long Id,
    string Name,
    HookMode Mode,
    LuaFunction Callback) : RegistrationDefinition(Id);

public sealed record TimerRegistration(
    long Id,
    float Interval,
    bool Repeat,
    bool StopOnMapChange,
    LuaFunction Callback) : RegistrationDefinition(Id);

public enum FrameSchedule
{
    NextFrame,
    NextWorldUpdate,
    AfterTicks
}

public sealed record FrameRegistration(
    long Id,
    FrameSchedule Schedule,
    int TickDelay,
    LuaFunction Callback) : RegistrationDefinition(Id);

public interface IRegistrationHandle : IDisposable
{
    long Id { get; }
}

internal sealed class RegistrationHandle(long id, Action dispose) : IRegistrationHandle
{
    private Action? _dispose = dispose;

    public long Id { get; } = id;

    public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
}

internal static class LuaRegistrationValidator
{
    internal static void Validate(IEnumerable<RegistrationDefinition> registrations)
    {
        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var registration in registrations)
        {
            switch (registration)
            {
                case EventRegistration gameEvent:
                    EventBindings.ValidateRegistration(gameEvent);
                    break;
                case ListenerRegistration listener:
                    ListenerBindings.ValidateRegistration(listener);
                    break;
                case CommandRegistration command:
                    CommandBindings.ValidateRegistration(command);
                    if (!commands.Add(command.Name))
                    {
                        throw new InvalidDataException($"Lua 命令 '{command.Name}' 在同一脚本中重复注册。");
                    }
                    break;
                case CommandListenerRegistration commandListener:
                    CommandBindings.ValidateRegistration(commandListener);
                    break;
                case TimerRegistration timer:
                    TimerBindings.ValidateRegistration(timer);
                    break;
                case FrameRegistration frame:
                    FrameBindings.ValidateRegistration(frame);
                    break;
                default:
                    throw new NotSupportedException($"不支持的 Lua 注册类型 {registration.GetType().Name}。");
            }
        }
    }
}
