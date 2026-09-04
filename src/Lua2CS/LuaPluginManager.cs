using System.Reflection;
using CounterStrikeSharp.API.Core;
using Lua2CS.Bindings;
using Microsoft.Extensions.Logging;

namespace Lua2CS;

public sealed class LuaPluginManager : IDisposable
{
    private static readonly StringComparison PathComparison = OperatingSystem.IsWindows()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private readonly ILogger _logger;
    private readonly string _scriptsDirectory;
    private readonly LuaRuntime _runtime;
    private readonly EventBindings _events;
    private readonly ListenerBindings _listeners;
    private readonly CommandBindings _commands;
    private readonly TimerBindings _timers;
    private readonly FrameBindings _frames;
    private readonly Dictionary<string, LuaPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private bool _disposed;

    public LuaPluginManager(BasePlugin host, ILogger logger, string scriptsDirectory, bool allowUnsafeLibraries)
    {
        _logger = logger;
        _scriptsDirectory = Path.GetFullPath(scriptsDirectory);
        _runtime = new LuaRuntime(logger, allowUnsafeLibraries);
        RuntimeVersion = LuaRuntime.ProbeRuntimeVersion();
        _events = new EventBindings(host);
        _listeners = new ListenerBindings(host);
        _commands = new CommandBindings(host);
        _timers = new TimerBindings(host);
        _frames = new FrameBindings();
        Directory.CreateDirectory(_scriptsDirectory);
    }

    public string ScriptsDirectory => _scriptsDirectory;
    public string RuntimeVersion { get; }
    public IReadOnlyCollection<LuaPlugin> Plugins => _plugins.Values;

