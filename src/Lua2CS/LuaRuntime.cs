using System.Reflection;
using System.Text;
using Microsoft.Extensions.Logging;
using NLua;

namespace Lua2CS;

public sealed class LuaRuntime(ILogger logger, bool allowUnsafeLibraries, int slowCallbackMilliseconds = 25)
{
    private const string Bootstrap = """
        local create_plugin = __lua2cs_create_plugin
        local register_event = __lua2cs_register_event
        local register_listener = __lua2cs_register_listener
        local register_command = __lua2cs_register_command
        local register_command_listener = __lua2cs_register_command_listener
        local register_timer = __lua2cs_register_timer
        local register_frame = __lua2cs_register_frame
        local register_load = __lua2cs_register_load
        local register_unload = __lua2cs_register_unload
        local cancel_registration = __lua2cs_cancel_registration
        local log_debug = __lua2cs_log_debug
        local log_info = __lua2cs_log_info
        local log_warn = __lua2cs_log_warn
        local log_error = __lua2cs_log_error
        local server_chat = __lua2cs_server_chat
        local server_console = __lua2cs_server_console
        local server_command = __lua2cs_server_command
        local server_info = __lua2cs_server_info
        local server_maps = __lua2cs_server_maps
        local server_is_map_valid = __lua2cs_server_is_map_valid
        local server_precache_model = __lua2cs_server_precache_model
        local convar_get = __lua2cs_convar_get
        local convar_set = __lua2cs_convar_set
        local capability_events = __lua2cs_capability_events
        local capability_listeners = __lua2cs_capability_listeners
        local game_rules = __lua2cs_game_rules
        local game_terminate_round = __lua2cs_game_terminate_round
        local nav_areas = __lua2cs_nav_areas
        local nav_closest = __lua2cs_nav_closest
        local menu_open = __lua2cs_menu_open
        local menu_close = __lua2cs_menu_close
        local storage_get = __lua2cs_storage_get
        local storage_has = __lua2cs_storage_has
        local storage_set_string = __lua2cs_storage_set_string
        local storage_set_integer = __lua2cs_storage_set_integer
        local storage_set_number = __lua2cs_storage_set_number
        local storage_set_boolean = __lua2cs_storage_set_boolean
        local storage_delete = __lua2cs_storage_delete
        local storage_clear = __lua2cs_storage_clear
        local storage_all = __lua2cs_storage_all
        local entities_find = __lua2cs_entities_find
        local entities_get = __lua2cs_entities_get
        local entities_create = __lua2cs_entities_create
        local weapons_get = __lua2cs_weapons_get
        local weapons_find = __lua2cs_weapons_find
        local entity_refresh = __lua2cs_entity_refresh
        local entity_spawn = __lua2cs_entity_spawn
        local entity_input = __lua2cs_entity_input
        local entity_remove = __lua2cs_entity_remove
        local entity_teleport = __lua2cs_entity_teleport
        local entity_set_health = __lua2cs_entity_set_health
        local entity_set_max_health = __lua2cs_entity_set_max_health
        local entity_set_gravity = __lua2cs_entity_set_gravity
        local entity_set_model = __lua2cs_entity_set_model
        local entity_set_render_color = __lua2cs_entity_set_render_color
        local entity_emit_sound = __lua2cs_entity_emit_sound
        local weapon_set_ammo = __lua2cs_weapon_set_ammo
        local weapon_set_econ = __lua2cs_weapon_set_econ
        local players_all = __lua2cs_players_all
        local players_get = __lua2cs_players_get
        local players_get_userid = __lua2cs_players_get_userid
        local players_get_steamid = __lua2cs_players_get_steamid
        local players_find = __lua2cs_players_find
        local players_humans = __lua2cs_players_humans
        local players_bots = __lua2cs_players_bots
        local players_count = __lua2cs_players_count
        local players_target = __lua2cs_players_target
        local player_is_current = __lua2cs_player_is_current
        local player_chat = __lua2cs_player_chat
        local player_console = __lua2cs_player_console
        local player_center = __lua2cs_player_center
        local player_alert = __lua2cs_player_alert
        local player_html = __lua2cs_player_html
        local player_refresh = __lua2cs_player_refresh
        local player_permission = __lua2cs_player_permission
        local player_can_target = __lua2cs_player_can_target
        local player_convar = __lua2cs_player_convar
        local player_execute = __lua2cs_player_execute
        local player_give_item = __lua2cs_player_give_item
        local player_give_weapon = __lua2cs_player_give_weapon
        local player_remove_item = __lua2cs_player_remove_item
        local player_remove_weapons = __lua2cs_player_remove_weapons
        local player_drop_weapon = __lua2cs_player_drop_weapon
        local player_respawn = __lua2cs_player_respawn
        local player_kill = __lua2cs_player_kill
        local player_kick = __lua2cs_player_kick
        local player_change_team = __lua2cs_player_change_team
        local player_teleport = __lua2cs_player_teleport
        local player_set_health = __lua2cs_player_set_health
        local player_set_armor = __lua2cs_player_set_armor
        local player_set_money = __lua2cs_player_set_money
        local player_aim_target = __lua2cs_player_aim_target
        local player_set_max_health = __lua2cs_player_set_max_health
        local player_set_gravity = __lua2cs_player_set_gravity
        local player_set_velocity_modifier = __lua2cs_player_set_velocity_modifier
        local player_set_model = __lua2cs_player_set_model
        local player_set_render_color = __lua2cs_player_set_render_color
        local player_set_ammo = __lua2cs_player_set_ammo
        local player_set_score = __lua2cs_player_set_score
        local player_set_round_score = __lua2cs_player_set_round_score
        local player_set_mvps = __lua2cs_player_set_mvps
        local player_set_voice_flags = __lua2cs_player_set_voice_flags
        local player_replicate_convar = __lua2cs_player_replicate_convar
        local player_set_fake_convar = __lua2cs_player_set_fake_convar
        local player_emit_sound = __lua2cs_player_emit_sound
        local command_reply = __lua2cs_command_reply

        cs = {
            continue = "continue",
            changed = "changed",
            handled = "handled",
            stop = "stop",
            log = {
                debug = log_debug,
                info = log_info,
                warn = log_warn,
                error = log_error
            },
            server = {
                print_chat_all = server_chat,
                print_console = server_console,
                execute = server_command,
                info = server_info,
                maps = server_maps,
                is_map_valid = server_is_map_valid,
                precache_model = server_precache_model
            },
            players = {
                all = players_all,
                get = players_get,
                get_userid = players_get_userid,
                get_steamid = players_get_steamid,
                find = players_find,
                target = function(pattern, caller)
                    return players_target(pattern, caller)
                end,
                humans = players_humans,
                bots = players_bots,
                count = players_count
            },
            convars = {
                get = convar_get,
                set = function(name, value)
                    return convar_set(name, tostring(value))
                end
            },
            capabilities = {
                events = capability_events,
                listeners = capability_listeners
            },
            game = {
                rules = game_rules,
                terminate_round = game_terminate_round
            },
            nav = {
                areas = function(limit)
                    return nav_areas(limit or 1024)
                end,
                closest = function(position, maximum_distance)
                    return nav_closest(position, maximum_distance or -1)
                end
            },
            menu = {
                open = menu_open,
                close = menu_close
            },
            storage = {},
            entities = {
                find = function(designer_name, limit)
                    return entities_find(designer_name, limit or 128)
                end,
                get = entities_get,
                create = function(designer_name, spawn)
                    if spawn == nil then spawn = true end
                    return entities_create(designer_name, spawn)
                end
            },
            weapons = {
                get = weapons_get,
                find = function(designer_name, limit)
                    return weapons_find(designer_name, limit or 128)
                end
            },
            team = {
                none = 0,
                spectator = 1,
                terrorist = 2,
                t = 2,
                counter_terrorist = 3,
                ct = 3
            },
            round_end = {
                target_bombed = "target_bombed",
                bomb_defused = "bomb_defused",
                ct_win = "ct_win",
                terrorist_win = "terrorist_win",
                draw = "draw",
                all_hostages_rescued = "all_hostages_rescued",
                target_saved = "target_saved",
                game_commencing = "game_commencing"
            },
            voice = {
                normal = 0,
                muted = 1 << 0,
                all = 1 << 1,
                listen_all = 1 << 2,
                team = 1 << 3,
                listen_team = 1 << 4
            },
            buttons = {
                attack = 1 << 0,
                jump = 1 << 1,
                duck = 1 << 2,
                forward = 1 << 3,
                back = 1 << 4,
                use = 1 << 5,
                left = 1 << 7,
                right = 1 << 8,
                move_left = 1 << 9,
                move_right = 1 << 10,
                attack2 = 1 << 11,
                reload = 1 << 13,
                speed = 1 << 16,
                walk = 1 << 17,
                zoom = 1 << 18,
                scoreboard = 1 << 33,
                inspect = 1 << 35
            },
            colors = {
                default = "\x01",
                white = "\x01",
                dark_red = "\x02",
                light_purple = "\x03",
                green = "\x04",
                olive = "\x05",
                lime = "\x06",
                red = "\x07",
                grey = "\x08",
                yellow = "\x09",
                silver = "\x0A",
                blue = "\x0B",
                dark_blue = "\x0C",
                purple = "\x0E",
                light_red = "\x0F",
                gold = "\x10",
                orange = "\x10"
            }
        }

        function cs.vec3(x, y, z)
            return { x = x, y = y, z = z, [1] = x, [2] = y, [3] = z }
        end

        cs.angle = cs.vec3

        function cs.storage.get(key, default_value)
            local value = storage_get(key)
            if value == nil then return default_value end
            return value
        end

        cs.storage.has = storage_has
        cs.storage.delete = storage_delete
        cs.storage.clear = storage_clear
        cs.storage.all = storage_all

        function cs.storage.set(key, value)
            local value_type = type(value)
            if value_type == "nil" then return storage_delete(key) end
            if value_type == "string" then return storage_set_string(key, value) end
            if value_type == "boolean" then return storage_set_boolean(key, value) end
            if value_type == "number" then
                if math.type(value) == "integer" then return storage_set_integer(key, value) end
                return storage_set_number(key, value)
            end
            error("持久化存储只支持 nil、字符串、布尔值和数值", 2)
        end

        function cs.plugin(spec)
            create_plugin(spec)
            local plugin = {}

            function plugin:on(name, callback, options)
                return register_event(name, callback, options or {})
            end

            function plugin:listen(name, callback)
                return register_listener(name, callback)
            end

            function plugin:command(name, options, callback)
                if type(options) == "function" then
                    callback = options
                    options = {}
                end
                return register_command(name, options or {}, callback)
            end

            function plugin:command_listener(name, options, callback)
                if type(options) == "function" then
                    callback = options
                    options = {}
                end
                return register_command_listener(name, options or {}, callback)
            end

            function plugin:timer(interval, callback, options)
                return register_timer(interval, callback, options or {})
            end

            function plugin:after(delay, callback, options)
                options = options or {}
                options.repeating = false
                return register_timer(delay, callback, options)
            end

            function plugin:every(interval, callback, options)
                options = options or {}
                options.repeating = true
                return register_timer(interval, callback, options)
            end

            function plugin:next_frame(callback)
                return register_frame("next_frame", 0, callback)
            end

            function plugin:next_world_update(callback)
                return register_frame("next_world_update", 0, callback)
            end

            function plugin:after_ticks(ticks, callback)
                return register_frame("after_ticks", ticks, callback)
            end

            function plugin:on_load(callback)
                register_load(callback)
            end

            function plugin:on_unload(callback)
                register_unload(callback)
            end

            function plugin:cancel(registration_id)
                return cancel_registration(registration_id)
            end

            return plugin
        end

        local function current_player_slot(player)
            if player == nil or not player_is_current(player.slot, player.user_id, player.steam_id) then
                return nil
            end
            return player.slot
        end

        function __lua2cs_player_print_chat(self, message)
            local slot = current_player_slot(self)
            return slot ~= nil and player_chat(slot, message) or false
        end

        function __lua2cs_player_print_console(self, message)
            local slot = current_player_slot(self)
            return slot ~= nil and player_console(slot, message) or false
        end

        function __lua2cs_player_print_center(self, message)
            local slot = current_player_slot(self)
            return slot ~= nil and player_center(slot, message) or false
        end

        function __lua2cs_player_print_alert(self, message)
            local slot = current_player_slot(self)
            return slot ~= nil and player_alert(slot, message) or false
        end

        function __lua2cs_player_print_html(self, message, duration)
            local slot = current_player_slot(self)
            return slot ~= nil and player_html(slot, message, duration or 5) or false
        end

        function __lua2cs_player_refresh_method(self)
            local slot = current_player_slot(self)
            return slot ~= nil and player_refresh(slot) or nil
        end

        function __lua2cs_player_has_permission_method(self, permission)
            local slot = current_player_slot(self)
            return slot ~= nil and player_permission(slot, permission) or false
        end

        function __lua2cs_player_can_target_method(self, target)
            local slot = current_player_slot(self)
            local target_slot = current_player_slot(target)
            return slot ~= nil and target_slot ~= nil and player_can_target(slot, target_slot) or false
        end

        function __lua2cs_player_get_convar_method(self, name)
            local slot = current_player_slot(self)
            return slot ~= nil and player_convar(slot, name) or nil
        end

        function __lua2cs_player_execute_method(self, command)
            local slot = current_player_slot(self)
            return slot ~= nil and player_execute(slot, command, false) or false
        end

        function __lua2cs_player_execute_server_method(self, command)
            local slot = current_player_slot(self)
            return slot ~= nil and player_execute(slot, command, true) or false
        end

        function __lua2cs_player_give_item_method(self, designer_name)
            local slot = current_player_slot(self)
            return slot ~= nil and player_give_item(slot, designer_name) or false
        end

        function __lua2cs_player_give_weapon_method(self, designer_name, options)
            local slot = current_player_slot(self)
            return slot ~= nil and player_give_weapon(slot, designer_name, options) or nil
        end

        function __lua2cs_player_remove_item_method(self, designer_name)
            local slot = current_player_slot(self)
            return slot ~= nil and player_remove_item(slot, designer_name) or false
        end

        function __lua2cs_player_remove_weapons_method(self)
            local slot = current_player_slot(self)
            return slot ~= nil and player_remove_weapons(slot) or false
        end

        function __lua2cs_player_drop_weapon_method(self)
            local slot = current_player_slot(self)
            return slot ~= nil and player_drop_weapon(slot) or false
        end

        function __lua2cs_player_respawn_method(self)
            local slot = current_player_slot(self)
            return slot ~= nil and player_respawn(slot) or false
        end

        function __lua2cs_player_kill_method(self, explode, force)
            local slot = current_player_slot(self)
            return slot ~= nil and player_kill(slot, explode or false, force or false) or false
        end

        function __lua2cs_player_kick_method(self)
            local slot = current_player_slot(self)
            return slot ~= nil and player_kick(slot) or false
        end

        function __lua2cs_player_change_team_method(self, team)
            local slot = current_player_slot(self)
            return slot ~= nil and player_change_team(slot, team, false) or false
        end

        function __lua2cs_player_switch_team_method(self, team)
            local slot = current_player_slot(self)
            return slot ~= nil and player_change_team(slot, team, true) or false
        end

        function __lua2cs_player_teleport_method(self, position, angles, velocity)
            local slot = current_player_slot(self)
            return slot ~= nil and player_teleport(slot, position, angles, velocity) or false
        end

        function __lua2cs_player_set_health_method(self, health)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_health(slot, health) or false
        end

        function __lua2cs_player_set_armor_method(self, armor)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_armor(slot, armor) or false
        end

        function __lua2cs_player_set_money_method(self, money)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_money(slot, money) or false
        end

        function __lua2cs_player_aim_target_method(self)
            local slot = current_player_slot(self)
            return slot ~= nil and player_aim_target(slot) or nil
        end

        function __lua2cs_player_set_max_health_method(self, health)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_max_health(slot, health) or false
        end

        function __lua2cs_player_set_gravity_method(self, scale)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_gravity(slot, scale) or false
        end

        function __lua2cs_player_set_velocity_modifier_method(self, modifier)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_velocity_modifier(slot, modifier) or false
        end

        function __lua2cs_player_set_model_method(self, model_name, precache)
            local slot = current_player_slot(self)
            if slot == nil then return false end
            if precache == nil then precache = true end
            return player_set_model(slot, model_name, precache)
        end

        function __lua2cs_player_set_render_color_method(self, red, green, blue, alpha)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_render_color(slot, red, green, blue, alpha or 255) or false
        end

        function __lua2cs_player_close_menu_method(self)
            if current_player_slot(self) == nil then return false end
            return menu_close(self)
        end

        function __lua2cs_player_set_ammo_method(self, clip, reserve)
            local slot = current_player_slot(self)
            if slot == nil then return false end
            if clip == nil and reserve == nil then error("弹匣和备弹不能同时省略", 2) end
            return player_set_ammo(slot, clip or -1, reserve or -1)
        end

        function __lua2cs_player_set_score_method(self, score)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_score(slot, score) or false
        end

        function __lua2cs_player_set_round_score_method(self, score)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_round_score(slot, score) or false
        end

        function __lua2cs_player_set_mvps_method(self, mvps)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_mvps(slot, mvps) or false
        end

        function __lua2cs_player_set_voice_flags_method(self, flags)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_voice_flags(slot, flags) or false
        end

        function __lua2cs_player_replicate_convar_method(self, name, value)
            local slot = current_player_slot(self)
            return slot ~= nil and player_replicate_convar(slot, name, tostring(value)) or false
        end

        function __lua2cs_player_set_fake_convar_method(self, name, value)
            local slot = current_player_slot(self)
            return slot ~= nil and player_set_fake_convar(slot, name, tostring(value)) or false
        end

        function __lua2cs_player_emit_sound_method(self, sound_event_name, volume, pitch)
            local slot = current_player_slot(self)
            if slot == nil then return 0 end
            return player_emit_sound(slot, sound_event_name, volume or 1, pitch or 0)
        end

        function __lua2cs_entity_refresh_method(self)
            return entity_refresh(self.handle)
        end

        function __lua2cs_entity_spawn_method(self)
            return entity_spawn(self.handle)
        end

        function __lua2cs_entity_input_method(self, input_name, value, delay)
            return entity_input(self.handle, input_name, value or "", delay or 0)
        end

        function __lua2cs_entity_remove_method(self, delay)
            return entity_remove(self.handle, delay or 0)
        end

        function __lua2cs_entity_teleport_method(self, position, angles, velocity)
            return entity_teleport(self.handle, position, angles, velocity)
        end

        function __lua2cs_entity_set_health_method(self, health)
            return entity_set_health(self.handle, health)
        end

        function __lua2cs_entity_set_max_health_method(self, health)
            return entity_set_max_health(self.handle, health)
        end

        function __lua2cs_entity_set_gravity_method(self, scale)
            return entity_set_gravity(self.handle, scale)
        end

        function __lua2cs_entity_set_model_method(self, model_name, precache)
            if precache == nil then precache = true end
            return entity_set_model(self.handle, model_name, precache)
        end

        function __lua2cs_entity_set_render_color_method(self, red, green, blue, alpha)
            return entity_set_render_color(self.handle, red, green, blue, alpha or 255)
        end

        function __lua2cs_entity_emit_sound_method(self, sound_event_name, volume, pitch)
            return entity_emit_sound(self.handle, sound_event_name, volume or 1, pitch or 0)
        end

        function __lua2cs_weapon_refresh_method(self)
            return weapons_get(self.handle)
        end

        function __lua2cs_weapon_set_ammo_method(self, clip, reserve, clip_secondary, reserve_secondary)
            if clip == nil and reserve == nil and clip_secondary == nil and reserve_secondary == nil then
                error("至少需要提供一个弹药数量", 2)
            end
            return weapon_set_ammo(
                self.handle,
                clip or -1,
                reserve or -1,
                clip_secondary or -1,
                reserve_secondary or -1
            )
        end

        function __lua2cs_weapon_set_econ_method(self, options)
            return weapon_set_econ(self.handle, options or {})
        end

        function __lua2cs_command_reply_method(self, message)
            return command_reply(self.__context_id, message)
        end

        package.path = __lua2cs_module_path .. "/?.lua;" .. __lua2cs_module_path .. "/?/init.lua"
        package.cpath = ""
        luanet = nil
        __lua2cs_create_plugin = nil
        __lua2cs_register_event = nil
        __lua2cs_register_listener = nil
        __lua2cs_register_command = nil
        __lua2cs_register_command_listener = nil
        __lua2cs_register_timer = nil
        __lua2cs_register_frame = nil
        __lua2cs_register_load = nil
        __lua2cs_register_unload = nil
        __lua2cs_cancel_registration = nil
        __lua2cs_log_debug = nil
        __lua2cs_log_info = nil
        __lua2cs_log_warn = nil
        __lua2cs_log_error = nil
        __lua2cs_server_chat = nil
        __lua2cs_server_console = nil
        __lua2cs_server_command = nil
        __lua2cs_server_info = nil
        __lua2cs_server_maps = nil
        __lua2cs_server_is_map_valid = nil
        __lua2cs_server_precache_model = nil
        __lua2cs_convar_get = nil
        __lua2cs_convar_set = nil
        __lua2cs_capability_events = nil
        __lua2cs_capability_listeners = nil
        __lua2cs_game_rules = nil
        __lua2cs_game_terminate_round = nil
        __lua2cs_nav_areas = nil
        __lua2cs_nav_closest = nil
        __lua2cs_menu_open = nil
        __lua2cs_menu_close = nil
        __lua2cs_storage_get = nil
        __lua2cs_storage_has = nil
        __lua2cs_storage_set_string = nil
        __lua2cs_storage_set_integer = nil
        __lua2cs_storage_set_number = nil
        __lua2cs_storage_set_boolean = nil
        __lua2cs_storage_delete = nil
        __lua2cs_storage_clear = nil
        __lua2cs_storage_all = nil
        __lua2cs_entities_find = nil
        __lua2cs_entities_get = nil
        __lua2cs_entities_create = nil
        __lua2cs_weapons_get = nil
        __lua2cs_weapons_find = nil
        __lua2cs_entity_refresh = nil
        __lua2cs_entity_spawn = nil
        __lua2cs_entity_input = nil
        __lua2cs_entity_remove = nil
        __lua2cs_entity_teleport = nil
        __lua2cs_entity_set_health = nil
        __lua2cs_entity_set_max_health = nil
        __lua2cs_entity_set_gravity = nil
        __lua2cs_entity_set_model = nil
        __lua2cs_entity_set_render_color = nil
        __lua2cs_entity_emit_sound = nil
        __lua2cs_weapon_set_ammo = nil
        __lua2cs_weapon_set_econ = nil
        __lua2cs_players_all = nil
        __lua2cs_players_get = nil
        __lua2cs_players_get_userid = nil
        __lua2cs_players_get_steamid = nil
        __lua2cs_players_find = nil
        __lua2cs_players_humans = nil
        __lua2cs_players_bots = nil
        __lua2cs_players_count = nil
        __lua2cs_players_target = nil
        __lua2cs_player_is_current = nil
        __lua2cs_player_chat = nil
        __lua2cs_player_console = nil
        __lua2cs_player_center = nil
        __lua2cs_player_alert = nil
        __lua2cs_player_html = nil
        __lua2cs_player_refresh = nil
        __lua2cs_player_permission = nil
        __lua2cs_player_can_target = nil
        __lua2cs_player_convar = nil
        __lua2cs_player_execute = nil
        __lua2cs_player_give_item = nil
        __lua2cs_player_give_weapon = nil
        __lua2cs_player_remove_item = nil
        __lua2cs_player_remove_weapons = nil
        __lua2cs_player_drop_weapon = nil
        __lua2cs_player_respawn = nil
        __lua2cs_player_kill = nil
        __lua2cs_player_kick = nil
        __lua2cs_player_change_team = nil
        __lua2cs_player_teleport = nil
        __lua2cs_player_set_health = nil
        __lua2cs_player_set_armor = nil
        __lua2cs_player_set_money = nil
        __lua2cs_player_aim_target = nil
        __lua2cs_player_set_max_health = nil
        __lua2cs_player_set_gravity = nil
        __lua2cs_player_set_velocity_modifier = nil
        __lua2cs_player_set_model = nil
        __lua2cs_player_set_render_color = nil
        __lua2cs_player_set_ammo = nil
        __lua2cs_player_set_score = nil
        __lua2cs_player_set_round_score = nil
        __lua2cs_player_set_mvps = nil
        __lua2cs_player_set_voice_flags = nil
        __lua2cs_player_replicate_convar = nil
        __lua2cs_player_set_fake_convar = nil
        __lua2cs_player_emit_sound = nil
        __lua2cs_command_reply = nil
        """;

