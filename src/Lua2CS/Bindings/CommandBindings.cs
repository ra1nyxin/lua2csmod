using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;

namespace Lua2CS.Bindings;

public sealed class CommandBindings(BasePlugin host)
{
    public void Validate(CommandRegistration registration) => ValidateRegistration(registration);

    internal static void ValidateRegistration(CommandRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.Name) || registration.Name.Any(char.IsWhiteSpace))
        {
            throw new InvalidDataException("Lua command names must be non-empty and cannot contain whitespace.");
        }

        if (registration.MinArgs < 0)
        {
            throw new InvalidDataException($"Command {registration.Name} has a negative min_args value.");
        }
    }

    public void Validate(CommandListenerRegistration registration) => ValidateRegistration(registration);

    internal static void ValidateRegistration(CommandListenerRegistration registration)
    {
        if (string.IsNullOrWhiteSpace(registration.Name) || registration.Name.Any(char.IsWhiteSpace))
        {
            throw new InvalidDataException("Lua command listener names must be non-empty and cannot contain whitespace.");
        }
    }

    public IRegistrationHandle Activate(LuaPlugin plugin, CommandRegistration registration)
    {
        CommandInfo.CommandCallback handler = (player, command) =>
        {
            if (player is null && !registration.AllowConsole)
            {
                command.ReplyToCommand("此命令只能由游戏内玩家执行。");
                return;
            }

            if (player is not null && !string.IsNullOrEmpty(registration.Permission)
                                   && !AdminManager.PlayerHasPermissions(player, registration.Permission))
            {
                command.ReplyToCommand("你没有使用此命令的权限。");
                return;
            }

            if (command.ArgCount - 1 < registration.MinArgs)
            {
                var usage = string.IsNullOrEmpty(registration.Usage) ? string.Empty : $" {registration.Usage}";
                command.ReplyToCommand($"用法：{registration.Name}{usage}");
                return;
            }

            using var commandSnapshot = plugin.Api.CreateCommandSnapshot(command);
            using var playerTable = player is null ? null : plugin.Api.CreatePlayerTable(player);
            plugin.Invoke($"命令 {registration.Name}", registration.Callback, playerTable, commandSnapshot.Table);
        };

        host.AddCommand(registration.Name, registration.Description, handler);
        return new RegistrationHandle(registration.Id, () => host.RemoveCommand(registration.Name, handler));
    }

    public IRegistrationHandle Activate(LuaPlugin plugin, CommandListenerRegistration registration)
    {
        CommandInfo.CommandListenerCallback handler = (player, command) =>
        {
            using var commandSnapshot = plugin.Api.CreateCommandSnapshot(command);
            using var playerTable = player is null ? null : plugin.Api.CreatePlayerTable(player);
            var result = plugin.Invoke($"原生命令 {registration.Name}", registration.Callback, playerTable, commandSnapshot.Table).FirstOrDefault();
            return plugin.Api.ParseHookResult(result);
        };

        host.AddCommandListener(registration.Name, handler, registration.Mode);
        return new RegistrationHandle(registration.Id, () =>
            host.RemoveCommandListener(registration.Name, handler, registration.Mode));
    }
}
