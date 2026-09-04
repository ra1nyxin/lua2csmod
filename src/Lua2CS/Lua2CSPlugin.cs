using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using Microsoft.Extensions.Logging;
using System.Runtime.InteropServices;

namespace Lua2CS;

[MinimumApiVersion(373)]
public sealed class Lua2CSPlugin : BasePlugin, IPluginConfig<Lua2CSConfig>
{
    private LuaPluginManager? _manager;
    private HotReload? _hotReload;

    public override string ModuleName => "Lua2CS";
    public override string ModuleVersion => "0.9.0";
    public override string ModuleDescription => "面向 CounterStrikeSharp 的 Lua 5.4 插件宿主";
    public Lua2CSConfig Config { get; set; } = new();

    public void OnConfigParsed(Lua2CSConfig config)
    {
        if (Path.IsPathRooted(config.ScriptsDirectory))
        {
            throw new InvalidDataException("ScriptsDirectory must be relative to the Lua2CS plugin directory.");
        }

        config.ReloadDebounceMilliseconds = Math.Clamp(config.ReloadDebounceMilliseconds, 100, 5000);
        config.SlowCallbackMilliseconds = Math.Clamp(config.SlowCallbackMilliseconds, 1, 5000);
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        try
        {
            var scriptsDirectory = ResolveScriptsDirectory();
            _manager = new LuaPluginManager(
                this,
                Logger,
                scriptsDirectory,
                Config.AllowUnsafeLibraries,
                Config.SlowCallbackMilliseconds);
            AddCommand("css_lua", "管理 Lua2CS 脚本", OnLuaCommand);

            foreach (var result in _manager.LoadAll().Where(result => !result.Success))
            {
                Logger.LogError("Lua startup load failed for {Script}: {Message}", result.Key, result.Message);
            }

            if (Config.AutoReload)
            {
                _hotReload = new HotReload(_manager, Logger, Config.ReloadDebounceMilliseconds, Server.NextWorldUpdate);
            }

            Logger.LogInformation(
                "Lua2CS loaded with {Runtime}, CounterStrikeSharp {CssVersion}, and {Count} Lua plugin(s) from {Directory}",
                _manager.RuntimeVersion,
                Api.GetVersionString(),
                _manager.Plugins.Count,
                scriptsDirectory);
        }
        catch
        {
            try
            {
                Cleanup(hotReload);
            }
            catch (Exception cleanupException)
            {
                Logger.LogError(cleanupException, "Lua2CS startup cleanup failed");
            }
            throw;
        }
    }

    public override void Unload(bool hotReload) => Cleanup(hotReload);

    private void OnLuaCommand(CCSPlayerController? player, CommandInfo command)
    {
        if (_manager is null) return;
        if (player is not null && !string.IsNullOrWhiteSpace(Config.AdminPermission)
                               && !AdminManager.PlayerHasPermissions(player, Config.AdminPermission))
        {
            command.ReplyToCommand("你没有管理 Lua 插件的权限。");
            return;
        }

        var action = command.ArgCount > 1 ? command.GetArg(1).Trim().ToLowerInvariant() : "list";
        try
        {
            switch (action)
            {
                case "status":
                case "doctor":
                    ReplyWithStatus(command);
                    break;
                case "list":
                    ReplyWithPluginList(command);
                    break;
                case "inspect":
                    ReplyWithPluginInspection(command, command.ArgCount > 2 ? command.GetArg(2) : null);
                    break;
                case "errors":
                    ReplyWithFailures(command, command.ArgCount > 2 ? command.GetArg(2) : null);
                    break;
                case "load":
                    Reply(command, command.ArgCount > 2 ? _manager.Load(command.GetArg(2)) : MissingName());
                    break;
                case "reload":
                case "restart":
                    Reply(command, command.ArgCount > 2 ? _manager.Reload(command.GetArg(2)) : MissingName());
                    break;
                case "unload":
                case "stop":
                    Reply(command, command.ArgCount > 2 ? _manager.Unload(command.GetArg(2)) : MissingName());
                    break;
                case "reload_all":
                    foreach (var result in _manager.ReloadAll()) Reply(command, result);
                    break;
                default:
                    command.ReplyToCommand("用法：css_lua [status|list|inspect|errors|load|reload|unload|reload_all] [脚本名]");
                    break;
            }
        }
        catch (Exception exception)
        {
            Logger.LogError(exception, "Lua management command failed");
            command.ReplyToCommand($"[Lua2CS] 错误：{exception.Message}");
        }
    }

    private void ReplyWithStatus(CommandInfo command)
    {
        command.ReplyToCommand(
            $"[Lua2CS] Lua2CS v{ModuleVersion} | {_manager!.RuntimeVersion} | CounterStrikeSharp {Api.GetVersionString()}");
        command.ReplyToCommand(
            $"[Lua2CS] 平台 {RuntimeInformation.RuntimeIdentifier} | 自动重载 {(Config.AutoReload ? "已开启" : "已关闭")} | 已加载 {_manager.Plugins.Count} 个 Lua 插件");
        var diagnostics = _manager.CallbackDiagnostics;
        command.ReplyToCommand(
            $"[Lua2CS] 回调 {diagnostics.InvocationCount} 次 | 失败 {diagnostics.FailureCount} 次 | 慢回调 {diagnostics.SlowCallbackCount} 次 | 平均 {diagnostics.AverageMilliseconds:F2} ms | 最大 {diagnostics.MaximumMilliseconds:F2} ms");
        command.ReplyToCommand($"[Lua2CS] 慢回调阈值：{Config.SlowCallbackMilliseconds} ms | 最近操作失败：{_manager.OperationFailures.Count} 条");
        command.ReplyToCommand($"[Lua2CS] 脚本目录：{_manager.ScriptsDirectory}");
    }