    internal static string ProbeRuntimeVersion()
    {
        using var state = new Lua();
        state.State.Encoding = Encoding.UTF8;
        var version = state.GetString("_VERSION") ?? string.Empty;
        if (!version.StartsWith("Lua 5.4", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"需要 Lua 5.4 原生运行库，当前探测结果为 '{version}'。");
        }
        return version;
    }

    public LuaPlugin Prepare(string scriptPath)
    {
        var fullPath = Path.GetFullPath(scriptPath);
        var state = new Lua { UseTraceback = true };
        state.State.Encoding = Encoding.UTF8;
        LuaPlugin? plugin = null;

        try
        {
            plugin = new LuaPlugin(fullPath, state, logger, slowCallbackMilliseconds);
            var api = new LuaApi(plugin, logger);
            plugin.Api = api;

            RegisterFunctions(state, api);
            state["__lua2cs_module_path"] = Path.GetDirectoryName(fullPath)!;
            state.DoString(Bootstrap, "@lua2cs/bootstrap.lua");
            api.InitializeLuaMethods();

            if (!allowUnsafeLibraries)
            {
                state.DoString(
                    "dofile=nil; loadfile=nil; io=nil; os.execute=nil; os.remove=nil; os.rename=nil; package.loadlib=nil",
                    "@lua2cs/sandbox.lua");
            }

            state.DoFile(fullPath);
            if (string.IsNullOrWhiteSpace(plugin.Name))
            {
                throw new InvalidDataException("The script must call cs.plugin({ name = ... }).");
            }

            return plugin;
        }
        catch
        {
            plugin?.Dispose();
            if (plugin is null)
            {
                state.Dispose();
            }

            throw;
        }
    }