    public IReadOnlyList<PluginOperationResult> LoadAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return Directory.EnumerateFiles(_scriptsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(IsStandaloneScript)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => Load(Path.GetFileNameWithoutExtension(path)))
            .ToArray();
    }

    public IReadOnlyList<PluginOperationResult> ReloadAll() => _plugins.Keys
        .Union(
            Directory.EnumerateFiles(_scriptsDirectory, "*")
                .Where(IsStandaloneScript)
                .Select(path => Path.GetFileNameWithoutExtension(path)!),
            StringComparer.OrdinalIgnoreCase)
        .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
        .Select(Reload)
        .ToArray();

    public PluginOperationResult Load(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        key = NormalizeKey(key);
        if (_plugins.ContainsKey(key)) return Reload(key);

        var path = ResolveScriptPath(_scriptsDirectory, key);
        if (!File.Exists(path)) return PluginOperationResult.Fail(key, "脚本文件不存在。");

        LuaPlugin? candidate = null;
        try
        {
            candidate = _runtime.Prepare(path);
            Validate(candidate, null);
            candidate.Activate(
                definition => Activate(candidate, definition),
                definition => ValidateAndActivate(candidate, definition, null));
            candidate.InvokeLifecycle(candidate.LoadCallback, false);
            _plugins.Add(key, candidate);
            _logger.LogInformation("Loaded Lua plugin {Name} v{Version} from {File}", candidate.Name, candidate.Version, Path.GetFileName(path));
            return PluginOperationResult.Ok(key, $"已加载 {candidate.Name} v{candidate.Version}。");
        }
        catch (Exception exception)
        {
            candidate?.Dispose();
            exception = Unwrap(exception);
            _logger.LogError(exception, "Failed to load Lua script {Script}", key);
            return PluginOperationResult.Fail(key, exception.Message);
        }
    }

    public PluginOperationResult Reload(string key)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        key = NormalizeKey(key);
        if (!_plugins.TryGetValue(key, out var current)) return Load(key);

        var path = ResolveScriptPath(_scriptsDirectory, key);
        if (!File.Exists(path)) return Unload(key);

        LuaPlugin? candidate = null;
        try
        {
            candidate = _runtime.Prepare(path);
            Validate(candidate, current);
        }
        catch (Exception exception)
        {
            candidate?.Dispose();
            exception = Unwrap(exception);
            _logger.LogError(exception, "Lua reload validation failed for {Script}; keeping the old version", key);
            return PluginOperationResult.Fail(key, $"重载被拒绝，旧版本仍在运行：{exception.Message}");
        }

        current.Deactivate();
        try
        {
            candidate.Activate(
                definition => Activate(candidate, definition),
                definition => ValidateAndActivate(candidate, definition, current));
            candidate.InvokeLifecycle(candidate.LoadCallback, true);
        }
        catch (Exception exception)
        {
            candidate.Dispose();
            exception = Unwrap(exception);
            try
            {
                current.Activate(
                    definition => Activate(current, definition),
                    definition => ValidateAndActivate(current, definition, null));
            }
            catch (Exception rollbackException)
            {
                _plugins.Remove(key);
                current.Dispose();
                _logger.LogCritical(rollbackException, "Failed to restore Lua plugin {Script} after a rejected reload", key);
                return PluginOperationResult.Fail(key, $"重载和回滚均失败：{exception.Message}");
            }

            _logger.LogError(exception, "Lua reload activation failed for {Script}; restored the old version", key);
            return PluginOperationResult.Fail(key, $"重载失败，已恢复旧版本：{exception.Message}");
        }

        try
        {
            current.InvokeLifecycle(current.UnloadCallback, true);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Old Lua plugin {Script} failed during on_unload", key);
        }

        current.Dispose();
        _plugins[key] = candidate;
        _logger.LogInformation("Reloaded Lua plugin {Name} v{Version}", candidate.Name, candidate.Version);
        return PluginOperationResult.Ok(key, $"已重载 {candidate.Name} v{candidate.Version}。");
    }

    public PluginOperationResult Unload(string key, bool hotReload = false)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        key = NormalizeKey(key);
        if (!_plugins.Remove(key, out var plugin)) return PluginOperationResult.Fail(key, "插件尚未加载。");

        try
        {
            plugin.InvokeLifecycle(plugin.UnloadCallback, hotReload);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Lua plugin {Script} failed during on_unload", key);
        }
        finally
        {
            plugin.Dispose();
        }

        _logger.LogInformation("Unloaded Lua plugin {Name}", plugin.Name);
        return PluginOperationResult.Ok(key, $"已卸载 {plugin.Name}。");
    }

    public void RefreshFiles(IReadOnlyCollection<string> changedPaths)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        var reloadAll = changedPaths.Any(path => IsModulePath(path, _scriptsDirectory));

        if (reloadAll)
        {
            ReloadAll();
            return;
        }

        foreach (var key in changedPaths.Select(Path.GetFileNameWithoutExtension).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            Reload(key!);
        }
    }

    public static string NormalizeKey(string key)
    {
        key = Path.GetFileNameWithoutExtension(key.Trim());
        if (string.IsNullOrEmpty(key) || key.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || key is "." or "..")
        {
            throw new ArgumentException("Invalid Lua plugin name.", nameof(key));
        }
        return key;
    }

    internal static bool IsStandaloneScript(string path) =>
        !Path.GetFileName(path).StartsWith('_') && path.EndsWith(".lua", StringComparison.OrdinalIgnoreCase);

    internal static bool IsModulePath(string path, string scriptsDirectory)
    {
        path = Path.GetFullPath(path);
        scriptsDirectory = Path.GetFullPath(scriptsDirectory);
        return !Path.GetDirectoryName(path)!.Equals(scriptsDirectory, PathComparison)
               || !IsStandaloneScript(path);
    }

    public void Dispose()
    {
        Shutdown(false);
    }

    public void Shutdown(bool hotReload)
    {
        if (_disposed) return;
        foreach (var key in _plugins.Keys.ToArray()) Unload(key, hotReload);
        _disposed = true;
    }

    private void Validate(LuaPlugin candidate, LuaPlugin? replacing)
    {
        LuaRegistrationValidator.Validate(candidate.Registrations);

        var commands = candidate.Registrations
            .OfType<CommandRegistration>()
            .Select(command => command.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var existingCommands = _plugins.Values
            .Where(plugin => !ReferenceEquals(plugin, candidate) && !ReferenceEquals(plugin, replacing))
            .SelectMany(plugin => plugin.Registrations)
            .OfType<CommandRegistration>()
            .Select(command => command.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var conflict = commands.FirstOrDefault(existingCommands.Contains);
        if (conflict is not null) throw new InvalidDataException($"Lua command '{conflict}' is already registered by another Lua plugin.");
    }

    private IRegistrationHandle Activate(LuaPlugin plugin, RegistrationDefinition definition) => definition switch
    {
        EventRegistration gameEvent => _events.Activate(plugin, gameEvent),
        ListenerRegistration listener => _listeners.Activate(plugin, listener),
        CommandRegistration command => _commands.Activate(plugin, command),
        CommandListenerRegistration commandListener => _commands.Activate(plugin, commandListener),
        TimerRegistration timer => _timers.Activate(plugin, timer),
        FrameRegistration frame => _frames.Activate(plugin, frame),
        _ => throw new NotSupportedException($"Unsupported Lua registration {definition.GetType().Name}.")
    };

    private IRegistrationHandle ValidateAndActivate(
        LuaPlugin plugin,
        RegistrationDefinition definition,
        LuaPlugin? replacing)
    {
        Validate(plugin, replacing);
        return Activate(plugin, definition);
    }

    internal static string ResolveScriptPath(string scriptsDirectory, string key)
    {
        scriptsDirectory = Path.GetFullPath(scriptsDirectory);
        var path = Path.GetFullPath(Path.Combine(scriptsDirectory, key + ".lua"));
        if (!Path.GetDirectoryName(path)!.Equals(scriptsDirectory, PathComparison))
        {
            throw new InvalidOperationException("Script path escapes the configured scripts directory.");
        }

        if (File.Exists(path)) return path;
        return Directory.EnumerateFiles(scriptsDirectory, "*", SearchOption.TopDirectoryOnly)
                   .Where(IsStandaloneScript)
                   .FirstOrDefault(candidate => Path.GetFileNameWithoutExtension(candidate)
                       .Equals(key, StringComparison.OrdinalIgnoreCase))
               ?? path;
    }

    private static Exception Unwrap(Exception exception)
    {
        while (exception is TargetInvocationException { InnerException: not null }) exception = exception.InnerException;
        return exception;
    }
}

public sealed record PluginOperationResult(string Key, bool Success, string Message)
{
    public static PluginOperationResult Ok(string key, string message) => new(key, true, message);
    public static PluginOperationResult Fail(string key, string message) => new(key, false, message);
}
