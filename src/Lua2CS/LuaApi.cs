using System.Collections;
using System.Drawing;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Translations;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Commands.Targeting;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Entities.Constants;
using CounterStrikeSharp.API.Modules.Events;
using CounterStrikeSharp.API.Modules.Menu;
using CounterStrikeSharp.API.Modules.Utils;
using CounterStrikeSharp.API.ValveConstants.Protobuf;
using Lua2CS.Bindings;
using Microsoft.Extensions.Logging;
using NLua;
using LuaRegistry = KeraLua.LuaRegistry;

namespace Lua2CS;

public sealed class LuaApi : IDisposable
{
    private static readonly JsonSerializerOptions StorageJsonOptions = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly string[] WrapperMethodNames =
    [
        "__lua2cs_player_print_chat",
        "__lua2cs_player_print_console",
        "__lua2cs_player_print_center",
        "__lua2cs_player_print_alert",
        "__lua2cs_player_print_html",
        "__lua2cs_player_refresh_method",
        "__lua2cs_player_has_permission_method",
        "__lua2cs_player_can_target_method",
        "__lua2cs_player_get_convar_method",
        "__lua2cs_player_execute_method",
        "__lua2cs_player_execute_server_method",
        "__lua2cs_player_give_item_method",
        "__lua2cs_player_give_weapon_method",
        "__lua2cs_player_remove_item_method",
        "__lua2cs_player_remove_weapons_method",
        "__lua2cs_player_drop_weapon_method",
        "__lua2cs_player_respawn_method",
        "__lua2cs_player_kill_method",
        "__lua2cs_player_kick_method",
        "__lua2cs_player_change_team_method",
        "__lua2cs_player_switch_team_method",
        "__lua2cs_player_teleport_method",
        "__lua2cs_player_set_health_method",
        "__lua2cs_player_set_armor_method",
        "__lua2cs_player_set_money_method",
        "__lua2cs_player_aim_target_method",
        "__lua2cs_player_set_max_health_method",
        "__lua2cs_player_set_gravity_method",
        "__lua2cs_player_set_velocity_modifier_method",
        "__lua2cs_player_set_model_method",
        "__lua2cs_player_set_render_color_method",
        "__lua2cs_player_close_menu_method",
        "__lua2cs_player_set_ammo_method",
        "__lua2cs_player_set_score_method",
        "__lua2cs_player_set_round_score_method",
        "__lua2cs_player_set_mvps_method",
        "__lua2cs_player_set_voice_flags_method",
        "__lua2cs_player_replicate_convar_method",
        "__lua2cs_player_set_fake_convar_method",
        "__lua2cs_player_emit_sound_method",
        "__lua2cs_entity_refresh_method",
        "__lua2cs_entity_spawn_method",
        "__lua2cs_entity_input_method",
        "__lua2cs_entity_remove_method",
        "__lua2cs_entity_teleport_method",
        "__lua2cs_entity_set_health_method",
        "__lua2cs_entity_set_max_health_method",
        "__lua2cs_entity_set_gravity_method",
        "__lua2cs_entity_set_model_method",
        "__lua2cs_entity_set_render_color_method",
        "__lua2cs_entity_emit_sound_method",
        "__lua2cs_weapon_refresh_method",
        "__lua2cs_weapon_set_ammo_method",
        "__lua2cs_weapon_set_econ_method",
        "__lua2cs_command_reply_method"
    ];

    private readonly ILogger _logger;
    private readonly Dictionary<long, CommandInfo> _commandContexts = [];
    private readonly Dictionary<nint, BaseMenu> _ownedMenus = [];
    private readonly LuaPlugin _plugin;
    private long _nextCommandContextId;
    private LuaFunction? _playerChatMethod;
    private LuaFunction? _playerConsoleMethod;
    private LuaFunction? _playerCenterMethod;
    private LuaFunction? _playerAlertMethod;
    private LuaFunction? _playerHtmlMethod;
    private LuaFunction? _playerRefreshMethod;
    private LuaFunction? _playerPermissionMethod;
    private LuaFunction? _playerCanTargetMethod;
    private LuaFunction? _playerConVarMethod;
    private LuaFunction? _playerExecuteMethod;
    private LuaFunction? _playerExecuteServerMethod;
    private LuaFunction? _playerGiveItemMethod;
    private LuaFunction? _playerGiveWeaponMethod;
    private LuaFunction? _playerRemoveItemMethod;
    private LuaFunction? _playerRemoveWeaponsMethod;
    private LuaFunction? _playerDropWeaponMethod;
    private LuaFunction? _playerRespawnMethod;
    private LuaFunction? _playerKillMethod;
    private LuaFunction? _playerKickMethod;
    private LuaFunction? _playerChangeTeamMethod;
    private LuaFunction? _playerSwitchTeamMethod;
    private LuaFunction? _playerTeleportMethod;
    private LuaFunction? _playerSetHealthMethod;
    private LuaFunction? _playerSetArmorMethod;
    private LuaFunction? _playerSetMoneyMethod;
    private LuaFunction? _playerAimTargetMethod;
    private LuaFunction? _playerSetMaxHealthMethod;
    private LuaFunction? _playerSetGravityMethod;
    private LuaFunction? _playerSetVelocityModifierMethod;
    private LuaFunction? _playerSetModelMethod;
    private LuaFunction? _playerSetRenderColorMethod;
    private LuaFunction? _playerCloseMenuMethod;
    private LuaFunction? _playerSetAmmoMethod;
    private LuaFunction? _playerSetScoreMethod;
    private LuaFunction? _playerSetRoundScoreMethod;
    private LuaFunction? _playerSetMvpsMethod;
    private LuaFunction? _playerSetVoiceFlagsMethod;
    private LuaFunction? _playerReplicateConVarMethod;
    private LuaFunction? _playerSetFakeConVarMethod;
    private LuaFunction? _playerEmitSoundMethod;
    private LuaFunction? _entityRefreshMethod;
    private LuaFunction? _entitySpawnMethod;
    private LuaFunction? _entityInputMethod;
    private LuaFunction? _entityRemoveMethod;
    private LuaFunction? _entityTeleportMethod;
    private LuaFunction? _entitySetHealthMethod;
    private LuaFunction? _entitySetMaxHealthMethod;
    private LuaFunction? _entitySetGravityMethod;
    private LuaFunction? _entitySetModelMethod;
    private LuaFunction? _entitySetRenderColorMethod;
    private LuaFunction? _entityEmitSoundMethod;
    private LuaFunction? _weaponRefreshMethod;
    private LuaFunction? _weaponSetAmmoMethod;
    private LuaFunction? _weaponSetEconMethod;
    private LuaFunction? _commandReplyMethod;

    private string StoragePath => Path.Combine(
        Path.GetDirectoryName(_plugin.ScriptPath)!,
        ".lua2cs-data",
        _plugin.Key + ".json");

    internal LuaApi(LuaPlugin plugin, ILogger logger)
    {
        _plugin = plugin;
        _logger = logger;
    }

    internal void InitializeLuaMethods()
    {
        _playerChatMethod = _plugin.State.GetFunction("__lua2cs_player_print_chat");
        _playerConsoleMethod = _plugin.State.GetFunction("__lua2cs_player_print_console");
        _playerCenterMethod = _plugin.State.GetFunction("__lua2cs_player_print_center");
        _playerAlertMethod = _plugin.State.GetFunction("__lua2cs_player_print_alert");
        _playerHtmlMethod = _plugin.State.GetFunction("__lua2cs_player_print_html");
        _playerRefreshMethod = _plugin.State.GetFunction("__lua2cs_player_refresh_method");
        _playerPermissionMethod = _plugin.State.GetFunction("__lua2cs_player_has_permission_method");
        _playerCanTargetMethod = _plugin.State.GetFunction("__lua2cs_player_can_target_method");
        _playerConVarMethod = _plugin.State.GetFunction("__lua2cs_player_get_convar_method");
        _playerExecuteMethod = _plugin.State.GetFunction("__lua2cs_player_execute_method");
        _playerExecuteServerMethod = _plugin.State.GetFunction("__lua2cs_player_execute_server_method");
        _playerGiveItemMethod = _plugin.State.GetFunction("__lua2cs_player_give_item_method");
        _playerGiveWeaponMethod = _plugin.State.GetFunction("__lua2cs_player_give_weapon_method");
        _playerRemoveItemMethod = _plugin.State.GetFunction("__lua2cs_player_remove_item_method");
        _playerRemoveWeaponsMethod = _plugin.State.GetFunction("__lua2cs_player_remove_weapons_method");
        _playerDropWeaponMethod = _plugin.State.GetFunction("__lua2cs_player_drop_weapon_method");
        _playerRespawnMethod = _plugin.State.GetFunction("__lua2cs_player_respawn_method");
        _playerKillMethod = _plugin.State.GetFunction("__lua2cs_player_kill_method");
        _playerKickMethod = _plugin.State.GetFunction("__lua2cs_player_kick_method");
        _playerChangeTeamMethod = _plugin.State.GetFunction("__lua2cs_player_change_team_method");
        _playerSwitchTeamMethod = _plugin.State.GetFunction("__lua2cs_player_switch_team_method");
        _playerTeleportMethod = _plugin.State.GetFunction("__lua2cs_player_teleport_method");
        _playerSetHealthMethod = _plugin.State.GetFunction("__lua2cs_player_set_health_method");
        _playerSetArmorMethod = _plugin.State.GetFunction("__lua2cs_player_set_armor_method");
        _playerSetMoneyMethod = _plugin.State.GetFunction("__lua2cs_player_set_money_method");
        _playerAimTargetMethod = _plugin.State.GetFunction("__lua2cs_player_aim_target_method");
        _playerSetMaxHealthMethod = _plugin.State.GetFunction("__lua2cs_player_set_max_health_method");
        _playerSetGravityMethod = _plugin.State.GetFunction("__lua2cs_player_set_gravity_method");
        _playerSetVelocityModifierMethod = _plugin.State.GetFunction("__lua2cs_player_set_velocity_modifier_method");
        _playerSetModelMethod = _plugin.State.GetFunction("__lua2cs_player_set_model_method");
        _playerSetRenderColorMethod = _plugin.State.GetFunction("__lua2cs_player_set_render_color_method");
        _playerCloseMenuMethod = _plugin.State.GetFunction("__lua2cs_player_close_menu_method");
        _playerSetAmmoMethod = _plugin.State.GetFunction("__lua2cs_player_set_ammo_method");
        _playerSetScoreMethod = _plugin.State.GetFunction("__lua2cs_player_set_score_method");
        _playerSetRoundScoreMethod = _plugin.State.GetFunction("__lua2cs_player_set_round_score_method");
        _playerSetMvpsMethod = _plugin.State.GetFunction("__lua2cs_player_set_mvps_method");
        _playerSetVoiceFlagsMethod = _plugin.State.GetFunction("__lua2cs_player_set_voice_flags_method");
        _playerReplicateConVarMethod = _plugin.State.GetFunction("__lua2cs_player_replicate_convar_method");
        _playerSetFakeConVarMethod = _plugin.State.GetFunction("__lua2cs_player_set_fake_convar_method");
        _playerEmitSoundMethod = _plugin.State.GetFunction("__lua2cs_player_emit_sound_method");
        _entityRefreshMethod = _plugin.State.GetFunction("__lua2cs_entity_refresh_method");
        _entitySpawnMethod = _plugin.State.GetFunction("__lua2cs_entity_spawn_method");
        _entityInputMethod = _plugin.State.GetFunction("__lua2cs_entity_input_method");
        _entityRemoveMethod = _plugin.State.GetFunction("__lua2cs_entity_remove_method");
        _entityTeleportMethod = _plugin.State.GetFunction("__lua2cs_entity_teleport_method");
        _entitySetHealthMethod = _plugin.State.GetFunction("__lua2cs_entity_set_health_method");
        _entitySetMaxHealthMethod = _plugin.State.GetFunction("__lua2cs_entity_set_max_health_method");
        _entitySetGravityMethod = _plugin.State.GetFunction("__lua2cs_entity_set_gravity_method");
        _entitySetModelMethod = _plugin.State.GetFunction("__lua2cs_entity_set_model_method");
        _entitySetRenderColorMethod = _plugin.State.GetFunction("__lua2cs_entity_set_render_color_method");
        _entityEmitSoundMethod = _plugin.State.GetFunction("__lua2cs_entity_emit_sound_method");
        _weaponRefreshMethod = _plugin.State.GetFunction("__lua2cs_weapon_refresh_method");
        _weaponSetAmmoMethod = _plugin.State.GetFunction("__lua2cs_weapon_set_ammo_method");
        _weaponSetEconMethod = _plugin.State.GetFunction("__lua2cs_weapon_set_econ_method");
        _commandReplyMethod = _plugin.State.GetFunction("__lua2cs_command_reply_method");

        foreach (var name in WrapperMethodNames)
        {
            _plugin.State[name] = null;
        }
    }