    private void ReplyWithPluginList(CommandInfo command)
    {
        if (_manager!.Plugins.Count == 0)
        {
            command.ReplyToCommand("[Lua2CS] 当前没有已加载的 Lua 插件。");
            return;
        }

        foreach (var plugin in _manager.Plugins.OrderBy(plugin => plugin.Key, StringComparer.OrdinalIgnoreCase))
        {
            var diagnostics = plugin.Diagnostics;
            command.ReplyToCommand(
                $"[Lua2CS] {plugin.Key}: {plugin.Name} v{plugin.Version}（{plugin.Registrations.Count} 个注册项，回调 {diagnostics.InvocationCount}，失败 {diagnostics.FailureCount}，慢 {diagnostics.SlowCallbackCount}，最大 {diagnostics.MaximumMilliseconds:F2} ms）");
        }
    }

    private void ReplyWithPluginInspection(CommandInfo command, string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            command.ReplyToCommand("用法：css_lua inspect <脚本名>");
            return;
        }

        var plugin = _manager!.FindPlugin(key);
        if (plugin is null)
        {
            command.ReplyToCommand("[Lua2CS] 指定脚本尚未加载。可使用 css_lua list 查看。");
            return;
        }

        var diagnostics = plugin.Diagnostics;
        command.ReplyToCommand(
            $"[Lua2CS] {plugin.Key}: {plugin.Name} v{plugin.Version} | {plugin.Registrations.Count} 个注册项 | 回调 {diagnostics.InvocationCount} 次，失败 {diagnostics.FailureCount} 次，慢 {diagnostics.SlowCallbackCount} 次");
        command.ReplyToCommand(
            $"[Lua2CS] 总耗时 {diagnostics.TotalMilliseconds:F2} ms | 平均 {diagnostics.AverageMilliseconds:F2} ms | 最大 {diagnostics.MaximumMilliseconds:F2} ms");
        if (diagnostics.LastFailureAt is not null)
        {
            command.ReplyToCommand(
                $"[Lua2CS] 最近回调异常：{FormatTime(diagnostics.LastFailureAt.Value)} | {diagnostics.LastFailureSource} | {diagnostics.LastFailureMessage}");
        }
        if (diagnostics.LastSlowAt is not null)
        {
            command.ReplyToCommand(
                $"[Lua2CS] 最近慢回调：{FormatTime(diagnostics.LastSlowAt.Value)} | {diagnostics.LastSlowSource} | {diagnostics.LastSlowMilliseconds:F2} ms");
        }
    }

    private void ReplyWithFailures(CommandInfo command, string? key)
    {
        var failures = _manager!.OperationFailures
            .Where(failure => string.IsNullOrWhiteSpace(key) || failure.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(failure => failure.OccurredAt)
            .ToArray();
        if (failures.Length == 0)
        {
            command.ReplyToCommand(string.IsNullOrWhiteSpace(key)
                ? "[Lua2CS] 当前进程内没有加载或热重载失败记录。"
                : "[Lua2CS] 指定脚本当前进程内没有加载或热重载失败记录。");
            return;
        }

        foreach (var failure in failures)
        {
            command.ReplyToCommand(
                $"[Lua2CS] {FormatTime(failure.OccurredAt)} | {failure.Key} | {failure.Operation} | {failure.Message}");
        }
    }

    private string ResolveScriptsDirectory()
    {
        var moduleDirectory = Path.GetFullPath(ModuleDirectory);
        var scriptsDirectory = Path.GetFullPath(Path.Combine(moduleDirectory, Config.ScriptsDirectory));
        var pathComparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!scriptsDirectory.StartsWith(moduleDirectory + Path.DirectorySeparatorChar, pathComparison))
        {
            throw new InvalidDataException("ScriptsDirectory escapes the Lua2CS plugin directory.");
        }
        return scriptsDirectory;
    }

    private void Cleanup(bool hotReload)
    {
        var reloadService = _hotReload;
        var manager = _manager;
        _hotReload = null;
        _manager = null;
        try
        {
            reloadService?.Dispose();
        }
        finally
        {
            manager?.Shutdown(hotReload);
        }
    }

    private static void Reply(CommandInfo command, PluginOperationResult result) =>
        command.ReplyToCommand($"[Lua2CS] {(result.Success ? "成功" : "失败")}：{result.Message}");

    private static PluginOperationResult MissingName() => PluginOperationResult.Fail(string.Empty, "必须提供脚本名。");

    private static string FormatTime(DateTimeOffset time) => time.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");
}
