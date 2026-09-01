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
    public override string ModuleVersion => "0.8.1";
    public override string ModuleDescription => "面向 CounterStrikeSharp 的 Lua 5.4 插件宿主";
    public Lua2CSConfig Config { get; set; } = new();

    public void OnConfigParsed(Lua2CSConfig config)
    {
        if (Path.IsPathRooted(config.ScriptsDirectory))
        {
            throw new InvalidDataException("ScriptsDirectory must be relative to the Lua2CS plugin directory.");
        }

        config.ReloadDebounceMilliseconds = Math.Clamp(config.ReloadDebounceMilliseconds, 100, 5000);
        Config = config;
    }

    public override void Load(bool hotReload)
    {
        try
        {
            var scriptsDirectory = ResolveScriptsDirectory();
            _manager = new LuaPluginManager(this, Logger, scriptsDirectory, Config.AllowUnsafeLibraries);
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
                    command.ReplyToCommand("用法：css_lua [status|list|load|reload|unload|reload_all] [脚本名]");
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
            command.ReplyToCommand($"[Lua2CS] {plugin.Key}: {plugin.Name} v{plugin.Version}（{plugin.Registrations.Count} 个注册项）");
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
}