    private static void RegisterFunctions(Lua state, LuaApi api)
    {
        Register(state, api, "__lua2cs_create_plugin", nameof(LuaApi.CreatePlugin));
        Register(state, api, "__lua2cs_register_event", nameof(LuaApi.RegisterEvent));
        Register(state, api, "__lua2cs_register_listener", nameof(LuaApi.RegisterListener));
        Register(state, api, "__lua2cs_register_command", nameof(LuaApi.RegisterCommand));
        Register(state, api, "__lua2cs_register_command_listener", nameof(LuaApi.RegisterCommandListener));
        Register(state, api, "__lua2cs_register_timer", nameof(LuaApi.RegisterTimer));
        Register(state, api, "__lua2cs_register_frame", nameof(LuaApi.RegisterFrame));
        Register(state, api, "__lua2cs_register_load", nameof(LuaApi.RegisterLoad));
        Register(state, api, "__lua2cs_register_unload", nameof(LuaApi.RegisterUnload));
        Register(state, api, "__lua2cs_cancel_registration", nameof(LuaApi.CancelRegistration));
        Register(state, api, "__lua2cs_log_debug", nameof(LuaApi.LogDebug));
        Register(state, api, "__lua2cs_log_info", nameof(LuaApi.LogInfo));
        Register(state, api, "__lua2cs_log_warn", nameof(LuaApi.LogWarning));
        Register(state, api, "__lua2cs_log_error", nameof(LuaApi.LogError));
        Register(state, api, "__lua2cs_server_chat", nameof(LuaApi.ServerPrintChatAll));
        Register(state, api, "__lua2cs_server_console", nameof(LuaApi.ServerPrintConsole));
        Register(state, api, "__lua2cs_server_command", nameof(LuaApi.ServerExecute));
        Register(state, api, "__lua2cs_server_info", nameof(LuaApi.GetServerInfo));
        Register(state, api, "__lua2cs_server_maps", nameof(LuaApi.GetMapList));
        Register(state, api, "__lua2cs_server_is_map_valid", nameof(LuaApi.ServerIsMapValid));
        Register(state, api, "__lua2cs_server_precache_model", nameof(LuaApi.ServerPrecacheModel));
        Register(state, api, "__lua2cs_convar_get", nameof(LuaApi.GetConVar));
        Register(state, api, "__lua2cs_convar_set", nameof(LuaApi.SetConVar));
        Register(state, api, "__lua2cs_capability_events", nameof(LuaApi.GetEventNames));
        Register(state, api, "__lua2cs_capability_listeners", nameof(LuaApi.GetListenerNames));
        Register(state, api, "__lua2cs_game_rules", nameof(LuaApi.GetGameRules));
        Register(state, api, "__lua2cs_game_terminate_round", nameof(LuaApi.TerminateRound));
        Register(state, api, "__lua2cs_nav_areas", nameof(LuaApi.GetNavAreas));
        Register(state, api, "__lua2cs_nav_closest", nameof(LuaApi.GetClosestNavArea));
        Register(state, api, "__lua2cs_menu_open", nameof(LuaApi.OpenMenu));
        Register(state, api, "__lua2cs_menu_close", nameof(LuaApi.CloseMenu));
        Register(state, api, "__lua2cs_storage_get", nameof(LuaApi.StorageGet));
        Register(state, api, "__lua2cs_storage_has", nameof(LuaApi.StorageHas));
        Register(state, api, "__lua2cs_storage_set_string", nameof(LuaApi.StorageSetString));
        Register(state, api, "__lua2cs_storage_set_integer", nameof(LuaApi.StorageSetInteger));
        Register(state, api, "__lua2cs_storage_set_number", nameof(LuaApi.StorageSetNumber));
        Register(state, api, "__lua2cs_storage_set_boolean", nameof(LuaApi.StorageSetBoolean));
        Register(state, api, "__lua2cs_storage_delete", nameof(LuaApi.StorageDelete));
        Register(state, api, "__lua2cs_storage_clear", nameof(LuaApi.StorageClear));
        Register(state, api, "__lua2cs_storage_all", nameof(LuaApi.StorageAll));
        Register(state, api, "__lua2cs_entities_find", nameof(LuaApi.FindEntities));
        Register(state, api, "__lua2cs_entities_get", nameof(LuaApi.GetEntity));
        Register(state, api, "__lua2cs_entities_create", nameof(LuaApi.CreateEntity));
        Register(state, api, "__lua2cs_weapons_get", nameof(LuaApi.GetWeapon));
        Register(state, api, "__lua2cs_weapons_find", nameof(LuaApi.FindWeapons));
        Register(state, api, "__lua2cs_entity_refresh", nameof(LuaApi.RefreshEntity));
        Register(state, api, "__lua2cs_entity_spawn", nameof(LuaApi.EntitySpawn));
        Register(state, api, "__lua2cs_entity_input", nameof(LuaApi.EntityInput));
        Register(state, api, "__lua2cs_entity_remove", nameof(LuaApi.EntityRemove));
        Register(state, api, "__lua2cs_entity_teleport", nameof(LuaApi.EntityTeleport));
        Register(state, api, "__lua2cs_entity_set_health", nameof(LuaApi.EntitySetHealth));
        Register(state, api, "__lua2cs_entity_set_max_health", nameof(LuaApi.EntitySetMaxHealth));
        Register(state, api, "__lua2cs_entity_set_gravity", nameof(LuaApi.EntitySetGravity));
        Register(state, api, "__lua2cs_entity_set_model", nameof(LuaApi.EntitySetModel));
        Register(state, api, "__lua2cs_entity_set_render_color", nameof(LuaApi.EntitySetRenderColor));
        Register(state, api, "__lua2cs_entity_emit_sound", nameof(LuaApi.EntityEmitSound));
        Register(state, api, "__lua2cs_weapon_set_ammo", nameof(LuaApi.WeaponSetAmmo));
        Register(state, api, "__lua2cs_weapon_set_econ", nameof(LuaApi.WeaponSetEcon));
        Register(state, api, "__lua2cs_players_all", nameof(LuaApi.GetPlayers));
        Register(state, api, "__lua2cs_players_get", nameof(LuaApi.GetPlayer));
        Register(state, api, "__lua2cs_players_get_userid", nameof(LuaApi.GetPlayerByUserId));
        Register(state, api, "__lua2cs_players_get_steamid", nameof(LuaApi.GetPlayerBySteamId));
        Register(state, api, "__lua2cs_players_find", nameof(LuaApi.FindPlayers));
        Register(state, api, "__lua2cs_players_humans", nameof(LuaApi.GetHumanPlayers));
        Register(state, api, "__lua2cs_players_bots", nameof(LuaApi.GetBots));
        Register(state, api, "__lua2cs_players_count", nameof(LuaApi.GetPlayerCount));
        Register(state, api, "__lua2cs_players_target", nameof(LuaApi.TargetPlayers));
        Register(state, api, "__lua2cs_player_is_current", nameof(LuaApi.IsCurrentPlayer));
        Register(state, api, "__lua2cs_player_chat", nameof(LuaApi.PlayerPrintChat));
        Register(state, api, "__lua2cs_player_console", nameof(LuaApi.PlayerPrintConsole));
        Register(state, api, "__lua2cs_player_center", nameof(LuaApi.PlayerPrintCenter));
        Register(state, api, "__lua2cs_player_alert", nameof(LuaApi.PlayerPrintAlert));
        Register(state, api, "__lua2cs_player_html", nameof(LuaApi.PlayerPrintHtml));
        Register(state, api, "__lua2cs_player_refresh", nameof(LuaApi.RefreshPlayer));
        Register(state, api, "__lua2cs_player_permission", nameof(LuaApi.PlayerHasPermission));
        Register(state, api, "__lua2cs_player_can_target", nameof(LuaApi.PlayerCanTarget));
        Register(state, api, "__lua2cs_player_convar", nameof(LuaApi.PlayerGetConVar));
        Register(state, api, "__lua2cs_player_execute", nameof(LuaApi.PlayerExecute));
        Register(state, api, "__lua2cs_player_give_item", nameof(LuaApi.PlayerGiveItem));
        Register(state, api, "__lua2cs_player_give_weapon", nameof(LuaApi.PlayerGiveWeapon));
        Register(state, api, "__lua2cs_player_remove_item", nameof(LuaApi.PlayerRemoveItem));
        Register(state, api, "__lua2cs_player_remove_weapons", nameof(LuaApi.PlayerRemoveWeapons));
        Register(state, api, "__lua2cs_player_drop_weapon", nameof(LuaApi.PlayerDropActiveWeapon));
        Register(state, api, "__lua2cs_player_respawn", nameof(LuaApi.PlayerRespawn));
        Register(state, api, "__lua2cs_player_kill", nameof(LuaApi.PlayerKill));
        Register(state, api, "__lua2cs_player_kick", nameof(LuaApi.PlayerKick));
        Register(state, api, "__lua2cs_player_change_team", nameof(LuaApi.PlayerChangeTeam));
        Register(state, api, "__lua2cs_player_teleport", nameof(LuaApi.PlayerTeleport));
        Register(state, api, "__lua2cs_player_set_health", nameof(LuaApi.PlayerSetHealth));
        Register(state, api, "__lua2cs_player_set_armor", nameof(LuaApi.PlayerSetArmor));
        Register(state, api, "__lua2cs_player_set_money", nameof(LuaApi.PlayerSetMoney));
        Register(state, api, "__lua2cs_player_aim_target", nameof(LuaApi.PlayerGetAimTarget));
        Register(state, api, "__lua2cs_player_set_max_health", nameof(LuaApi.PlayerSetMaxHealth));
        Register(state, api, "__lua2cs_player_set_gravity", nameof(LuaApi.PlayerSetGravity));
        Register(state, api, "__lua2cs_player_set_velocity_modifier", nameof(LuaApi.PlayerSetVelocityModifier));
        Register(state, api, "__lua2cs_player_set_model", nameof(LuaApi.PlayerSetModel));
        Register(state, api, "__lua2cs_player_set_render_color", nameof(LuaApi.PlayerSetRenderColor));
        Register(state, api, "__lua2cs_player_set_ammo", nameof(LuaApi.PlayerSetAmmo));
        Register(state, api, "__lua2cs_player_set_score", nameof(LuaApi.PlayerSetScore));
        Register(state, api, "__lua2cs_player_set_round_score", nameof(LuaApi.PlayerSetRoundScore));
        Register(state, api, "__lua2cs_player_set_mvps", nameof(LuaApi.PlayerSetMvps));
        Register(state, api, "__lua2cs_player_set_voice_flags", nameof(LuaApi.PlayerSetVoiceFlags));
        Register(state, api, "__lua2cs_player_replicate_convar", nameof(LuaApi.PlayerReplicateConVar));
        Register(state, api, "__lua2cs_player_set_fake_convar", nameof(LuaApi.PlayerSetFakeConVar));
        Register(state, api, "__lua2cs_player_emit_sound", nameof(LuaApi.PlayerEmitSound));
        Register(state, api, "__lua2cs_command_reply", nameof(LuaApi.CommandReply));
    }

    private static void Register(Lua state, LuaApi api, string luaName, string methodName)
    {
        var method = typeof(LuaApi).GetMethod(methodName, BindingFlags.Instance | BindingFlags.Public)
                     ?? throw new MissingMethodException(typeof(LuaApi).FullName, methodName);
        state.RegisterFunction(luaName, api, method);
    }
}
