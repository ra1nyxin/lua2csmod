using Microsoft.Extensions.Logging.Abstractions;

namespace Lua2CS.Tests;

public sealed class LuaRuntimeTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lua2cs-tests", Guid.NewGuid().ToString("N"));

    public LuaRuntimeTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void NativeRuntimeProbeConfirmsLua54() =>
        Assert.StartsWith("Lua 5.4", LuaRuntime.ProbeRuntimeVersion(), StringComparison.Ordinal);

    [Fact]
    public void PrepareLoadsMetadataAndRegistrationDefinitions()
    {
        var path = WriteScript("sample.lua", """
            local plugin = cs.plugin({
                name = "Sample",
                version = "1.2.3",
                description = "test plugin"
            })

            plugin:on("player_death", function(event, info)
                return cs.continue
            end, { mode = "pre" })

            plugin:listen("OnMapStart", function(map_name) end)
            plugin:command("css_sample", {
                description = "sample command",
                permission = "@css/generic",
                min_args = 1,
                usage = "<value>"
            }, function(player, command) end)
            plugin:timer(1.5, function() end, { repeating = true })
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);

        Assert.Equal("Sample", plugin.Name);
        Assert.Equal("1.2.3", plugin.Version);
        Assert.Equal("test plugin", plugin.Description);
        Assert.Collection(
            plugin.Registrations,
            item => Assert.IsType<EventRegistration>(item),
            item => Assert.IsType<ListenerRegistration>(item),
            item => Assert.IsType<CommandRegistration>(item),
            item => Assert.IsType<TimerRegistration>(item));

        var gameEvent = Assert.IsType<EventRegistration>(plugin.Registrations[0]);
        Assert.Equal(CounterStrikeSharp.API.Core.HookMode.Pre, gameEvent.Mode);
        var command = Assert.IsType<CommandRegistration>(plugin.Registrations[2]);
        Assert.Equal(1, command.MinArgs);
        Assert.Equal("@css/generic", command.Permission);
    }

    [Fact]
    public void PrepareLoadsModulesFromTheScriptDirectory()
    {
        WriteScript("helper.lua", "return { version = '2.0.0' }");
        var path = WriteScript("module_user.lua", """
            local helper = require("helper")
            cs.plugin({ name = "Module User", version = helper.version })
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);

        Assert.Equal("2.0.0", plugin.Version);
    }

    [Fact]
    public void SafeRuntimeRemovesClrAndUnsafeFileApis()
    {
        var path = WriteScript("safe.lua", """
            assert(luanet == nil)
            assert(io == nil)
            assert(dofile == nil)
            assert(loadfile == nil)
            assert(os.execute == nil)
            assert(package.loadlib == nil)
            cs.plugin({ name = "Safe" })
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        Assert.Equal("Safe", plugin.Name);
    }

    [Fact]
    public void PrepareRejectsScriptsWithoutPluginMetadata()
    {
        var path = WriteScript("invalid.lua", "return true");
        var exception = Assert.Throws<InvalidDataException>(() =>
            new LuaRuntime(NullLogger.Instance, false).Prepare(path));
        Assert.Contains("cs.plugin", exception.Message);
    }

    [Fact]
    public void LifecycleCallbacksExecute()
    {
        var path = WriteScript("lifecycle.lua", """
            local plugin = cs.plugin({ name = "Lifecycle" })
            plugin:on_load(function(hot_reload)
                loaded = hot_reload and 2 or 1
            end)
            plugin:on_unload(function(hot_reload)
                unloaded = hot_reload and 2 or 1
            end)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        plugin.InvokeLifecycle(plugin.LoadCallback, true);
        plugin.InvokeLifecycle(plugin.UnloadCallback, false);

        Assert.Equal(2, plugin.State.GetInteger("loaded"));
        Assert.Equal(1, plugin.State.GetInteger("unloaded"));
    }

    [Fact]
    public void RegistrationCanBeCancelledDuringPreparation()
    {
        var path = WriteScript("cancel.lua", """
            local plugin = cs.plugin({ name = "Cancel" })
            local id = plugin:timer(5, function() end)
            assert(plugin:cancel(id))
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        Assert.Empty(plugin.Registrations);
    }

    [Fact]
    public void CommandOptionsCanBeOmitted()
    {
        var path = WriteScript("short_command.lua", """
            local plugin = cs.plugin({ name = "Short Command" })
            plugin:command("css_short", function(player, command) end)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        Assert.Single(plugin.Registrations);
        Assert.IsType<CommandRegistration>(plugin.Registrations[0]);
    }

    [Fact]
    public void CommandListenersAndFrameSchedulesAreRegistered()
    {
        var path = WriteScript("schedules.lua", """
            local plugin = cs.plugin({ name = "Schedules" })
            plugin:command_listener("drop", { mode = "pre" }, function() return cs.handled end)
            plugin:command_listener("say", { mode = "post" }, function() return cs.continue end)
            plugin:next_frame(function() end)
            plugin:next_world_update(function() end)
            plugin:after_ticks(64, function() end)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);

        var listeners = plugin.Registrations.OfType<CommandListenerRegistration>().ToArray();
        Assert.Equal(2, listeners.Length);
        Assert.Equal(CounterStrikeSharp.API.Core.HookMode.Pre, listeners[0].Mode);
        Assert.Equal(CounterStrikeSharp.API.Core.HookMode.Post, listeners[1].Mode);
        var frames = plugin.Registrations.OfType<FrameRegistration>().ToArray();
        Assert.Equal([FrameSchedule.NextFrame, FrameSchedule.NextWorldUpdate, FrameSchedule.AfterTicks], frames.Select(item => item.Schedule));
        Assert.Equal(64, frames[2].TickDelay);
    }

    [Fact]
    public void WeaponApiIsExposedAndNativeBridgesAreHidden()
    {
        var path = WriteScript("weapons.lua", """
            cs.plugin({ name = "Weapons" })
            assert(cs.weapons.get ~= nil)
            assert(cs.weapons.find ~= nil)
            assert(__lua2cs_weapons_find == nil)
            assert(__lua2cs_weapon_set_econ == nil)
            assert(__lua2cs_player_give_weapon == nil)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(1000001)]
    public void InvalidAfterTickDelaysAreRejected(int ticks)
    {
        var path = WriteScript("bad_ticks.lua", $$"""
            local plugin = cs.plugin({ name = "Bad Ticks" })
            plugin:after_ticks({{ticks}}, function() end)
            """);
        Assert.ThrowsAny<Exception>(() => new LuaRuntime(NullLogger.Instance, false).Prepare(path));
    }

    [Fact]
    public void LuaStringsUseUtf8AcrossPluginMetadataAndCommands()
    {
        var path = WriteScript("utf8.lua", """
            local plugin = cs.plugin({
                name = "中文插件名",
                description = "中文插件说明"
            })
            plugin:command("css_utf8", {
                description = "中文命令说明",
                usage = "<中文参数>"
            }, function() end)
            lua_text = "Lua 传给 C# 的中文"
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);

        Assert.Equal("中文插件名", plugin.Name);
        Assert.Equal("中文插件说明", plugin.Description);
        Assert.Equal("Lua 传给 C# 的中文", plugin.State.GetString("lua_text"));
        var command = Assert.IsType<CommandRegistration>(Assert.Single(plugin.Registrations));
        Assert.Equal("中文命令说明", command.Description);
        Assert.Equal("<中文参数>", command.Usage);
    }

    [Fact]
    public void ExtendedBootstrapExposesHelpersAndHidesBridgeFunctions()
    {
        var path = WriteScript("extended_api.lua", """
            local plugin = cs.plugin({ name = "Extended API" })
            assert(cs.server.info ~= nil)
            assert(cs.server.maps ~= nil)
            assert(cs.players.get_userid ~= nil)
            assert(cs.players.get_steamid ~= nil)
            assert(cs.players.find ~= nil)
            assert(cs.convars.get ~= nil)
            assert(cs.capabilities.events ~= nil)
            assert(cs.capabilities.listeners ~= nil)
            assert(cs.game.rules ~= nil)
            assert(cs.storage.get ~= nil and cs.storage.set ~= nil)
            assert(cs.players.target ~= nil)
            assert(cs.entities.find ~= nil and cs.entities.get ~= nil and cs.entities.create ~= nil)
            assert(cs.game.terminate_round ~= nil)
            assert(cs.nav.areas ~= nil and cs.nav.closest ~= nil)
            assert(cs.menu.open ~= nil and cs.menu.close ~= nil)
            assert(cs.round_end.ct_win == "ct_win")
            assert(cs.voice.muted == 1 and cs.voice.listen_team == 16)
            assert(cs.team.t == 2 and cs.team.ct == 3)
            assert(cs.buttons.jump == 2)

            local vector = cs.vec3(1, 2, 3)
            assert(vector.x == 1 and vector[2] == 2 and vector.z == 3)

            local found_event = false
            for _, name in ipairs(cs.capabilities.events()) do
                if name == "player_death" then found_event = true end
            end
            assert(found_event)

            local found_listener = false
            for _, name in ipairs(cs.capabilities.listeners()) do
                if name == "OnMapStart" then found_listener = true end
            end
            assert(found_listener)

            plugin:after(1, function() end)
            plugin:every(2, function() end)

            assert(__lua2cs_server_info == nil)
            assert(__lua2cs_player_give_item == nil)
            assert(__lua2cs_player_print_html == nil)
            assert(__lua2cs_player_emit_sound == nil)
            assert(__lua2cs_entities_find == nil)
            assert(__lua2cs_entity_spawn == nil)
            assert(__lua2cs_entity_remove == nil)
            assert(__lua2cs_nav_closest == nil)
            assert(__lua2cs_menu_open == nil)
            assert(__lua2cs_player_set_model_method == nil)
            assert(__lua2cs_entity_set_gravity_method == nil)
            assert(__lua2cs_player_set_ammo == nil)
            assert(__lua2cs_player_replicate_convar == nil)
            assert(__lua2cs_player_set_voice_flags_method == nil)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);

        Assert.Collection(
            plugin.Registrations,
            item => Assert.False(Assert.IsType<TimerRegistration>(item).Repeat),
            item => Assert.True(Assert.IsType<TimerRegistration>(item).Repeat));
    }

    [Fact]
    public void StoragePersistsPrimitiveValuesAcrossLuaVirtualMachines()
    {
        var path = WriteScript("storage.lua", """
            local plugin = cs.plugin({ name = "Storage" })
            counter = cs.storage.get("counter", 0) + 1
            cs.storage.set("counter", counter)
            cs.storage.set("enabled", true)
            cs.storage.set("message", "中文数据")
            cs.storage.set("ratio", 1.25)
            cs.storage.set("temporary", "待删除")
            assert(cs.storage.set("temporary", nil))
            assert(not cs.storage.has("temporary"))
            assert(cs.storage.has("counter"))
            local all = cs.storage.all()
            assert(all.counter == counter)
            assert(all.enabled == true)
            assert(all.message == "中文数据")
            assert(all.ratio == 1.25)
            """);

        using (var first = new LuaRuntime(NullLogger.Instance, false).Prepare(path))
        {
            Assert.Equal(1, first.State.GetInteger("counter"));
        }

        using (var second = new LuaRuntime(NullLogger.Instance, false).Prepare(path))
        {
            Assert.Equal(2, second.State.GetInteger("counter"));
        }

        var dataPath = Path.Combine(_directory, ".lua2cs-data", "storage.json");
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dataPath));
        Assert.Equal(2, document.RootElement.GetProperty("counter").GetInt64());
        Assert.True(document.RootElement.GetProperty("enabled").GetBoolean());
        Assert.Equal("中文数据", document.RootElement.GetProperty("message").GetString());
        Assert.Equal(1.25, document.RootElement.GetProperty("ratio").GetDouble());
        Assert.False(document.RootElement.TryGetProperty("temporary", out _));
    }

    [Fact]
    public void StorageRejectsNonFiniteNumbersAndCanBeCleared()
    {
        var invalidPath = WriteScript("invalid_number.lua", """
            cs.plugin({ name = "Invalid Number" })
            cs.storage.set("bad", 0 / 0)
            """);
        Assert.ThrowsAny<Exception>(() => new LuaRuntime(NullLogger.Instance, false).Prepare(invalidPath));

        var clearPath = WriteScript("clear.lua", """
            cs.plugin({ name = "Clear" })
            cs.storage.set("one", 1)
            cs.storage.clear()
            assert(next(cs.storage.all()) == nil)
            assert(cs.storage.get("one") == nil)
            assert(cs.storage.delete("missing") == false)
            """);
        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(clearPath);
    }

    [Fact]
    public void StorageRecoversFromDamagedJson()
    {
        var dataDirectory = Path.Combine(_directory, ".lua2cs-data");
        Directory.CreateDirectory(dataDirectory);
        var dataPath = Path.Combine(dataDirectory, "recover.json");
        File.WriteAllText(dataPath, "{ damaged json");
        var path = WriteScript("recover.lua", """
            cs.plugin({ name = "Recover" })
            assert(cs.storage.get("missing", "fallback") == "fallback")
            cs.storage.set("recovered", true)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        using var document = System.Text.Json.JsonDocument.Parse(File.ReadAllText(dataPath));
        Assert.True(document.RootElement.GetProperty("recovered").GetBoolean());
    }

    [Theory]
    [InlineData("none", CounterStrikeSharp.API.Modules.Utils.CsTeam.None)]
    [InlineData("spec", CounterStrikeSharp.API.Modules.Utils.CsTeam.Spectator)]
    [InlineData("t", CounterStrikeSharp.API.Modules.Utils.CsTeam.Terrorist)]
    [InlineData("counter_terrorist", CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist)]
    [InlineData(3L, CounterStrikeSharp.API.Modules.Utils.CsTeam.CounterTerrorist)]
    public void TeamNamesAreParsed(object source, CounterStrikeSharp.API.Modules.Utils.CsTeam expected) =>
        Assert.Equal(expected, LuaApi.ParseTeam(source));

    [Theory]
    [InlineData(-1L)]
    [InlineData(4L)]
    [InlineData("unknown")]
    public void InvalidTeamNamesAreRejected(object source) =>
        Assert.Throws<ArgumentException>(() => LuaApi.ParseTeam(source));

    [Theory]
    [InlineData("ct_win", CounterStrikeSharp.API.Modules.Entities.Constants.RoundEndReason.CTsWin)]
    [InlineData("T", CounterStrikeSharp.API.Modules.Entities.Constants.RoundEndReason.TerroristsWin)]
    [InlineData("bomb_defused", CounterStrikeSharp.API.Modules.Entities.Constants.RoundEndReason.BombDefused)]
    [InlineData("round_draw", CounterStrikeSharp.API.Modules.Entities.Constants.RoundEndReason.RoundDraw)]
    public void RoundEndReasonsAreParsed(string source, CounterStrikeSharp.API.Modules.Entities.Constants.RoundEndReason expected) =>
        Assert.Equal(expected, LuaApi.ParseRoundEndReason(source));

    [Theory]
    [InlineData("close", CounterStrikeSharp.API.Modules.Menu.PostSelectAction.Close)]
    [InlineData("reset", CounterStrikeSharp.API.Modules.Menu.PostSelectAction.Reset)]
    [InlineData("keep", CounterStrikeSharp.API.Modules.Menu.PostSelectAction.Nothing)]
    public void MenuPostSelectActionsAreParsed(string source, CounterStrikeSharp.API.Modules.Menu.PostSelectAction expected) =>
        Assert.Equal(expected, LuaApi.ParsePostSelectAction(source));

    [Fact]
    public void InvalidRoundAndMenuValuesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => LuaApi.ParseRoundEndReason("invalid"));
        Assert.Throws<ArgumentException>(() => LuaApi.ParsePostSelectAction("invalid"));
    }

    [Theory]
    [InlineData("cl_language")]
    [InlineData("  bot_difficulty  ")]
    public void ConVarNamesAreNormalized(string source) =>
        Assert.False(string.IsNullOrWhiteSpace(LuaApi.ValidateConVarName(source)));

    [Theory]
    [InlineData("")]
    [InlineData("bad name")]
    [InlineData("bad\nname")]
    public void InvalidConVarNamesAreRejected(string source) =>
        Assert.Throws<ArgumentException>(() => LuaApi.ValidateConVarName(source));

    [Theory]
    [InlineData("bad\nvalue")]
    [InlineData("bad\0value")]
    public void InvalidConVarValuesAreRejected(string source) =>
        Assert.Throws<ArgumentException>(() => LuaApi.ValidateConVarValue(source));

    [Fact]
    public void PlayerMutationArgumentsAreValidatedBeforeNativeLookup()
    {
        var path = WriteScript("mutation_validation.lua", "cs.plugin({ name = 'Mutation Validation' })");
        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);

        Assert.Throws<ArgumentOutOfRangeException>(() => plugin.Api.PlayerSetAmmo(-1, -2, 90));
        Assert.Throws<ArgumentOutOfRangeException>(() => plugin.Api.PlayerSetAmmo(-1, 30, 10001));
        Assert.Throws<ArgumentException>(() => plugin.Api.PlayerSetAmmo(-1, -1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => plugin.Api.PlayerSetScore(-1, -1));
        Assert.Throws<ArgumentOutOfRangeException>(() => plugin.Api.PlayerSetVoiceFlags(-1, 32));

        Assert.False(plugin.Api.PlayerSetAmmo(-1, 30, 90));
        Assert.False(plugin.Api.PlayerSetScore(-1, 10));
        Assert.False(plugin.Api.PlayerSetVoiceFlags(-1, 0));
        Assert.False(plugin.Api.PlayerReplicateConVar(-1, "cl_language", "schinese"));
    }

    [Fact]
    public void FailedDynamicRegistrationIsRemovedAgain()
    {
        var path = WriteScript("dynamic_failure.lua", """
            local plugin = cs.plugin({ name = "Dynamic Failure" })
            plugin:on_load(function()
                plugin:timer(1, function() end)
            end)
            """);

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        plugin.Activate(
            definition => new RegistrationHandle(definition.Id, () => { }),
            _ => throw new InvalidOperationException("activation rejected"));

        Assert.ThrowsAny<Exception>(() => plugin.InvokeLifecycle(plugin.LoadCallback, false));
        Assert.Empty(plugin.Registrations);
    }

    [Fact]
    public void DeactivationContinuesAfterOneHandleFails()
    {
        var path = WriteScript("cleanup.lua", """
            local plugin = cs.plugin({ name = "Cleanup" })
            plugin:timer(1, function() end)
            plugin:timer(2, function() end)
            """);
        var disposed = 0;

        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        plugin.Activate(definition => new RegistrationHandle(definition.Id, () =>
        {
            if (definition.Id == 2) throw new InvalidOperationException("cleanup failed");
            disposed++;
        }));

        plugin.Deactivate();
        Assert.Equal(1, disposed);
        Assert.False(plugin.IsActive);
    }

    [Theory]
    [InlineData("hello.lua")]
    [InlineData("qwq.lua")]
    [InlineData("awa.lua")]
    [InlineData("round_timer.lua")]
    [InlineData("admin_tools.lua")]
    [InlineData("spawn_protection.lua")]
    [InlineData("round_loadout.lua")]
    [InlineData("kill_reward.lua")]
    [InlineData("player_hud.lua")]
    [InlineData("join_messages.lua")]
    [InlineData("map_tools.lua")]
    [InlineData("checkpoints.lua")]
    [InlineData("player_info.lua")]
    [InlineData("module_demo.lua")]
    [InlineData("persistent_kills.lua")]
    [InlineData("target_tools.lua")]
    [InlineData("game_status.lua")]
    [InlineData("entity_tools.lua")]
    [InlineData("menu_demo.lua")]
    [InlineData("movement_fun.lua")]
    [InlineData("nav_tools.lua")]
    [InlineData("round_control.lua")]
    [InlineData("model_tools.lua")]
    [InlineData("ammo_refill.lua")]
    [InlineData("weapon_inspector.lua")]
    [InlineData("scoreboard_tools.lua")]
    [InlineData("voice_tools.lua")]
    [InlineData("client_convar.lua")]
    [InlineData("bot_convar.lua")]
    [InlineData("damage_report.lua")]
    [InlineData("chat_cooldown.lua")]
    [InlineData("random_loadout.lua")]
    [InlineData("welcome_menu.lua")]
    [InlineData("bomb_announcer.lua")]
    [InlineData("aim_inspector.lua")]
    [InlineData("team_summary.lua")]
    [InlineData("tpa.lua")]
    [InlineData("infinite_weapon_drop.lua")]
    [InlineData("native_command_listener.lua")]
    [InlineData("frame_scheduler.lua")]
    [InlineData("weapon_factory.lua")]
    [InlineData("killstreak_arena.lua")]
    [InlineData("weapon_shop.lua")]
    [InlineData("map_vote.lua")]
    [InlineData("gun_game.lua")]
    [InlineData("parkour_time_trial.lua")]
    public void ShippedExamplesLoad(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "examples", fileName);
        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        Assert.False(string.IsNullOrWhiteSpace(plugin.Name));
    }

    [Fact]
    public void ShippedTpaCommandsHaveExpectedAccessAndArguments()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "examples", "tpa.lua");
        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        var commands = plugin.Registrations.OfType<CommandRegistration>().ToDictionary(item => item.Name);

        Assert.Equal("0.3.5", plugin.Version);
        Assert.Equal(13, plugin.Registrations.Count);
        Assert.Single(plugin.Registrations.OfType<ListenerRegistration>());
        Assert.Equal([
            "css_tpalist",
            "css_tpa",
            "css_tpaid",
            "css_tpaslot",
            "css_tpaname",
            "css_tpahere",
            "css_tpahereid",
            "css_tpahereslot",
            "css_tpaherename",
            "css_tpaccept",
            "css_tpdeny",
            "css_tpcancel"
        ], commands.Keys);
        Assert.All(commands.Values, command => Assert.True(string.IsNullOrEmpty(command.Permission)));
        Assert.All(commands.Values, command => Assert.False(command.AllowConsole));
        Assert.Equal(0, commands["css_tpalist"].MinArgs);
        Assert.All(commands
            .Where(item => item.Key is not "css_tpalist" and not "css_tpaccept" and not "css_tpdeny" and not "css_tpcancel"),
            item => Assert.Equal(1, item.Value.MinArgs));
        Assert.Equal(0, commands["css_tpaccept"].MinArgs);
        Assert.Equal(0, commands["css_tpdeny"].MinArgs);
        Assert.Equal(0, commands["css_tpcancel"].MinArgs);
    }

    [Fact]
    public void InfiniteDropUsesNativeDropAndDoesNotCreateWeaponEntitiesDirectly()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "examples", "infinite_weapon_drop.lua");
        using var plugin = new LuaRuntime(NullLogger.Instance, false).Prepare(path);
        var listener = Assert.IsType<CommandListenerRegistration>(
            Assert.Single(plugin.Registrations.OfType<CommandListenerRegistration>()));

        Assert.Equal("drop", listener.Name);
        Assert.Equal(CounterStrikeSharp.API.Core.HookMode.Pre, listener.Mode);
        Assert.DoesNotContain("cs.weapons.spawn", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("DmgArmor", "dmg_armor")]
    [InlineData("Userid", "userid")]
    [InlineData("WeaponFauxitemid", "weapon_fauxitemid")]
    public void EventFieldNamesBecomeSnakeCase(string source, string expected) =>
        Assert.Equal(expected, LuaApi.ToSnakeCase(source));

    [Theory]
    [InlineData("hello", "hello")]
    [InlineData("hello.lua", "hello")]
    [InlineData("  fun_plugin.lua  ", "fun_plugin")]
    public void PluginKeysAreNormalized(string source, string expected) =>
        Assert.Equal(expected, LuaPluginManager.NormalizeKey(source));

    [Theory]
    [InlineData("gameplay.lua", true)]
    [InlineData("GAMEPLAY.LUA", true)]
    [InlineData("_shared.lua", false)]
    [InlineData("readme.txt", false)]
    public void StandaloneScriptNamesExcludeModuleFiles(string path, bool expected) =>
        Assert.Equal(expected, LuaPluginManager.IsStandaloneScript(path));

    [Fact]
    public void ScriptPathResolutionSupportsUppercaseLuaExtensions()
    {
        var path = WriteScript("UPPER.LUA", "cs.plugin({ name = 'Upper' })");
        var resolved = LuaPluginManager.ResolveScriptPath(_directory, "upper");
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        Assert.True(string.Equals(path, resolved, comparison));
        Assert.True(File.Exists(resolved));
    }

    [Fact]
    public void RootUnderscoreAndNestedFilesTriggerFullReload()
    {
        var root = Path.Combine(Path.GetTempPath(), "lua2cs-scripts");

        Assert.False(LuaPluginManager.IsModulePath(Path.Combine(root, "gameplay.lua"), root));
        Assert.True(LuaPluginManager.IsModulePath(Path.Combine(root, "_shared.lua"), root));
        Assert.True(LuaPluginManager.IsModulePath(Path.Combine(root, "modules", "shared.lua"), root));
    }

    private string WriteScript(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }
}