    public void CreatePlugin(LuaTable spec)
    {
        if (!string.IsNullOrEmpty(_plugin.Name))
        {
            throw new InvalidOperationException("cs.plugin may only be called once per script.");
        }

        _plugin.Name = ReadString(spec, "name", required: true);
        _plugin.Version = ReadString(spec, "version", defaultValue: "0.0.0");
        _plugin.Description = ReadString(spec, "description", defaultValue: string.Empty);
    }

    public long RegisterEvent(string eventName, LuaFunction callback, LuaTable options)
    {
        var mode = ReadString(options, "mode", defaultValue: "post").Equals("pre", StringComparison.OrdinalIgnoreCase)
            ? HookMode.Pre
            : HookMode.Post;
        var id = _plugin.NextRegistrationId();
        _plugin.AddRegistration(new EventRegistration(id, eventName.Trim(), mode, callback));
        return id;
    }

    public long RegisterListener(string listenerName, LuaFunction callback)
    {
        var id = _plugin.NextRegistrationId();
        _plugin.AddRegistration(new ListenerRegistration(id, listenerName.Trim(), callback));
        return id;
    }

    public long RegisterCommand(string name, LuaTable options, LuaFunction callback)
    {
        var id = _plugin.NextRegistrationId();
        _plugin.AddRegistration(new CommandRegistration(
            id,
            name.Trim(),
            ReadString(options, "description", defaultValue: "Lua 命令"),
            ReadString(options, "permission", defaultValue: string.Empty),
            ReadBool(options, "allow_console", true),
            ReadInt(options, "min_args", 0),
            ReadString(options, "usage", defaultValue: string.Empty),
            callback));
        return id;
    }

    public long RegisterCommandListener(string name, LuaTable options, LuaFunction callback)
    {
        var modeName = ReadString(options, "mode", defaultValue: "pre");
        var mode = modeName.ToLowerInvariant() switch
        {
            "pre" => HookMode.Pre,
            "post" => HookMode.Post,
            _ => throw new InvalidDataException("Command listener mode must be pre or post.")
        };
        var id = _plugin.NextRegistrationId();
        _plugin.AddRegistration(new CommandListenerRegistration(id, name.Trim(), mode, callback));
        return id;
    }

    public long RegisterTimer(double interval, LuaFunction callback, LuaTable options)
    {
        if (!double.IsFinite(interval) || interval <= 0 || interval > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(interval), "Timer interval must be greater than zero.");
        }

