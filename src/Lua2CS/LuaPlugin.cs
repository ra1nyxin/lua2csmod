using CounterStrikeSharp.API.Core;
using Microsoft.Extensions.Logging;
using NLua;

namespace Lua2CS;

public sealed class LuaPlugin : IDisposable
{
    private readonly ILogger _logger;
    private readonly List<RegistrationDefinition> _registrations = [];
    private readonly Dictionary<long, IRegistrationHandle> _handles = [];
    private bool _disposed;
    private long _nextRegistrationId;

    internal LuaPlugin(string scriptPath, Lua state, ILogger logger)
    {
        ScriptPath = scriptPath;
        State = state;
        _logger = logger;
    }

    public string ScriptPath { get; }
    public string Key => Path.GetFileNameWithoutExtension(ScriptPath);
    public string Name { get; internal set; } = string.Empty;
    public string Version { get; internal set; } = "0.0.0";
    public string Description { get; internal set; } = string.Empty;
    public bool IsActive { get; private set; }
    public IReadOnlyList<RegistrationDefinition> Registrations => _registrations;
    internal Lua State { get; }
    internal LuaFunction? LoadCallback { get; set; }
    internal LuaFunction? UnloadCallback { get; set; }
    internal LuaApi Api { get; set; } = null!;
    internal Func<RegistrationDefinition, IRegistrationHandle>? ActivateRegistration { get; set; }

    internal long NextRegistrationId() => Interlocked.Increment(ref _nextRegistrationId);

    internal void AddRegistration(RegistrationDefinition definition)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _registrations.Add(definition);

        if (IsActive && ActivateRegistration is not null)
        {
            try
            {
                _handles.Add(definition.Id, ActivateRegistration(definition));
            }
            catch
            {
                _registrations.Remove(definition);
                throw;
            }
        }
    }

    internal bool RemoveRegistration(long id)
    {
        var definition = _registrations.FirstOrDefault(item => item.Id == id);
        if (definition is null)
        {
            return false;
        }

        _registrations.Remove(definition);
        if (_handles.Remove(id, out var handle))
        {
            handle.Dispose();
        }

        return true;
    }

    internal void Activate(
        Func<RegistrationDefinition, IRegistrationHandle> activator,
        Func<RegistrationDefinition, IRegistrationHandle>? dynamicActivator = null)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (IsActive)
        {
            return;
        }

        ActivateRegistration = activator;
        try
        {
            foreach (var definition in _registrations)
            {
                _handles.Add(definition.Id, activator(definition));
            }

            IsActive = true;
            ActivateRegistration = dynamicActivator ?? activator;
        }
        catch
        {
            Deactivate();
            throw;
        }
    }

    internal void Deactivate()
    {
        IsActive = false;
        ActivateRegistration = null;

        foreach (var handle in _handles.Values.Reverse())
        {
            try
            {
                handle.Dispose();
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to remove registration {RegistrationId} from {Plugin}", handle.Id, Name);
            }
        }

        _handles.Clear();
    }

    internal object?[] Invoke(LuaFunction callback, params object?[] args)
    {
        if (_disposed)
        {
            return [];
        }

        try
        {
            return callback.Call(args) ?? [];
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lua callback failed in {Plugin}", Name);
            var rootCause = exception.GetBaseException();
            if (!ReferenceEquals(rootCause, exception))
            {
                _logger.LogError(rootCause, "Lua callback root cause in {Plugin}", Name);
            }
            return [];
        }
    }

    internal object?[] InvokeOrThrow(LuaFunction callback, params object?[] args)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return callback.Call(args) ?? [];
    }

    internal void InvokeLifecycle(LuaFunction? callback, bool hotReload)
    {
        if (callback is null)
        {
            return;
        }

        var results = InvokeOrThrow(callback, hotReload);
        if (results.Length > 0 && results[0] is bool success && !success)
        {
            throw new InvalidOperationException($"Lua lifecycle callback rejected activation for {Name}.");
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Deactivate();
        _disposed = true;
        // Prepare 在 LuaApi 完成初始化前失败时仍要释放 Lua VM，不能让清理异常覆盖原始校验错误。
        if (Api is not null) Api.Dispose();
        State.Dispose();
    }
}