        var id = _plugin.NextRegistrationId();
        _plugin.AddRegistration(new TimerRegistration(
            id,
            (float)interval,
            ReadBool(options, "repeating", ReadBool(options, "repeat", false)),
            ReadBool(options, "stop_on_map_change", true),
            callback));
        return id;
    }

    public long RegisterFrame(string scheduleName, long tickDelay, LuaFunction callback)
    {
        var schedule = scheduleName switch
        {
            "next_frame" => FrameSchedule.NextFrame,
            "next_world_update" => FrameSchedule.NextWorldUpdate,
            "after_ticks" => FrameSchedule.AfterTicks,
            _ => throw new InvalidDataException($"Unknown frame schedule '{scheduleName}'.")
        };
        if (tickDelay is < 0 or > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(tickDelay));
        if (schedule == FrameSchedule.AfterTicks && tickDelay is < 1 or > 1_000_000)
        {
            throw new ArgumentOutOfRangeException(nameof(tickDelay), "after_ticks 延迟必须为 1 到 1000000 Tick。");
        }
        var id = _plugin.NextRegistrationId();
        _plugin.AddRegistration(new FrameRegistration(id, schedule, (int)tickDelay, callback));
        return id;
    }

    public void RegisterLoad(LuaFunction callback) => _plugin.LoadCallback = callback;
    public void RegisterUnload(LuaFunction callback) => _plugin.UnloadCallback = callback;
    public bool CancelRegistration(long registrationId) => _plugin.RemoveRegistration(registrationId);

    public void LogDebug(object? message) => _logger.LogDebug("[{Plugin}] {Message}", _plugin.Name, message);
    public void LogInfo(object? message) => _logger.LogInformation("[{Plugin}] {Message}", _plugin.Name, message);
    public void LogWarning(object? message) => _logger.LogWarning("[{Plugin}] {Message}", _plugin.Name, message);
    public void LogError(object? message) => _logger.LogError("[{Plugin}] {Message}", _plugin.Name, message);

    public void ServerPrintChatAll(string message) => Server.PrintToChatAll(ChatMessageFormatter.Normalize(message));
    public void ServerPrintConsole(string message) => Server.PrintToConsole(message);
    public void ServerExecute(string command) => Server.ExecuteCommand(command);

    public LuaTable GetServerInfo()
    {
        var table = NewTable();
        table["map_name"] = Server.MapName;
        table["max_players"] = Server.MaxPlayers;
        table["tick_interval"] = Server.TickInterval;
        table["tick_count"] = Server.TickCount;
        table["current_time"] = Server.CurrentTime;
        table["ticked_time"] = Server.TickedTime;
        table["engine_time"] = Server.EngineTime;
        table["frame_time"] = Server.FrameTime;
        return table;
    }

    public LuaTable GetMapList()
    {
        var table = NewTable();
        var index = 1;
        foreach (var map in Server.GetMapList()) table[index++] = map;
        return table;
    }

    public bool ServerIsMapValid(string mapName) => Server.IsMapValid(mapName);
    public void ServerPrecacheModel(string modelName) => Server.PrecacheModel(modelName);

    public string? GetConVar(string name) => ConVar.Find(name.Trim())?.StringValue;

    public bool SetConVar(string name, string value)
    {
        var conVar = ConVar.Find(name.Trim());
        if (conVar is null) return false;
        conVar.StringValue = value;
        return true;
    }

    public LuaTable GetEventNames() => CreateStringList(EventBindings.Names.Order(StringComparer.OrdinalIgnoreCase));
    public LuaTable GetListenerNames() => CreateStringList(ListenerBindings.Names.Order(StringComparer.OrdinalIgnoreCase));

    public LuaTable FindEntities(string designerName, long limit)
    {
        designerName = designerName.Trim();
        if (string.IsNullOrEmpty(designerName)) return NewTable();
        var entities = Utilities.FindAllEntitiesByDesignerName<CBaseEntity>(designerName)
            .Where(entity => entity.IsValid)
            .Take((int)Math.Clamp(limit, 1, 512));
        return CreateEntityList(entities);
    }

    public LuaTable? GetEntity(long index)
    {
        if (index is < 0 or >= Utilities.MaxEntities) return null;
        var entity = Utilities.GetEntityFromIndex<CBaseEntity>((int)index);
        return entity is { IsValid: true } ? CreateEntityTable(entity) : null;
    }

    public LuaTable? CreateEntity(string designerName, bool spawn)
    {
        designerName = ValidateDesignerName(designerName);
        var entity = Utilities.CreateEntityByName<CBaseEntity>(designerName);
        if (entity is not { IsValid: true }) return null;
        try
        {
            if (spawn) entity.DispatchSpawn();
            return CreateEntityTable(entity);
        }
        catch
        {
            if (entity.IsValid) entity.Remove();
            throw;
        }
    }

    public LuaTable? GetWeapon(long handle)
    {
        var weapon = ResolveWeapon(handle);
        return weapon is null ? null : CreateWeaponTable(weapon);
    }

    public LuaTable FindWeapons(string designerName, long limit)
    {
        designerName = ValidateWeaponDesignerName(designerName);
        var weapons = Utilities.FindAllEntitiesByDesignerName<CBasePlayerWeapon>(designerName)
            .Where(weapon => weapon.IsValid)
            .Take((int)Math.Clamp(limit, 1, 512));
        var table = NewTable();
        var index = 1;
        foreach (var weapon in weapons)
        {
            using var weaponTable = CreateWeaponTable(weapon);
            table[index++] = weaponTable;
        }
        return table;
    }

    public bool WeaponSetAmmo(long handle, long clip, long reserve, long clipSecondary, long reserveSecondary)
    {
        ValidateOptionalAmmo(clip, nameof(clip));
        ValidateOptionalAmmo(reserve, nameof(reserve));
        ValidateOptionalAmmo(clipSecondary, nameof(clipSecondary));
        ValidateOptionalAmmo(reserveSecondary, nameof(reserveSecondary));
        var weapon = ResolveWeapon(handle);
        if (weapon is null) return false;
        ApplyWeaponAmmo(weapon, clip, reserve, clipSecondary, reserveSecondary);
        return true;
    }

    public bool WeaponSetEcon(long handle, LuaTable options)
    {
        var weapon = ResolveWeapon(handle);
        if (weapon is null) return false;
        ApplyWeaponEcon(weapon, options);
        return true;
    }

    public LuaTable? RefreshEntity(long handle)
    {
        var entity = ResolveEntity(handle);
        return entity is null ? null : CreateEntityTable(entity);
    }

    public bool EntitySpawn(long handle)
    {
        var entity = ResolveEntity(handle);
        if (entity is null) return false;
        entity.DispatchSpawn();
        return true;
    }

    public bool EntityInput(long handle, string inputName, string value, double delay)
    {
        var entity = ResolveEntity(handle);
        if (entity is null || string.IsNullOrWhiteSpace(inputName)) return false;
        if (!double.IsFinite(delay) || delay is < 0 or > 3600) throw new ArgumentOutOfRangeException(nameof(delay));
        if (delay > 0) entity.AddEntityIOEvent(inputName.Trim(), value: value, delay: (float)delay);
        else entity.AcceptInput(inputName.Trim(), value: value);
        return true;
    }

    public bool EntityRemove(long handle, double delay)
    {
        var entity = ResolveEntity(handle);
        if (entity is null) return false;
        if (!double.IsFinite(delay) || delay is < 0 or > 3600) throw new ArgumentOutOfRangeException(nameof(delay));
        if (delay > 0) entity.AddEntityIOEvent("Kill", delay: (float)delay);
        else entity.Remove();
        return true;
    }

    public bool EntityTeleport(long handle, LuaTable? position, LuaTable? angles, LuaTable? velocity)
    {
        var entity = ResolveEntity(handle);
        if (entity is null) return false;
        var parsedPosition = ReadVector(position);
        var parsedAngles = ReadVector(angles);
        var parsedVelocity = ReadVector(velocity);
        if (parsedPosition is null && parsedAngles is null && parsedVelocity is null) return false;
        entity.Teleport(parsedPosition, parsedAngles, parsedVelocity);
        return true;
    }

    public bool EntitySetHealth(long handle, long health)
    {
        var entity = ResolveEntity(handle);
        if (entity is null) return false;
        entity.Health = (int)Math.Clamp(health, 0, int.MaxValue);
        Utilities.SetStateChanged(entity, "CBaseEntity", "m_iHealth");
        return true;
    }

    public bool EntitySetMaxHealth(long handle, long health)
    {
        var entity = ResolveEntity(handle);
        if (entity is null) return false;
        entity.MaxHealth = (int)Math.Clamp(health, 1, int.MaxValue);
        Utilities.SetStateChanged(entity, "CBaseEntity", "m_iMaxHealth");
        return true;
    }

    public bool EntitySetGravity(long handle, double scale)
    {
        var entity = ResolveEntity(handle);
        if (entity is null) return false;
        entity.GravityScale = ClampFinite(scale, 0, 10, nameof(scale));
        Utilities.SetStateChanged(entity, "CBaseEntity", "m_flGravityScale");
        return true;
    }

    public bool EntitySetModel(long handle, string modelName, bool precache)
    {
        var entity = ResolveModelEntity(handle);
        if (entity is null) return false;
        modelName = ValidateResourceName(modelName, "模型路径");
        if (precache) Server.PrecacheModel(modelName);
        entity.SetModel(modelName);
        return true;
    }

    public bool EntitySetRenderColor(long handle, long red, long green, long blue, long alpha)
    {
        var entity = ResolveModelEntity(handle);
        if (entity is null) return false;
        entity.Render = CreateColor(red, green, blue, alpha);
        Utilities.SetStateChanged(entity, "CBaseModelEntity", "m_clrRender");
        return true;
    }

    public long EntityEmitSound(long handle, string soundEventName, double volume, double pitch)
    {
        var entity = ResolveEntity(handle);
        if (entity is null || string.IsNullOrWhiteSpace(soundEventName)) return 0;
        ValidateSoundParameters(volume, pitch);
        return entity.EmitSound(
            soundEventName.Trim(),
            volume: (float)Math.Clamp(volume, 0, 1),
            pitch: (float)Math.Clamp(pitch, 0, 255));
    }

    public LuaTable? GetGameRules()
    {
        var rules = ResolveGameRules();
        if (rules is null) return null;

        var table = NewTable();
        table["freeze_period"] = rules.FreezePeriod;
        table["warmup_period"] = rules.WarmupPeriod;
        table["warmup_start_time"] = rules.WarmupPeriodStart;
        table["warmup_end_time"] = rules.WarmupPeriodEnd;
        table["round_start_time"] = rules.RoundStartTime;
        table["game_restart"] = rules.GameRestart;
        table["game_phase"] = rules.GamePhase;
        table["total_rounds_played"] = rules.TotalRoundsPlayed;
        table["overtime_playing"] = rules.OvertimePlaying;
        table["bomb_planted"] = rules.BombPlanted;
        table["bomb_dropped"] = rules.BombDropped;
        table["ct_timeout_active"] = rules.CTTimeOutActive;
        table["terrorist_timeout_active"] = rules.TerroristTimeOutActive;

        foreach (var team in Utilities.FindAllEntitiesByDesignerName<CCSTeam>("cs_team_manager")
                     .Where(entity => entity.IsValid))
        {
            if (team.TeamNum == (byte)CsTeam.Terrorist) table["terrorist_score"] = team.Score;
            if (team.TeamNum == (byte)CsTeam.CounterTerrorist) table["ct_score"] = team.Score;
        }

        return table;
    }

    public bool TerminateRound(double delay, string reason)
    {
        if (!double.IsFinite(delay) || delay is < 0 or > 60)
        {
            throw new ArgumentOutOfRangeException(nameof(delay), "回合结束延迟必须为 0 到 60 秒的有限数值。");
        }

        var rules = ResolveGameRules();
        if (rules is null) return false;
        rules.TerminateRound((float)delay, ParseRoundEndReason(reason));
        return true;
    }

    public LuaTable GetNavAreas(long limit)
    {
        var table = NewTable();
        var index = 1;
        foreach (var area in CCSNavArea.GetAllNavAreas().Take((int)Math.Clamp(limit, 1, 4096)))
        {
            using var areaTable = CreateNavAreaTable(area);
            table[index++] = areaTable;
        }
        return table;
    }

    public LuaTable? GetClosestNavArea(LuaTable? position, double maximumDistance)
    {
        if (!double.IsFinite(maximumDistance) || maximumDistance < -1 || maximumDistance > float.MaxValue)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDistance));
        }

        var parsed = ReadVector(position) ?? throw new InvalidDataException("导航查询必须提供位置向量。");
        var area = CCSNavArea.GetClosestNavArea(
            new Vector(parsed.X, parsed.Y, parsed.Z),
            out var distance,
            (float)maximumDistance);
        if (area is null) return null;
        var table = CreateNavAreaTable(area);
        table["distance"] = distance;
        return table;
    }

    public bool OpenMenu(LuaTable? playerReference, LuaTable? spec, LuaFunction? callback)
    {
        var player = ResolvePlayerReference(playerReference);
        if (player is null) return false;
        if (spec is null || callback is null) throw new InvalidDataException("菜单必须提供配置表和回调函数。");
        var title = ValidateDisplayText(ReadString(spec, "title", required: true), "菜单标题", 256);
        var type = ReadString(spec, "type", defaultValue: "chat").ToLowerInvariant();
        BaseMenu menu = type switch
        {
            "chat" => new ChatMenu(title),
            "console" => new ConsoleMenu(title),
            _ => throw new ArgumentException("菜单 type 必须是 chat 或 console。", nameof(spec))
        };

        menu.ExitButton = ReadBool(spec, "exit_button", true);
        menu.PostSelectAction = ParsePostSelectAction(ReadString(spec, "post_select", defaultValue: "reset"));
        if (spec["items"] is not LuaTable items)
        {
            throw new InvalidDataException("菜单必须提供 items 数组。");
        }

        using (items)
        {
            for (var index = 1; index <= 64; index++)
            {
                if (items[index] is not LuaTable item) break;
                using (item)
                {
                    var optionIndex = index;
                    menu.AddMenuOption(
                        ValidateDisplayText(ReadString(item, "text", required: true), "菜单选项", 512),
                        (selectedPlayer, _) =>
                        {
                            using var selectedPlayerTable = CreatePlayerTable(selectedPlayer);
                            _plugin.Invoke(callback, selectedPlayerTable, optionIndex);
                            if (menu.PostSelectAction == PostSelectAction.Close
                                && _ownedMenus.TryGetValue(selectedPlayer.Handle, out var ownedMenu)
                                && ReferenceEquals(ownedMenu, menu))
                            {
                                _ownedMenus.Remove(selectedPlayer.Handle);
                            }
                        },
                        ReadBool(item, "disabled", false));
                }
            }
        }

        if (menu.MenuOptions.Count == 0) throw new InvalidDataException("菜单至少需要一个选项。");
        _ownedMenus[player.Handle] = menu;
        try
        {
            menu.Open(player);
            return true;
        }
        catch
        {
            if (_ownedMenus.TryGetValue(player.Handle, out var ownedMenu) && ReferenceEquals(ownedMenu, menu))
            {
                _ownedMenus.Remove(player.Handle);
            }
            throw;
        }
    }

    public bool CloseMenu(LuaTable? playerReference)
    {
        var player = ResolvePlayerReference(playerReference);
        if (player is null || MenuManager.GetActiveMenu(player) is null) return false;
        _ownedMenus.Remove(player.Handle);
        MenuManager.CloseActiveMenu(player);
        return true;
    }

    public object? StorageGet(string key)
    {
        key = ValidateStorageKey(key);
        var data = LoadStorage();
        return data.TryGetValue(key, out var value) ? ConvertJsonValue(value) : null;
    }

    public bool StorageHas(string key) => LoadStorage().ContainsKey(ValidateStorageKey(key));

    public bool StorageSetString(string key, string value) => StorageSetValue(key, value);
    public bool StorageSetInteger(string key, long value) => StorageSetValue(key, value);
    public bool StorageSetNumber(string key, double value) => StorageSetValue(key, value);
    public bool StorageSetBoolean(string key, bool value) => StorageSetValue(key, value);

    private bool StorageSetValue(string key, object value)
    {
        key = ValidateStorageKey(key);
        var data = LoadStorage();
        data[key] = SerializeStorageValue(value);
        SaveStorage(data);
        return true;
    }

    public bool StorageDelete(string key)
    {
        key = ValidateStorageKey(key);
        var data = LoadStorage();
        if (!data.Remove(key)) return false;
        SaveStorage(data);
        return true;
    }

    public void StorageClear() => SaveStorage(new Dictionary<string, JsonElement>(StringComparer.Ordinal));

    public LuaTable StorageAll()
    {
        var table = NewTable();
        foreach (var (key, value) in LoadStorage()) table[key] = ConvertJsonValue(value);
        return table;
    }

    public LuaTable GetPlayers()
    {
        var table = NewTable();
        var index = 1;
        foreach (var player in Utilities.GetPlayers().Where(IsUsablePlayer))
        {
            using var playerTable = CreatePlayerTable(player);
            table[index++] = playerTable;
        }

        return table;
    }

    public LuaTable? GetPlayer(long slot)
    {
        var player = ResolvePlayer(slot);
        return player is null ? null : CreatePlayerTable(player);
    }

    public LuaTable? GetPlayerByUserId(long userId)
    {
        if (userId is < 0 or > int.MaxValue) return null;
        var player = Utilities.GetPlayerFromUserid((int)userId);
        return IsUsablePlayer(player) ? CreatePlayerTable(player!) : null;
    }

    public LuaTable? GetPlayerBySteamId(string steamId)
    {
        if (!ulong.TryParse(steamId.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var id)) return null;
        var player = Utilities.GetPlayerFromSteamId64(id);
        return IsUsablePlayer(player) ? CreatePlayerTable(player!) : null;
    }

    public LuaTable FindPlayers(string query)
    {
        query = query.Trim();
        if (string.IsNullOrEmpty(query)) return NewTable();

        IEnumerable<CCSPlayerController> players = Utilities.GetPlayers().Where(IsUsablePlayer);
        if (int.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var numeric))
        {
            players = players.Where(player => player.Slot == numeric || player.UserId == numeric);
        }
        else if (ulong.TryParse(query, NumberStyles.None, CultureInfo.InvariantCulture, out var steamId))
        {
            players = players.Where(player => player.SteamID == steamId);
        }
        else
        {
            players = players.Where(player => player.PlayerName.Contains(query, StringComparison.OrdinalIgnoreCase));
        }

        return CreatePlayerList(players);
    }

    public LuaTable TargetPlayers(string pattern, LuaTable? caller = null)
    {
        pattern = pattern.Trim();
        if (string.IsNullOrEmpty(pattern)) return NewTable();
        var callerPlayer = ResolvePlayerReference(caller);
        return CreatePlayerList(new Target(pattern).GetTarget(callerPlayer).Players.Where(IsUsablePlayer));
    }

    public LuaTable GetHumanPlayers() => CreatePlayerList(Utilities.GetPlayers().Where(player => IsUsablePlayer(player) && !player.IsBot && !player.IsHLTV));
    public LuaTable GetBots() => CreatePlayerList(Utilities.GetPlayers().Where(player => IsUsablePlayer(player) && player.IsBot));
    public long GetPlayerCount() => Utilities.GetPlayers().LongCount(IsUsablePlayer);

    public LuaTable? RefreshPlayer(long slot) => GetPlayer(slot);

    public bool IsCurrentPlayer(long slot, object? userId, string? steamId)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        if (userId is not null && player.UserId != Convert.ToInt32(userId, CultureInfo.InvariantCulture)) return false;
        return string.IsNullOrEmpty(steamId)
               || player.SteamID.ToString(CultureInfo.InvariantCulture).Equals(steamId, StringComparison.Ordinal);
    }

    public bool PlayerPrintChat(long slot, string message)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.PrintToChat(ChatMessageFormatter.Normalize(message));
        return true;
    }

    public bool PlayerPrintConsole(long slot, string message)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.PrintToConsole(message);
        return true;
    }

    public bool PlayerPrintCenter(long slot, string message)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.PrintToCenter(message);
        return true;
    }

    public bool PlayerPrintAlert(long slot, string message)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.PrintToCenterAlert(message);
        return true;
    }

    public bool PlayerPrintHtml(long slot, string message, long duration)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.PrintToCenterHtml(message, (int)Math.Clamp(duration, 1, 60));
        return true;
    }

    public bool PlayerHasPermission(long slot, string permission)
    {
        var player = ResolvePlayer(slot);
        return player is not null && AdminManager.PlayerHasPermissions(player, permission);
    }

    public bool PlayerCanTarget(long slot, long targetSlot)
    {
        var player = ResolvePlayer(slot);
        var target = ResolvePlayer(targetSlot);
        return player is not null && target is not null && AdminManager.CanPlayerTarget(player, target);
    }

    public string? PlayerGetConVar(long slot, string name) => ResolvePlayer(slot)?.GetConVarValue(name);

    public bool PlayerExecute(long slot, string command, bool asServer)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        if (asServer) player.ExecuteClientCommandFromServer(command);
        else player.ExecuteClientCommand(command);
        return true;
    }

    public bool PlayerGiveItem(long slot, string designerName)
    {
        var player = ResolvePlayer(slot);
        if (player is null || string.IsNullOrWhiteSpace(designerName)) return false;
        return player.GiveNamedItem(designerName.Trim()) != IntPtr.Zero;
    }

    public LuaTable? PlayerGiveWeapon(long slot, string designerName, LuaTable? options)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return null;
        designerName = ValidateWeaponDesignerName(designerName);
        ValidateWeaponOptions(options);
        var pointer = player.GiveNamedItem(designerName);
        if (pointer == IntPtr.Zero) return null;

        var weapon = new CBasePlayerWeapon(pointer);
        if (!weapon.IsValid) return null;
        ApplyWeaponOptions(weapon, options);
        return CreateWeaponTable(weapon);
    }

    public bool PlayerRemoveItem(long slot, string designerName)
    {
        var player = ResolvePlayer(slot);
        return player is not null && !string.IsNullOrWhiteSpace(designerName)
                                  && player.RemoveItemByDesignerName(designerName.Trim());
    }

    public bool PlayerRemoveWeapons(long slot)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.RemoveWeapons();
        return true;
    }

    public bool PlayerDropActiveWeapon(long slot)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.DropActiveWeapon();
        return true;
    }

    public bool PlayerRespawn(long slot)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.Respawn();
        return true;
    }

    public bool PlayerKill(long slot, bool explode, bool force)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.CommitSuicide(explode, force);
        return true;
    }

    public bool PlayerKick(long slot)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.Disconnect(NetworkDisconnectionReason.NETWORK_DISCONNECT_KICKED);
        return true;
    }

    public bool PlayerChangeTeam(long slot, object? team, bool keepAlive)
    {
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        var resolvedTeam = ParseTeam(team);
        if (keepAlive) player.SwitchTeam(resolvedTeam);
        else player.ChangeTeam(resolvedTeam);
        return true;
    }

    public bool PlayerTeleport(long slot, LuaTable? position, LuaTable? angles, LuaTable? velocity)
    {
        var pawn = ResolvePlayer(slot)?.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid) return false;
        var parsedPosition = ReadVector(position);
        var parsedAngles = ReadVector(angles);
        var parsedVelocity = ReadVector(velocity);
        if (parsedPosition is null && parsedAngles is null && parsedVelocity is null) return false;
        pawn.Teleport(parsedPosition, parsedAngles, parsedVelocity);
        return true;
    }

    public bool PlayerSetHealth(long slot, long health)
    {
        var pawn = ResolvePlayer(slot)?.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid) return false;
        pawn.Health = (int)Math.Clamp(health, 0, int.MaxValue);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iHealth");
        return true;
    }

    public bool PlayerSetArmor(long slot, long armor)
    {
        var pawn = ResolvePlayer(slot)?.PlayerPawn.Value;
        if (pawn is null || !pawn.IsValid) return false;
        pawn.ArmorValue = (int)Math.Clamp(armor, 0, int.MaxValue);
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_ArmorValue");
        return true;
    }

    public bool PlayerSetMoney(long slot, long money)
    {
        var services = ResolvePlayer(slot)?.InGameMoneyServices;
        if (services is null) return false;
        services.Account = (int)Math.Clamp(money, 0, int.MaxValue);
        return true;
    }

    public bool PlayerSetAmmo(long slot, long clip, long reserve)
    {
        if (clip is < -1 or > 10000) throw new ArgumentOutOfRangeException(nameof(clip), "弹匣数量必须为 0 到 10000。");
        if (reserve is < -1 or > 10000) throw new ArgumentOutOfRangeException(nameof(reserve), "备弹数量必须为 0 到 10000。");
        if (clip == -1 && reserve == -1) throw new ArgumentException("弹匣和备弹不能同时省略。");

        var weapon = ResolveActiveWeapon(slot);
        if (weapon is null) return false;
        if (clip >= 0)
        {
            weapon.Clip1 = (int)clip;
            Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_iClip1");
        }
        if (reserve >= 0)
        {
            weapon.ReserveAmmo[0] = (int)reserve;
            Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
        }
        return true;
    }

    public bool PlayerSetScore(long slot, long score)
    {
        var parsedScore = ClampCounter(score, nameof(score));
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.Score = parsedScore;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iScore");
        return true;
    }

    public bool PlayerSetRoundScore(long slot, long score)
    {
        var parsedScore = ClampCounter(score, nameof(score));
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.RoundScore = parsedScore;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iRoundScore");
        return true;
    }

    public bool PlayerSetMvps(long slot, long mvps)
    {
        var parsedMvps = ClampCounter(mvps, nameof(mvps));
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.MVPs = parsedMvps;
        Utilities.SetStateChanged(player, "CCSPlayerController", "m_iMVPs");
        return true;
    }

    public bool PlayerSetVoiceFlags(long slot, long flags)
    {
        if (flags is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(flags), "语音标志必须为 0 到 31。");
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.VoiceFlags = (VoiceFlags)(byte)flags;
        return true;
    }

    public bool PlayerReplicateConVar(long slot, string name, string value)
    {
        name = ValidateConVarName(name);
        value = ValidateConVarValue(value);
        var player = ResolvePlayer(slot);
        if (player is null) return false;
        player.ReplicateConVar(name, value);
        return true;
    }

    public bool PlayerSetFakeConVar(long slot, string name, string value)
    {
        name = ValidateConVarName(name);
        value = ValidateConVarValue(value);
        var player = ResolvePlayer(slot);
        if (player is null || !player.IsBot) return false;
        player.SetFakeClientConVar(name, value);
        return true;
    }

    public LuaTable? PlayerGetAimTarget(long slot)
    {
        var player = ResolvePlayer(slot);
        var rules = ResolveGameRules();
        if (player is null || rules is null) return null;
        var target = rules.GetClientAimTarget(player);
        return IsUsablePlayer(target) ? CreatePlayerTable(target!) : null;
    }

    public bool PlayerSetMaxHealth(long slot, long health)
    {
        var pawn = ResolvePlayerPawn(slot);
        if (pawn is null) return false;
        pawn.MaxHealth = (int)Math.Clamp(health, 1, int.MaxValue);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_iMaxHealth");
        return true;
    }

    public bool PlayerSetGravity(long slot, double scale)
    {
        var pawn = ResolvePlayerPawn(slot);
        if (pawn is null) return false;
        pawn.GravityScale = ClampFinite(scale, 0, 10, nameof(scale));
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_flGravityScale");
        return true;
    }

    public bool PlayerSetVelocityModifier(long slot, double modifier)
    {
        var pawn = ResolvePlayerPawn(slot);
        if (pawn is null) return false;
        pawn.VelocityModifier = ClampFinite(modifier, 0, 10, nameof(modifier));
        Utilities.SetStateChanged(pawn, "CCSPlayerPawn", "m_flVelocityModifier");
        return true;
    }

    public bool PlayerSetModel(long slot, string modelName, bool precache)
    {
        var pawn = ResolvePlayerPawn(slot);
        if (pawn is null) return false;
        modelName = ValidateResourceName(modelName, "模型路径");
        if (precache) Server.PrecacheModel(modelName);
        pawn.SetModel(modelName);
        return true;
    }

    public bool PlayerSetRenderColor(long slot, long red, long green, long blue, long alpha)
    {
        var pawn = ResolvePlayerPawn(slot);
        if (pawn is null) return false;
        pawn.Render = CreateColor(red, green, blue, alpha);
        Utilities.SetStateChanged(pawn, "CBaseModelEntity", "m_clrRender");
        return true;
    }

    public long PlayerEmitSound(long slot, string soundEventName, double volume, double pitch)
    {
        var player = ResolvePlayer(slot);
        var pawn = player?.PlayerPawn.Value;
        if (player is null || pawn is not { IsValid: true } || string.IsNullOrWhiteSpace(soundEventName)) return 0;
        ValidateSoundParameters(volume, pitch);
        return pawn.EmitSound(
            soundEventName.Trim(),
            new RecipientFilter(player),
            (float)Math.Clamp(volume, 0, 1),
            (float)Math.Clamp(pitch, 0, 255));
    }

    public bool CommandReply(long contextId, string message)
    {
        if (!_commandContexts.TryGetValue(contextId, out var command)) return false;
        command.ReplyToCommand(ChatMessageFormatter.Normalize(message));
        return true;
    }

    internal LuaEventSnapshot CreateEventSnapshot(GameEvent gameEvent, GameEventInfo info, bool writable)
    {
        var eventTable = NewTable();
        var infoTable = NewTable();
        var properties = EventProperties(gameEvent.GetType()).ToArray();

        eventTable["name"] = gameEvent.EventName;
        foreach (var property in properties)
        {
            try
            {
                var value = property.GetValue(gameEvent);
                SetMappedValue(eventTable, ToSnakeCase(property.Name), value);
            }
            catch (Exception exception)
            {
                _logger.LogDebug(exception, "Unable to read event field {Field}", property.Name);
            }
        }

        var existingPlayer = eventTable["player"];
        var userIdValue = eventTable["userid"];
        if (existingPlayer is null && userIdValue is LuaTable userTable)
        {
            eventTable["player"] = userTable;
            userTable.Dispose();
        }
        else if (existingPlayer is null && userIdValue is long userId)
        {
            var player = Utilities.GetPlayerFromUserid((int)userId);
            if (IsUsablePlayer(player))
            {
                using var playerTable = CreatePlayerTable(player!);
                eventTable["player"] = playerTable;
            }
        }

        infoTable["dont_broadcast"] = info.DontBroadcast;
        return new LuaEventSnapshot(this, gameEvent, info, eventTable, infoTable, properties, writable);
    }

    internal LuaMappedArguments MapArguments(IEnumerable<object?> values)
    {
        var mapped = new List<object?>();
        var owned = new List<IDisposable>();
        foreach (var value in values)
        {
            if (value is CCSPlayerController player)
            {
                var table = CreatePlayerTable(player);
                owned.Add(table);
                mapped.Add(table);
            }
            else if (value is SteamID steamId)
            {
                mapped.Add(steamId.SteamId64.ToString(CultureInfo.InvariantCulture));
            }
            else if (value is Vector vector)
            {
                var table = CreateVectorTable(vector.X, vector.Y, vector.Z);
                owned.Add(table);
                mapped.Add(table);
            }
            else if (value is QAngle angle)
            {
                var table = CreateVectorTable(angle.X, angle.Y, angle.Z);
                owned.Add(table);
                mapped.Add(table);
            }
            else if (value?.GetType().IsEnum == true)
            {
                mapped.Add(value.ToString());
            }
            else if (IsLuaPrimitive(value))
            {
                mapped.Add(value);
            }
            else
            {
                mapped.Add(value?.ToString());
            }
        }

        return new LuaMappedArguments(mapped.ToArray(), owned);
    }

    internal LuaCommandSnapshot CreateCommandSnapshot(CommandInfo command)
    {
        var id = Interlocked.Increment(ref _nextCommandContextId);
        _commandContexts[id] = command;
        var table = NewTable();
        var args = NewTable();

        for (var index = 1; index < command.ArgCount; index++)
        {
            args[index] = command.GetArg(index);
        }

        table["__context_id"] = id;
        table["name"] = command.ArgCount > 0 ? command.GetArg(0) : string.Empty;
        table["args"] = args;
        table["arg_string"] = command.ArgString;
        table["context"] = command.CallingContext.ToString().ToLowerInvariant();
        table["reply"] = _commandReplyMethod;
        args.Dispose();
        return new LuaCommandSnapshot(table, () => _commandContexts.Remove(id));
    }

    internal HookResult ParseHookResult(object? value)
    {
        if (value is long number && Enum.IsDefined(typeof(HookResult), (int)number))
        {
            return (HookResult)(int)number;
        }

        return value?.ToString()?.Trim().ToLowerInvariant() switch
        {
            "changed" => HookResult.Changed,
            "handled" => HookResult.Handled,
            "stop" => HookResult.Stop,
            _ => HookResult.Continue
        };
    }

    internal LuaTable CreatePlayerTable(CCSPlayerController player)
    {
        var table = NewTable();
        var snapshotComplete = true;
        CCSPlayerPawn? pawn = null;

        void PopulateOptional(string group, Action populate)
        {
            try
            {
                populate();
            }
            catch (Exception exception)
            {
                snapshotComplete = false;
                _logger.LogDebug(exception, "无法读取玩家 {Slot} 的 {Group} 快照字段", player.Slot, group);
            }
        }

        table["slot"] = player.Slot;
        table["user_id"] = player.UserId;
        table["name"] = player.PlayerName;
        table["steam_id"] = player.SteamID.ToString(CultureInfo.InvariantCulture);
        table["team"] = player.Team.ToString();
        table["team_id"] = (long)player.Team;
        table["is_bot"] = player.IsBot;
        table["is_hltv"] = player.IsHLTV;
        table["is_alive"] = player.PawnIsAlive;
        PopulateOptional("连接", () =>
        {
            table["ip_address"] = player.IpAddress;
            table["ping"] = player.Ping;
        });
        PopulateOptional("计分板", () =>
        {
            table["score"] = player.Score;
            table["round_score"] = player.RoundScore;
            table["rounds_won"] = player.RoundsWon;
            table["mvps"] = player.MVPs;
            table["teammate_color"] = player.CompTeammateColor;
        });
        PopulateOptional("语言与语音", () =>
        {
            table["language"] = player.GetLanguage().Name;
            table["voice_flags"] = (long)player.VoiceFlags;
        });
        PopulateOptional("经济与装备", () =>
        {
            table["money"] = player.InGameMoneyServices?.Account;
            table["has_helmet"] = player.PawnHasHelmet;
            table["has_defuser"] = player.PawnHasDefuser;
        });
        PopulateOptional("Pawn", () =>
        {
            pawn = player.PlayerPawn.Value is { IsValid: true } validPawn ? validPawn : null;
            table["health"] = pawn?.Health;
            table["max_health"] = pawn?.MaxHealth;
            table["armor"] = pawn?.ArmorValue;
            table["in_buy_zone"] = pawn?.InBuyZone;
            table["in_bomb_zone"] = pawn?.InBombZone;
            table["is_scoped"] = pawn?.IsScoped;
            table["is_defusing"] = pawn?.IsDefusing;
            table["is_grabbing_hostage"] = pawn?.IsGrabbingHostage;
            table["is_walking"] = pawn?.IsWalking;
            table["shots_fired"] = pawn?.ShotsFired;
            table["velocity_modifier"] = pawn?.VelocityModifier;
            table["gravity_scale"] = pawn?.GravityScale;
            table["flags"] = pawn is null ? null : (long)pawn.Flags;
            table["buttons"] = ReadButtons(pawn);
        });
        PopulateOptional("坐标", () =>
        {
            if (pawn?.AbsOrigin is { } position)
            {
                using var positionTable = CreateVectorTable(position.X, position.Y, position.Z);
                table["position"] = positionTable;
            }
            if (pawn is not null)
            {
                using var velocityTable = CreateVectorTable(pawn.AbsVelocity.X, pawn.AbsVelocity.Y, pawn.AbsVelocity.Z);
                using var anglesTable = CreateVectorTable(pawn.EyeAngles.X, pawn.EyeAngles.Y, pawn.EyeAngles.Z);
                table["velocity"] = velocityTable;
                table["eye_angles"] = anglesTable;
            }
        });
        PopulateOptional("模型", () =>
        {
            if (pawn?.CBodyComponent?.SceneNode is not { } sceneNode) return;
            table["model"] = sceneNode.GetSkeletonInstance().ModelState.ModelName;
            using var colorTable = CreateColorTable(pawn.Render);
            table["render_color"] = colorTable;
        });

        var weapons = NewTable();
        var weaponDetails = NewTable();
        var weaponIndex = 1;
        PopulateOptional("武器", () =>
        {
            var activeWeapon = pawn?.WeaponServices?.ActiveWeapon.Value is { IsValid: true } validWeapon ? validWeapon : null;
            table["active_weapon"] = activeWeapon?.DesignerName;
            if (activeWeapon is not null)
            {
                using var activeWeaponTable = CreateWeaponTable(activeWeapon);
                table["active_weapon_info"] = activeWeaponTable;
            }

            if (pawn?.WeaponServices is not { } weaponServices) return;
            foreach (var weapon in weaponServices.MyWeapons.Select(handle => handle.Value).Where(weapon => weapon is { IsValid: true }))
            {
                weapons[weaponIndex] = weapon!.DesignerName;
                using var weaponTable = CreateWeaponTable(weapon);
                weaponDetails[weaponIndex] = weaponTable;
                weaponIndex++;
            }
        });
        table["weapons"] = weapons;
        table["weapon_details"] = weaponDetails;
        table["snapshot_complete"] = snapshotComplete;
        weapons.Dispose();
        weaponDetails.Dispose();

        table["print_chat"] = _playerChatMethod;
        table["print_console"] = _playerConsoleMethod;
        table["print_center"] = _playerCenterMethod;
        table["print_alert"] = _playerAlertMethod;
        table["print_html"] = _playerHtmlMethod;
        table["refresh"] = _playerRefreshMethod;
        table["has_permission"] = _playerPermissionMethod;
        table["can_target"] = _playerCanTargetMethod;
        table["get_convar"] = _playerConVarMethod;
        table["execute"] = _playerExecuteMethod;
        table["execute_as_server"] = _playerExecuteServerMethod;
        table["give_item"] = _playerGiveItemMethod;
        table["give_weapon"] = _playerGiveWeaponMethod;
        table["remove_item"] = _playerRemoveItemMethod;
        table["remove_weapons"] = _playerRemoveWeaponsMethod;
        table["drop_active_weapon"] = _playerDropWeaponMethod;
        table["respawn"] = _playerRespawnMethod;
        table["kill"] = _playerKillMethod;
        table["kick"] = _playerKickMethod;
        table["change_team"] = _playerChangeTeamMethod;
        table["switch_team"] = _playerSwitchTeamMethod;
        table["teleport"] = _playerTeleportMethod;
        table["set_health"] = _playerSetHealthMethod;
        table["set_armor"] = _playerSetArmorMethod;
        table["set_money"] = _playerSetMoneyMethod;
        table["aim_target"] = _playerAimTargetMethod;
        table["set_max_health"] = _playerSetMaxHealthMethod;
        table["set_gravity"] = _playerSetGravityMethod;
        table["set_velocity_modifier"] = _playerSetVelocityModifierMethod;
        table["set_model"] = _playerSetModelMethod;
        table["set_render_color"] = _playerSetRenderColorMethod;
        table["close_menu"] = _playerCloseMenuMethod;
        table["set_ammo"] = _playerSetAmmoMethod;
        table["set_score"] = _playerSetScoreMethod;
        table["set_round_score"] = _playerSetRoundScoreMethod;
        table["set_mvps"] = _playerSetMvpsMethod;
        table["set_voice_flags"] = _playerSetVoiceFlagsMethod;
        table["replicate_convar"] = _playerReplicateConVarMethod;
        table["set_fake_convar"] = _playerSetFakeConVarMethod;
        table["emit_sound"] = _playerEmitSoundMethod;
        return table;
    }

    private LuaTable CreateWeaponTable(CBasePlayerWeapon weapon)
    {
        var table = NewTable();
        table["handle"] = (long)weapon.EntityHandle.Raw;
        table["index"] = (long)weapon.Index;
        table["designer_name"] = weapon.DesignerName;
        table["clip"] = weapon.Clip1;
        table["clip_secondary"] = weapon.Clip2;
        table["reserve"] = weapon.ReserveAmmo[0];
        table["reserve_secondary"] = weapon.ReserveAmmo[1];
        try
        {
            var item = weapon.AttributeManager.Item;
            table["item_definition_index"] = item.ItemDefinitionIndex;
            table["paint_kit"] = weapon.FallbackPaintKit;
            table["paint_seed"] = weapon.FallbackSeed;
            table["paint_wear"] = weapon.FallbackWear;
            table["stattrak"] = weapon.FallbackStatTrak;
            table["entity_quality"] = item.EntityQuality;
            table["entity_level"] = item.EntityLevel;
            table["item_id"] = item.ItemID.ToString(CultureInfo.InvariantCulture);
            table["account_id"] = item.AccountID.ToString(CultureInfo.InvariantCulture);
            table["inventory_position"] = item.InventoryPosition.ToString(CultureInfo.InvariantCulture);
            table["custom_name"] = item.CustomName;
            table["custom_name_override"] = item.CustomNameOverride;
            var originalOwner = ((ulong)weapon.OriginalOwnerXuidHigh << 32) | weapon.OriginalOwnerXuidLow;
            table["original_owner_steam_id"] = originalOwner.ToString(CultureInfo.InvariantCulture);
            table["econ_available"] = true;
        }
        catch (Exception)
        {
            // CSS 的 FollowCS2ServerGuidelines 会禁止访问部分经济字段；核心武器快照仍应可用。
            table["econ_available"] = false;
        }
        table["owner_handle"] = (long)weapon.OwnerEntity.Raw;

        if (weapon.AbsOrigin is { } position)
        {
            using var positionTable = CreateVectorTable(position.X, position.Y, position.Z);
            table["position"] = positionTable;
        }
        if (weapon.AbsRotation is { } rotation)
        {
            using var rotationTable = CreateVectorTable(rotation.X, rotation.Y, rotation.Z);
            table["rotation"] = rotationTable;
        }
        using var velocityTable = CreateVectorTable(weapon.AbsVelocity.X, weapon.AbsVelocity.Y, weapon.AbsVelocity.Z);
        table["velocity"] = velocityTable;
        table["refresh"] = _weaponRefreshMethod;
        table["set_ammo"] = _weaponSetAmmoMethod;
        table["set_econ"] = _weaponSetEconMethod;
        table["remove"] = _entityRemoveMethod;
        table["teleport"] = _entityTeleportMethod;
        return table;
    }

    private LuaTable CreateEntityTable(CBaseEntity entity)
    {
        var table = NewTable();
        table["handle"] = (long)entity.EntityHandle.Raw;
        table["index"] = (long)entity.Index;
        table["designer_name"] = entity.DesignerName;
        table["name"] = entity.Entity?.Name;
        table["health"] = entity.Health;
        table["max_health"] = entity.MaxHealth;
        table["team_id"] = entity.TeamNum;
        table["gravity_scale"] = entity.GravityScale;
        table["flags"] = (long)entity.Flags;
        table["spawn_flags"] = (long)entity.Spawnflags;

        if (entity.AbsOrigin is { } position)
        {
            using var positionTable = CreateVectorTable(position.X, position.Y, position.Z);
            table["position"] = positionTable;
        }
        if (entity.AbsRotation is { } rotation)
        {
            using var rotationTable = CreateVectorTable(rotation.X, rotation.Y, rotation.Z);
            table["rotation"] = rotationTable;
        }

        using var velocityTable = CreateVectorTable(entity.AbsVelocity.X, entity.AbsVelocity.Y, entity.AbsVelocity.Z);
        table["velocity"] = velocityTable;
        table["refresh"] = _entityRefreshMethod;
        table["spawn"] = _entitySpawnMethod;
        table["input"] = _entityInputMethod;
        table["remove"] = _entityRemoveMethod;
        table["teleport"] = _entityTeleportMethod;
        table["set_health"] = _entitySetHealthMethod;
        table["set_max_health"] = _entitySetMaxHealthMethod;
        table["set_gravity"] = _entitySetGravityMethod;
        table["set_model"] = _entitySetModelMethod;
        table["set_render_color"] = _entitySetRenderColorMethod;
        table["emit_sound"] = _entityEmitSoundMethod;
        return table;
    }

    internal static bool IsUsablePlayer(CCSPlayerController? player) =>
        player is { IsValid: true, Connected: PlayerConnectedState.Connected };

    private CCSPlayerController? ResolvePlayer(long slot)
    {
        if (slot is < 0 or > 255) return null;
        var player = Utilities.GetPlayerFromSlot((int)slot);
        return IsUsablePlayer(player) ? player : null;
    }

    private CCSPlayerPawn? ResolvePlayerPawn(long slot)
    {
        var pawn = ResolvePlayer(slot)?.PlayerPawn.Value;
        return pawn is { IsValid: true } ? pawn : null;
    }

    private CBasePlayerWeapon? ResolveActiveWeapon(long slot)
    {
        var weapon = ResolvePlayerPawn(slot)?.WeaponServices?.ActiveWeapon.Value;
        return weapon is { IsValid: true } ? weapon : null;
    }

    private static CBaseEntity? ResolveEntity(long handle)
    {
        if (handle is < 0 or > uint.MaxValue) return null;
        var entity = new CHandle<CBaseEntity>((uint)handle).Value;
        return entity is { IsValid: true } ? entity : null;
    }

    private static CBasePlayerWeapon? ResolveWeapon(long handle)
    {
        if (handle is < 0 or > uint.MaxValue) return null;
        var weapon = new CHandle<CBasePlayerWeapon>((uint)handle).Value;
        return weapon is { IsValid: true } ? weapon : null;
    }

    private static CBaseModelEntity? ResolveModelEntity(long handle)
    {
        if (handle is < 0 or > uint.MaxValue) return null;
        var entity = new CHandle<CBaseModelEntity>((uint)handle).Value;
        return entity is { IsValid: true } ? entity : null;
    }

    private static CCSGameRules? ResolveGameRules() =>
        Utilities.FindAllEntitiesByDesignerName<CCSGameRulesProxy>("cs_gamerules")
            .FirstOrDefault(entity => entity.IsValid)?.GameRules;

    private static void ValidateSoundParameters(double volume, double pitch)
    {
        if (!double.IsFinite(volume)) throw new ArgumentOutOfRangeException(nameof(volume), "音量必须是有限数值。");
        if (!double.IsFinite(pitch)) throw new ArgumentOutOfRangeException(nameof(pitch), "音调必须是有限数值。");
    }

    private CCSPlayerController? ResolvePlayerReference(LuaTable? table)
    {
        if (table?["slot"] is null) return null;
        var slot = Convert.ToInt64(table["slot"], CultureInfo.InvariantCulture);
        var player = ResolvePlayer(slot);
        if (player is null) return null;
        return IsCurrentPlayer(slot, table["user_id"], table["steam_id"]?.ToString()) ? player : null;
    }

    private LuaTable CreatePlayerList(IEnumerable<CCSPlayerController> players)
    {
        var table = NewTable();
        var index = 1;
        foreach (var player in players)
        {
            using var playerTable = CreatePlayerTable(player);
            table[index++] = playerTable;
        }
        return table;
    }

    private LuaTable CreateEntityList(IEnumerable<CBaseEntity> entities)
    {
        var table = NewTable();
        var index = 1;
        foreach (var entity in entities)
        {
            using var entityTable = CreateEntityTable(entity);
            table[index++] = entityTable;
        }
        return table;
    }

    private LuaTable CreateNavAreaTable(CCSNavArea area)
    {
        var table = NewTable();
        table["id"] = (long)area.Id;
        table["width"] = area.Width;
        table["height"] = area.Height;
        table["area_2d"] = area.Area2D;
        using var center = CreateVectorTable(area.Center.X, area.Center.Y, area.Center.Z);
        using var normal = CreateVectorTable(area.Normal.X, area.Normal.Y, area.Normal.Z);
        using var min = CreateVectorTable(area.Min.X, area.Min.Y, area.Min.Z);
        using var max = CreateVectorTable(area.Max.X, area.Max.Y, area.Max.Z);
        table["center"] = center;
        table["normal"] = normal;
        table["min"] = min;
        table["max"] = max;
        return table;
    }

    private static long ReadButtons(CCSPlayerPawn? pawn)
    {
        if (pawn?.MovementServices is null) return 0;
        return unchecked((long)pawn.MovementServices.Buttons.ButtonStates[0]);
    }

    private LuaTable CreateStringList(IEnumerable<string> values)
    {
        var table = NewTable();
        var index = 1;
        foreach (var value in values) table[index++] = value;
        return table;
    }

    private Dictionary<string, JsonElement> LoadStorage()
    {
        if (!File.Exists(StoragePath)) return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        try
        {
            var data = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(File.ReadAllText(StoragePath))
                       ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var value in data.Values) _ = ConvertJsonValue(value);
            return data;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            _logger.LogWarning(exception, "无法读取 Lua 插件 {Plugin} 的持久化数据，将使用空数据", _plugin.Name);
            return new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        }
    }

    private void SaveStorage(Dictionary<string, JsonElement> data)
    {
        var directory = Path.GetDirectoryName(StoragePath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = StoragePath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            var serializable = data.ToDictionary(pair => pair.Key, pair => ConvertJsonValue(pair.Value), StringComparer.Ordinal);
            var json = JsonSerializer.Serialize(serializable, StorageJsonOptions);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, StoragePath, true);
        }
        finally
        {
            if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
        }
    }

    private static JsonElement SerializeStorageValue(object? value)
    {
        if (value is not string and not bool
            and not byte and not sbyte and not short and not ushort
            and not int and not uint and not long and not ulong
            and not float and not double and not decimal)
        {
            throw new InvalidDataException("持久化存储只支持字符串、布尔值和数值；设置 nil 会删除键。");
        }

        if (value is float single && !float.IsFinite(single)
            || value is double number && !double.IsFinite(number))
        {
            throw new InvalidDataException("持久化存储不支持 NaN 或无穷大。");
        }

        return JsonSerializer.SerializeToElement(value, value.GetType(), StorageJsonOptions);
    }

    private static object? ConvertJsonValue(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null => null,
        JsonValueKind.String => value.GetString(),
        JsonValueKind.True => true,
        JsonValueKind.False => false,
        JsonValueKind.Number when value.TryGetInt64(out var integer) => integer,
        JsonValueKind.Number when value.TryGetDouble(out var number) && double.IsFinite(number) => number,
        JsonValueKind.Number => throw new InvalidDataException("持久化数据包含非有限或超出范围的数值。"),
        _ => throw new InvalidDataException("持久化数据包含不支持的 JSON 类型。")
    };

    private static string ValidateStorageKey(string key)
    {
        key = key.Trim();
        if (string.IsNullOrEmpty(key) || key.Length > 128 || key.Any(char.IsControl))
        {
            throw new ArgumentException("持久化键必须为 1 到 128 个非控制字符。", nameof(key));
        }
        return key;
    }

    private static string ValidateDesignerName(string designerName)
    {
        designerName = designerName.Trim();
        if (string.IsNullOrEmpty(designerName) || designerName.Length > 128
            || designerName.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
        {
            throw new ArgumentException("实体 Designer Name 只能包含 ASCII 字母、数字和下划线。", nameof(designerName));
        }
        return designerName;
    }

    private static string ValidateWeaponDesignerName(string designerName)
    {
        designerName = ValidateDesignerName(designerName);
        if (!designerName.StartsWith("weapon_", StringComparison.Ordinal))
        {
            throw new ArgumentException("武器 Designer Name 必须以 weapon_ 开头。", nameof(designerName));
        }
        return designerName;
    }

    private static void ApplyWeaponOptions(CBasePlayerWeapon weapon, LuaTable? options)
    {
        if (options is null) return;
        ApplyWeaponAmmo(
            weapon,
            ReadOptionalAmmo(options, "clip"),
            ReadOptionalAmmo(options, "reserve"),
            ReadOptionalAmmo(options, "clip_secondary"),
            ReadOptionalAmmo(options, "reserve_secondary"));
        ApplyWeaponEcon(weapon, options);
    }

    private static void ValidateWeaponOptions(LuaTable? options)
    {
        if (options is null) return;
        _ = ReadOptionalAmmo(options, "clip");
        _ = ReadOptionalAmmo(options, "reserve");
        _ = ReadOptionalAmmo(options, "clip_secondary");
        _ = ReadOptionalAmmo(options, "reserve_secondary");
        _ = ReadOptionalInteger(options, "paint_kit", int.MinValue, int.MaxValue);
        _ = ReadOptionalInteger(options, "paint_seed", int.MinValue, int.MaxValue);
        _ = ReadOptionalInteger(options, "stattrak", -1, int.MaxValue);
        _ = ReadOptionalFiniteNumber(options, "paint_wear", 0, 1);
        _ = ReadOptionalInteger(options, "item_definition_index", 0, ushort.MaxValue);
        _ = ReadOptionalInteger(options, "entity_quality", int.MinValue, int.MaxValue);
        _ = ReadOptionalInteger(options, "entity_level", 0, uint.MaxValue);
        _ = ReadOptionalUnsigned(options, "item_id", ulong.MaxValue);
        _ = ReadOptionalUnsigned(options, "account_id", uint.MaxValue);
        _ = ReadOptionalUnsigned(options, "inventory_position", uint.MaxValue);
        _ = ReadOptionalUnsigned(options, "original_owner_steam_id", ulong.MaxValue);
        if (options["custom_name"] is not null) _ = ValidateWeaponName(options["custom_name"]?.ToString() ?? string.Empty);
        if (options["custom_name_override"] is not null)
        {
            _ = ValidateWeaponName(options["custom_name_override"]?.ToString() ?? string.Empty);
        }
    }

    private static void ApplyWeaponAmmo(CBasePlayerWeapon weapon, long clip, long reserve, long clipSecondary, long reserveSecondary)
    {
        ValidateOptionalAmmo(clip, nameof(clip));
        ValidateOptionalAmmo(reserve, nameof(reserve));
        ValidateOptionalAmmo(clipSecondary, nameof(clipSecondary));
        ValidateOptionalAmmo(reserveSecondary, nameof(reserveSecondary));

        if (clip >= 0)
        {
            weapon.Clip1 = (int)clip;
            Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_iClip1");
        }
        if (clipSecondary >= 0)
        {
            weapon.Clip2 = (int)clipSecondary;
            Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_iClip2");
        }
        if (reserve >= 0) weapon.ReserveAmmo[0] = (int)reserve;
        if (reserveSecondary >= 0) weapon.ReserveAmmo[1] = (int)reserveSecondary;
        if (reserve >= 0 || reserveSecondary >= 0)
        {
            Utilities.SetStateChanged(weapon, "CBasePlayerWeapon", "m_pReserveAmmo");
        }
    }

    private static void ApplyWeaponEcon(CBasePlayerWeapon weapon, LuaTable options)
    {
        var paintKit = ReadOptionalInteger(options, "paint_kit", int.MinValue, int.MaxValue);
        var paintSeed = ReadOptionalInteger(options, "paint_seed", int.MinValue, int.MaxValue);
        var stattrak = ReadOptionalInteger(options, "stattrak", -1, int.MaxValue);
        var wear = ReadOptionalFiniteNumber(options, "paint_wear", 0, 1);
        var definition = ReadOptionalInteger(options, "item_definition_index", 0, ushort.MaxValue);
        var quality = ReadOptionalInteger(options, "entity_quality", int.MinValue, int.MaxValue);
        var level = ReadOptionalInteger(options, "entity_level", 0, uint.MaxValue);
        var itemId = ReadOptionalUnsigned(options, "item_id", ulong.MaxValue);
        var accountId = ReadOptionalUnsigned(options, "account_id", uint.MaxValue);
        var inventoryPosition = ReadOptionalUnsigned(options, "inventory_position", uint.MaxValue);
        var originalOwner = ReadOptionalUnsigned(options, "original_owner_steam_id", ulong.MaxValue);

        if (paintKit != long.MinValue)
        {
            weapon.FallbackPaintKit = (int)paintKit;
            Utilities.SetStateChanged(weapon, "CEconEntity", "m_nFallbackPaintKit");
        }
        if (paintSeed != long.MinValue)
        {
            weapon.FallbackSeed = (int)paintSeed;
            Utilities.SetStateChanged(weapon, "CEconEntity", "m_nFallbackSeed");
        }
        if (wear.HasValue)
        {
            weapon.FallbackWear = (float)wear.Value;
            Utilities.SetStateChanged(weapon, "CEconEntity", "m_flFallbackWear");
        }
        if (stattrak != long.MinValue)
        {
            weapon.FallbackStatTrak = (int)stattrak;
            Utilities.SetStateChanged(weapon, "CEconEntity", "m_nFallbackStatTrak");
        }
        if (originalOwner.HasValue)
        {
            weapon.OriginalOwnerXuidLow = (uint)(originalOwner.Value & uint.MaxValue);
            weapon.OriginalOwnerXuidHigh = (uint)(originalOwner.Value >> 32);
        }

        var item = weapon.AttributeManager.Item;
        if (definition != long.MinValue) item.ItemDefinitionIndex = (ushort)definition;
        if (quality != long.MinValue) item.EntityQuality = (int)quality;
        if (level != long.MinValue) item.EntityLevel = (uint)level;
        if (itemId.HasValue)
        {
            item.ItemID = itemId.Value;
            item.ItemIDLow = (uint)(itemId.Value & uint.MaxValue);
            item.ItemIDHigh = (uint)(itemId.Value >> 32);
        }
        if (accountId.HasValue) item.AccountID = (uint)accountId.Value;
        if (inventoryPosition.HasValue) item.InventoryPosition = (uint)inventoryPosition.Value;
        if (options["custom_name"] is not null) item.CustomName = ValidateWeaponName(options["custom_name"]?.ToString() ?? string.Empty);
        if (options["custom_name_override"] is not null)
        {
            item.CustomNameOverride = ValidateWeaponName(options["custom_name_override"]?.ToString() ?? string.Empty);
        }
    }

    private static void ValidateOptionalAmmo(long value, string parameterName)
    {
        if (value is < -1 or > 10_000)
        {
            throw new ArgumentOutOfRangeException(parameterName, "弹药数量必须为 0 到 10000，或使用 nil 保持不变。");
        }
    }

    private static long ReadOptionalInteger(LuaTable table, string key, long minimum, long maximum)
    {
        var value = table[key];
        if (value is null) return long.MinValue;
        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number) || number != Math.Truncate(number) || number < minimum || number > maximum)
        {
            throw new ArgumentOutOfRangeException(key, $"{key} 必须是 {minimum} 到 {maximum} 的整数。");
        }
        return (long)number;
    }

    private static long ReadOptionalAmmo(LuaTable table, string key)
    {
        var value = ReadOptionalInteger(table, key, 0, 10_000);
        return value == long.MinValue ? -1 : value;
    }

    private static double? ReadOptionalFiniteNumber(LuaTable table, string key, double minimum, double maximum)
    {
        var value = table[key];
        if (value is null) return null;
        var number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
        if (!double.IsFinite(number) || number < minimum || number > maximum)
        {
            throw new ArgumentOutOfRangeException(key, $"{key} 必须是 {minimum} 到 {maximum} 的有限数值。");
        }
        return number;
    }

    private static ulong? ReadOptionalUnsigned(LuaTable table, string key, ulong maximum)
    {
        var value = table[key];
        if (value is null) return null;
        if (!ulong.TryParse(value.ToString(), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed > maximum)
        {
            throw new ArgumentOutOfRangeException(key, $"{key} 必须是 0 到 {maximum} 的无符号整数或十进制字符串。");
        }
        return parsed;
    }

    private static string ValidateWeaponName(string value)
    {
        if (value.Any(character => character is '\0' or '\r' or '\n') || Encoding.UTF8.GetByteCount(value) > 160)
        {
            throw new ArgumentException("武器自定义名称不能包含换行或空字符，UTF-8 编码后不能超过 160 字节。", nameof(value));
        }
        return value;
    }

    private static string ValidateResourceName(string value, string label)
    {
        value = value.Trim();
        if (string.IsNullOrEmpty(value) || value.Length > 256 || value.Any(char.IsControl))
        {
            throw new ArgumentException($"{label}必须为 1 到 256 个非控制字符。", nameof(value));
        }
        return value;
    }

    internal static string ValidateConVarName(string name)
    {
        name = name.Trim();
        if (string.IsNullOrEmpty(name) || name.Length > 128 || name.Any(char.IsWhiteSpace) || name.Any(char.IsControl))
        {
            throw new ArgumentException("ConVar 名称必须为 1 到 128 个不含空白或控制字符的字符。", nameof(name));
        }
        return name;
    }

    internal static string ValidateConVarValue(string value)
    {
        if (value.Length > 4096 || value.Any(character => character is '\0' or '\r' or '\n'))
        {
            throw new ArgumentException("ConVar 值不能包含换行或空字符，长度不能超过 4096。", nameof(value));
        }
        return value;
    }

    private static string ValidateDisplayText(string value, string label, int maximumLength)
    {
        if (value.Length > maximumLength || value.Any(character => character is '\0' or '\r' or '\n'))
        {
            throw new ArgumentException($"{label}不能包含换行或空字符，长度不能超过 {maximumLength}。", nameof(value));
        }
        return value;
    }

    private static float ClampFinite(double value, float minimum, float maximum, string parameterName)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(parameterName, "参数必须是有限数值。");
        return (float)Math.Clamp(value, minimum, maximum);
    }

    private static int ClampCounter(long value, string parameterName)
    {
        if (value < 0) throw new ArgumentOutOfRangeException(parameterName, "计数值不能为负数。");
        return (int)Math.Min(value, int.MaxValue);
    }

    private static Color CreateColor(long red, long green, long blue, long alpha) => Color.FromArgb(
        (int)Math.Clamp(alpha, 0, 255),
        (int)Math.Clamp(red, 0, 255),
        (int)Math.Clamp(green, 0, 255),
        (int)Math.Clamp(blue, 0, 255));

    internal static RoundEndReason ParseRoundEndReason(string reason)
    {
        var normalized = reason.Trim().Replace("_", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "targetbombed" or "bombed" => RoundEndReason.TargetBombed,
            "terroristsescaped" => RoundEndReason.TerroristsEscaped,
            "ctspreventescape" => RoundEndReason.CTsPreventEscape,
            "escapingterroristsneutralized" => RoundEndReason.EscapingTerroristsNeutralized,
            "bombdefused" or "defused" => RoundEndReason.BombDefused,
            "ctswin" or "ctwin" or "ct" => RoundEndReason.CTsWin,
            "terroristswin" or "terroristwin" or "twin" or "t" => RoundEndReason.TerroristsWin,
            "rounddraw" or "draw" => RoundEndReason.RoundDraw,
            "allhostagerescued" => RoundEndReason.AllHostageRescued,
            "targetsaved" => RoundEndReason.TargetSaved,
            "hostagesnotrescued" => RoundEndReason.HostagesNotRescued,
            "terroristsnotescaped" => RoundEndReason.TerroristsNotEscaped,
            "gamecommencing" => RoundEndReason.GameCommencing,
            "terroristssurrender" => RoundEndReason.TerroristsSurrender,
            "ctssurrender" => RoundEndReason.CTsSurrender,
            "terroristsplanted" => RoundEndReason.TerroristsPlanted,
            "ctsreachedhostage" => RoundEndReason.CTsReachedHostage,
            "survivalwin" => RoundEndReason.SurvivalWin,
            "survivaldraw" => RoundEndReason.SurvivalDraw,
            _ => throw new ArgumentException($"未知回合结束原因：{reason}", nameof(reason))
        };
    }

    internal static PostSelectAction ParsePostSelectAction(string action) => action.Trim().ToLowerInvariant() switch
    {
        "close" => PostSelectAction.Close,
        "reset" => PostSelectAction.Reset,
        "nothing" or "keep" => PostSelectAction.Nothing,
        _ => throw new ArgumentException("菜单 post_select 必须是 close、reset 或 nothing。", nameof(action))
    };

    private LuaTable CreateVectorTable(float x, float y, float z)
    {
        var table = NewTable();
        table["x"] = x;
        table["y"] = y;
        table["z"] = z;
        table[1] = x;
        table[2] = y;
        table[3] = z;
        return table;
    }

    private LuaTable CreateColorTable(Color color)
    {
        var table = NewTable();
        table["red"] = color.R;
        table["green"] = color.G;
        table["blue"] = color.B;
        table["alpha"] = color.A;
        return table;
    }

    private static System.Numerics.Vector3? ReadVector(LuaTable? table)
    {
        if (table is null) return null;
        var x = ReadNumber(table["x"] ?? table[1], "x");
        var y = ReadNumber(table["y"] ?? table[2], "y");
        var z = ReadNumber(table["z"] ?? table[3], "z");
        return new System.Numerics.Vector3(x, y, z);
    }

    private static float ReadNumber(object? value, string field)
    {
        if (value is null) throw new InvalidDataException($"向量缺少 {field} 分量。");
        var number = Convert.ToSingle(value, CultureInfo.InvariantCulture);
        if (!float.IsFinite(number)) throw new InvalidDataException($"向量的 {field} 分量必须是有限数值。");
        return number;
    }

    internal static CsTeam ParseTeam(object? value)
    {
        if (value is long or int or double)
        {
            var number = Convert.ToInt32(value, CultureInfo.InvariantCulture);
            if (number is >= 0 and <= 3 && Enum.IsDefined(typeof(CsTeam), (byte)number)) return (CsTeam)number;
        }

        return value?.ToString()?.Trim().ToLowerInvariant() switch
        {
            "0" or "none" => CsTeam.None,
            "1" or "spec" or "spectator" => CsTeam.Spectator,
            "2" or "t" or "terrorist" => CsTeam.Terrorist,
            "3" or "ct" or "counterterrorist" or "counter_terrorist" => CsTeam.CounterTerrorist,
            _ => throw new ArgumentException("队伍必须是 none、spectator、t、ct 或 0 到 3。", nameof(value))
        };
    }

    private LuaTable NewTable()
    {
        var state = _plugin.State.State;
        state.NewTable();
        var reference = state.Ref(LuaRegistry.Index);
        return new LuaTable(reference, _plugin.State);
    }

    private void SetMappedValue(LuaTable table, string key, object? value)
    {
        if (value is CCSPlayerController player && IsUsablePlayer(player))
        {
            using var playerTable = CreatePlayerTable(player);
            table[key] = playerTable;
        }
        else if (value is SteamID steamId)
        {
            table[key] = steamId.SteamId64.ToString(CultureInfo.InvariantCulture);
        }
        else if (value is Vector vector)
        {
            using var vectorTable = CreateVectorTable(vector.X, vector.Y, vector.Z);
            table[key] = vectorTable;
        }
        else if (value is QAngle angle)
        {
            using var angleTable = CreateVectorTable(angle.X, angle.Y, angle.Z);
            table[key] = angleTable;
        }
        else if (value is ulong unsigned)
        {
            table[key] = unsigned.ToString(CultureInfo.InvariantCulture);
        }
        else if (value?.GetType().IsEnum == true)
        {
            table[key] = value.ToString();
        }
        else if (IsLuaPrimitive(value))
        {
            table[key] = value;
        }
    }

    private static IEnumerable<PropertyInfo> EventProperties(Type eventType) => eventType
        .GetProperties(BindingFlags.Instance | BindingFlags.Public)
        .Where(property => property.DeclaringType != typeof(GameEvent)
                           && property.DeclaringType != typeof(NativeObject)
                           && property.GetIndexParameters().Length == 0
                           && property.CanRead);

    private static bool IsLuaPrimitive(object? value) => value is null or string or bool
        or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal;

    internal static object? ConvertLuaValue(object? value, Type targetType)
    {
        var nullableType = Nullable.GetUnderlyingType(targetType);
        targetType = nullableType ?? targetType;
        if (value is null) return nullableType is not null || !targetType.IsValueType ? null : Activator.CreateInstance(targetType);
        if (targetType == typeof(string)) return value.ToString();
        if (targetType == typeof(bool)) return value is bool boolean ? boolean : Convert.ToBoolean(value, CultureInfo.InvariantCulture);
        if (targetType == typeof(ulong)) return ulong.Parse(value.ToString()!, CultureInfo.InvariantCulture);
        if (targetType == typeof(Vector) && value is LuaTable vectorTable)
        {
            var vector = ReadVector(vectorTable)!.Value;
            return new Vector(vector.X, vector.Y, vector.Z);
        }
        if (targetType == typeof(QAngle) && value is LuaTable angleTable)
        {
            var angle = ReadVector(angleTable)!.Value;
            return new QAngle(angle.X, angle.Y, angle.Z);
        }
        if (targetType.IsEnum) return Enum.Parse(targetType, value.ToString()!, true);
        if (typeof(CCSPlayerController).IsAssignableFrom(targetType) && value is LuaTable playerTable)
        {
            var slot = Convert.ToInt32(playerTable["slot"], CultureInfo.InvariantCulture);
            return Utilities.GetPlayerFromSlot(slot);
        }

        return Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
    }

    internal static string ToSnakeCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var result = new StringBuilder(value.Length + 8);
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (char.IsUpper(character) && index > 0 && (char.IsLower(value[index - 1]) || index + 1 < value.Length && char.IsLower(value[index + 1])))
            {
                result.Append('_');
            }
            result.Append(char.ToLowerInvariant(character));
        }
        return result.ToString();
    }

    private static string ReadString(LuaTable table, string key, bool required = false, string defaultValue = "")
    {
        var value = table[key]?.ToString()?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            if (required) throw new InvalidDataException($"Lua plugin field '{key}' is required.");
            return defaultValue;
        }
        return value;
    }

    private static bool ReadBool(LuaTable table, string key, bool defaultValue) => table[key] is bool value ? value : defaultValue;

    private static int ReadInt(LuaTable table, string key, int defaultValue)
    {
        var value = table[key];
        return value is null ? defaultValue : Convert.ToInt32(value, CultureInfo.InvariantCulture);
    }

    public void Dispose()
    {
        foreach (var instance in MenuManager.GetActiveMenus().Values.OfType<BaseMenuInstance>().ToArray())
        {
            if (instance.Menu is BaseMenu menu
                && _ownedMenus.TryGetValue(instance.Player.Handle, out var ownedMenu)
                && ReferenceEquals(ownedMenu, menu))
            {
                MenuManager.CloseActiveMenu(instance.Player);
            }
        }
        _ownedMenus.Clear();
        _commandContexts.Clear();
    }

    internal sealed class LuaEventSnapshot(
        LuaApi api,
        GameEvent gameEvent,
        GameEventInfo info,
        LuaTable eventTable,
        LuaTable infoTable,
        PropertyInfo[] properties,
        bool writable) : IDisposable
    {
        public LuaTable Event => eventTable;
        public LuaTable Info => infoTable;

        public void Apply()
        {
            if (!writable) return;
            foreach (var property in properties.Where(item => item.CanWrite))
            {
                var key = ToSnakeCase(property.Name);
                try
                {
                    property.SetValue(gameEvent, ConvertLuaValue(eventTable[key], property.PropertyType));
                }
                catch (Exception exception)
                {
                    api._logger.LogWarning(exception, "Unable to write Lua event field {Field}", key);
                }
            }

            if (infoTable["dont_broadcast"] is bool dontBroadcast)
            {
                info.DontBroadcast = dontBroadcast;
            }
        }

        public void Dispose()
        {
            eventTable.Dispose();
            infoTable.Dispose();
        }
    }

    internal sealed class LuaMappedArguments(object?[] values, List<IDisposable> owned) : IDisposable
    {
        public object?[] Values { get; } = values;
        public void Dispose()
        {
            foreach (var item in owned) item.Dispose();
        }
    }

    internal sealed class LuaCommandSnapshot(LuaTable table, Action dispose) : IDisposable
    {
        public LuaTable Table { get; } = table;
        public void Dispose()
        {
            dispose();
            Table.Dispose();
        }
    }
}
