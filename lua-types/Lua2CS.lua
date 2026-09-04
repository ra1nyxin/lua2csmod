---@meta

-- Lua2CS 的 Lua Language Server 类型库。
-- 此文件只供编辑器分析，不应在游戏脚本中 require 或 dofile。

---@alias Lua2CSHookResult "continue"|"changed"|"handled"|"stop"
---@alias Lua2CSHookMode "pre"|"post"
---@alias Lua2CSTeam "none"|"spec"|"spectator"|"t"|"terrorist"|"ct"|"counter_terrorist"|integer
---@alias Lua2CSMenuType "chat"|"console"
---@alias Lua2CSPostSelectAction "close"|"reset"|"nothing"

---@class Lua2CSVector
---@field x number X 坐标或俯仰角
---@field y number Y 坐标或偏航角
---@field z number Z 坐标或翻滚角

---@class Lua2CSColor
---@field red integer
---@field green integer
---@field blue integer
---@field alpha integer

---@class Lua2CSPluginSpec
---@field name string 插件名称，必填
---@field version? string 插件版本，默认 `0.0.0`
---@field description? string 插件说明

---@class Lua2CSEventOptions
---@field mode? Lua2CSHookMode 默认 `post`

---@class Lua2CSCommandOptions
---@field description? string 命令说明
---@field permission? string CounterStrikeSharp 权限，例如 `@css/generic`
---@field allow_console? boolean 是否允许服务器控制台和 RCON 调用，默认 `true`
---@field min_args? integer 最少参数数，不含命令名
---@field usage? string 参数用法文本

---@class Lua2CSCommandListenerOptions
---@field mode? Lua2CSHookMode 默认 `pre`

---@class Lua2CSTimerOptions
---@field repeating? boolean 是否循环，默认 `false`
---@field stop_on_map_change? boolean 换图时是否停止，默认 `true`

---@class Lua2CSMenuItem
---@field text string 显示文本，不能包含换行或空字符
---@field disabled? boolean 是否禁用，默认 `false`

---@class Lua2CSMenuSpec
---@field title string 菜单标题
---@field type? Lua2CSMenuType 默认 `chat`
---@field exit_button? boolean 默认 `true`
---@field post_select? Lua2CSPostSelectAction 默认 `reset`
---@field items Lua2CSMenuItem[] 1 到 64 个选项

---@class Lua2CSCommand
---@field name string 实际命令名
---@field args string[] 从 1 开始的参数数组，不含命令名
---@field arg_string string 原始参数字符串
---@field context integer 内部调用上下文
---@field reply fun(self: Lua2CSCommand, message: string) 回复调用者

---@class Lua2CSEventInfo
---@field dont_broadcast boolean Pre 事件中可修改；设为 `true` 可抑制广播

---@class Lua2CSWeaponEconOptions
---@field paint_kit? integer
---@field paint_seed? integer
---@field paint_wear? number 0 到 1
---@field stattrak? integer
---@field item_definition_index? integer
---@field entity_quality? integer
---@field entity_level? integer
---@field item_id? string 64 位数值请使用十进制字符串
---@field account_id? string
---@field inventory_position? string
---@field original_owner_steam_id? string
---@field custom_name? string
---@field custom_name_override? string

---@class Lua2CSWeaponOptions: Lua2CSWeaponEconOptions
---@field clip? integer
---@field reserve? integer
---@field clip_secondary? integer
---@field reserve_secondary? integer

---@class Lua2CSWeapon
---@field handle integer 完整 CHandle
---@field index integer 实体索引
---@field designer_name string
---@field clip integer?
---@field clip_secondary integer?
---@field reserve integer?
---@field reserve_secondary integer?
---@field econ_available boolean
---@field item_definition_index integer?
---@field entity_quality integer?
---@field entity_level integer?
---@field paint_kit integer?
---@field paint_seed integer?
---@field paint_wear number?
---@field stattrak integer?
---@field item_id string?
---@field account_id string?
---@field inventory_position string?
---@field original_owner_steam_id string?
---@field custom_name string?
---@field custom_name_override string?
---@field position Lua2CSVector?
---@field rotation Lua2CSVector?
---@field velocity Lua2CSVector?
---@field owner_handle integer?
---@field refresh fun(self: Lua2CSWeapon): Lua2CSWeapon? 使用完整 CHandle 读取最新快照
---@field set_ammo fun(self: Lua2CSWeapon, clip: integer?, reserve: integer?, clip_secondary: integer?, reserve_secondary: integer?): boolean
---@field set_econ fun(self: Lua2CSWeapon, options: Lua2CSWeaponEconOptions): boolean

---@class Lua2CSEntity
---@field handle integer 完整 CHandle
---@field index integer 实体索引
---@field designer_name string
---@field classname string?
---@field position Lua2CSVector?
---@field rotation Lua2CSVector?
---@field velocity Lua2CSVector?
---@field health integer?
---@field max_health integer?
---@field gravity_scale number?
---@field model string?
---@field render_color Lua2CSColor?
---@field refresh fun(self: Lua2CSEntity): Lua2CSEntity?
---@field spawn fun(self: Lua2CSEntity): boolean
---@field input fun(self: Lua2CSEntity, input_name: string, value: string?, delay: number?): boolean
---@field remove fun(self: Lua2CSEntity, delay: number?): boolean
---@field teleport fun(self: Lua2CSEntity, position: Lua2CSVector?, angles: Lua2CSVector?, velocity: Lua2CSVector?): boolean
---@field set_health fun(self: Lua2CSEntity, value: integer): boolean
---@field set_max_health fun(self: Lua2CSEntity, value: integer): boolean
---@field set_gravity fun(self: Lua2CSEntity, scale: number): boolean
---@field set_model fun(self: Lua2CSEntity, model_name: string, precache: boolean?): boolean
---@field set_render_color fun(self: Lua2CSEntity, red: integer, green: integer, blue: integer, alpha: integer?): boolean
---@field emit_sound fun(self: Lua2CSEntity, sound_event_name: string, volume: number?, pitch: number?): integer

---@class Lua2CSPlayer
---@field snapshot_complete boolean 原生状态不允许读取可选字段时为 `false`
---@field slot integer
---@field user_id integer
---@field name string
---@field steam_id string SteamID64
---@field ip_address string?
---@field team string
---@field team_id integer
---@field is_bot boolean
---@field is_hltv boolean
---@field is_alive boolean
---@field ping integer?
---@field score integer?
---@field round_score integer?
---@field rounds_won integer?
---@field mvps integer?
---@field teammate_color integer?
---@field language string?
---@field voice_flags integer?
---@field health integer?
---@field armor integer?
---@field money integer?
---@field max_health integer?
---@field gravity_scale number?
---@field velocity_modifier number?
---@field has_helmet boolean?
---@field has_defuser boolean?
---@field in_buy_zone boolean?
---@field in_bomb_zone boolean?
---@field is_scoped boolean?
---@field is_defusing boolean?
---@field is_grabbing_hostage boolean?
---@field is_walking boolean?
---@field shots_fired integer?
---@field flags integer?
---@field buttons integer?
---@field position Lua2CSVector?
---@field velocity Lua2CSVector?
---@field eye_angles Lua2CSVector?
---@field active_weapon string?
---@field weapons string[]
---@field active_weapon_info Lua2CSWeapon?
---@field weapon_details Lua2CSWeapon[]
---@field model string?
---@field render_color Lua2CSColor?
---@field print_chat fun(self: Lua2CSPlayer, message: string): boolean
---@field print_console fun(self: Lua2CSPlayer, message: string): boolean
---@field print_center fun(self: Lua2CSPlayer, message: string): boolean
---@field print_alert fun(self: Lua2CSPlayer, message: string): boolean
---@field print_html fun(self: Lua2CSPlayer, html: string, duration: integer): boolean
---@field refresh fun(self: Lua2CSPlayer): Lua2CSPlayer? 按 slot、userid 和 SteamID64 重新读取
---@field has_permission fun(self: Lua2CSPlayer, permission: string): boolean
---@field can_target fun(self: Lua2CSPlayer, target: Lua2CSPlayer): boolean
---@field get_convar fun(self: Lua2CSPlayer, name: string): string?
---@field execute fun(self: Lua2CSPlayer, command: string): boolean
---@field execute_as_server fun(self: Lua2CSPlayer, command: string): boolean
---@field give_item fun(self: Lua2CSPlayer, designer_name: string): boolean
---@field give_weapon fun(self: Lua2CSPlayer, designer_name: string, options: Lua2CSWeaponOptions?): Lua2CSWeapon?
---@field remove_item fun(self: Lua2CSPlayer, designer_name: string): boolean
---@field remove_weapons fun(self: Lua2CSPlayer): boolean
---@field drop_active_weapon fun(self: Lua2CSPlayer): boolean
---@field respawn fun(self: Lua2CSPlayer): boolean
---@field kill fun(self: Lua2CSPlayer, explode: boolean?, force: boolean?): boolean
---@field kick fun(self: Lua2CSPlayer): boolean
---@field change_team fun(self: Lua2CSPlayer, team: Lua2CSTeam): boolean
---@field switch_team fun(self: Lua2CSPlayer, team: Lua2CSTeam): boolean
---@field teleport fun(self: Lua2CSPlayer, position: Lua2CSVector?, angles: Lua2CSVector?, velocity: Lua2CSVector?): boolean
---@field set_health fun(self: Lua2CSPlayer, value: integer): boolean
---@field set_armor fun(self: Lua2CSPlayer, value: integer): boolean
---@field set_money fun(self: Lua2CSPlayer, value: integer): boolean
---@field aim_target fun(self: Lua2CSPlayer): Lua2CSPlayer?
---@field set_max_health fun(self: Lua2CSPlayer, value: integer): boolean
---@field set_gravity fun(self: Lua2CSPlayer, scale: number): boolean
---@field set_velocity_modifier fun(self: Lua2CSPlayer, value: number): boolean
---@field set_model fun(self: Lua2CSPlayer, model_name: string, precache: boolean?): boolean
---@field set_render_color fun(self: Lua2CSPlayer, red: integer, green: integer, blue: integer, alpha: integer?): boolean
---@field close_menu fun(self: Lua2CSPlayer): boolean
---@field set_ammo fun(self: Lua2CSPlayer, clip: integer?, reserve: integer?): boolean
---@field set_score fun(self: Lua2CSPlayer, value: integer): boolean
---@field set_round_score fun(self: Lua2CSPlayer, value: integer): boolean
---@field set_mvps fun(self: Lua2CSPlayer, value: integer): boolean
---@field set_voice_flags fun(self: Lua2CSPlayer, flags: integer): boolean
---@field replicate_convar fun(self: Lua2CSPlayer, name: string, value: string|number|boolean): boolean
---@field set_fake_convar fun(self: Lua2CSPlayer, name: string, value: string|number|boolean): boolean
---@field emit_sound fun(self: Lua2CSPlayer, sound_event_name: string, volume: number?, pitch: number?): integer

---@class Lua2CSPlayerDeathEvent
---@field player Lua2CSPlayer? 死亡玩家
---@field attacker Lua2CSPlayer? 攻击者
---@field assister Lua2CSPlayer? 助攻者
---@field weapon string?
---@field headshot boolean?
---@field penetrated integer?
---@field dominated integer?
---@field revenge integer?

---@class Lua2CSPlayerHurtEvent
---@field player Lua2CSPlayer? 受伤玩家
---@field attacker Lua2CSPlayer? 攻击者
---@field weapon string?
---@field dmg_health integer?
---@field dmg_armor integer?
---@field health integer?
---@field armor integer?
---@field hitgroup integer?

---@class Lua2CSPlayerSpawnEvent
---@field player Lua2CSPlayer?
---@field teamnum integer?

---@class Lua2CSPlayerChatEvent
---@field player Lua2CSPlayer?
---@field text string?
---@field teamonly boolean?

---@class Lua2CSBombEvent
---@field player Lua2CSPlayer?
---@field userid integer?
---@field site integer?

---@class Lua2CSRoundEvent
---@field winner integer?
---@field reason integer?
---@field message string?

---@class Lua2CSServerInfo
---@field map_name string
---@field max_players integer
---@field tick_interval number
---@field tick_count integer
---@field current_time number
---@field ticked_time number
---@field engine_time number
---@field frame_time number

---@class Lua2CSGameRules
---@field freeze_period boolean
---@field warmup_period boolean
---@field warmup_start_time number
---@field warmup_end_time number
---@field round_start_time number
---@field game_restart boolean
---@field game_phase integer
---@field total_rounds_played integer
---@field overtime_playing boolean
---@field bomb_planted boolean
---@field bomb_dropped boolean
---@field ct_timeout_active boolean
---@field terrorist_timeout_active boolean
---@field terrorist_score integer?
---@field ct_score integer?

---@class Lua2CSNavArea
---@field id integer
---@field center Lua2CSVector
---@field normal Lua2CSVector
---@field min Lua2CSVector
---@field max Lua2CSVector
---@field width number
---@field height number
---@field area_2d number
---@field distance number? 仅 `cs.nav.closest` 返回

---@class Lua2CSPlugin
local Lua2CSPlugin = {}

---@param callback fun(hot_reload: boolean): boolean?
function Lua2CSPlugin:on_load(callback) end

---@param callback fun(hot_reload: boolean)
function Lua2CSPlugin:on_unload(callback) end

---@overload fun(self: Lua2CSPlugin, event: "player_death", callback: fun(event: Lua2CSPlayerDeathEvent, info: Lua2CSEventInfo): Lua2CSHookResult?, options: Lua2CSEventOptions?): integer
---@overload fun(self: Lua2CSPlugin, event: "player_hurt", callback: fun(event: Lua2CSPlayerHurtEvent, info: Lua2CSEventInfo): Lua2CSHookResult?, options: Lua2CSEventOptions?): integer
---@overload fun(self: Lua2CSPlugin, event: "player_spawn", callback: fun(event: Lua2CSPlayerSpawnEvent, info: Lua2CSEventInfo): Lua2CSHookResult?, options: Lua2CSEventOptions?): integer
---@overload fun(self: Lua2CSPlugin, event: "player_chat", callback: fun(event: Lua2CSPlayerChatEvent, info: Lua2CSEventInfo): Lua2CSHookResult?, options: Lua2CSEventOptions?): integer
---@overload fun(self: Lua2CSPlugin, event: "round_start"|"round_end", callback: fun(event: Lua2CSRoundEvent, info: Lua2CSEventInfo): Lua2CSHookResult?, options: Lua2CSEventOptions?): integer
---@overload fun(self: Lua2CSPlugin, event: "bomb_planted"|"bomb_defused"|"bomb_exploded"|"bomb_dropped", callback: fun(event: Lua2CSBombEvent, info: Lua2CSEventInfo): Lua2CSHookResult?, options: Lua2CSEventOptions?): integer
---@param event string CounterStrikeSharp 标准事件名
---@param callback fun(event: table, info: Lua2CSEventInfo): Lua2CSHookResult?
---@param options? Lua2CSEventOptions
---@return integer registration_id
function Lua2CSPlugin:on(event, callback, options) end

---@overload fun(self: Lua2CSPlugin, listener: "OnMapStart", callback: fun(map_name: string)): integer
---@overload fun(self: Lua2CSPlugin, listener: "OnClientDisconnect", callback: fun(slot: integer)): integer
---@param listener string CounterStrikeSharp `Listeners` 委托名
---@param callback fun(...: any): Lua2CSHookResult?
---@return integer registration_id
function Lua2CSPlugin:listen(listener, callback) end

---@overload fun(self: Lua2CSPlugin, name: string, callback: fun(player: Lua2CSPlayer?, command: Lua2CSCommand)): integer
---@param name string 建议以 `css_` 开头
---@param options Lua2CSCommandOptions
---@param callback fun(player: Lua2CSPlayer?, command: Lua2CSCommand)
---@return integer registration_id
function Lua2CSPlugin:command(name, options, callback) end

---@overload fun(self: Lua2CSPlugin, name: string, callback: fun(player: Lua2CSPlayer?, command: Lua2CSCommand): Lua2CSHookResult?): integer
---@param name string 已存在的原生命令名
---@param options Lua2CSCommandListenerOptions
---@param callback fun(player: Lua2CSPlayer?, command: Lua2CSCommand): Lua2CSHookResult?
---@return integer registration_id
function Lua2CSPlugin:command_listener(name, options, callback) end

---@param seconds number 大于零的秒数
---@param callback fun()
---@param options? Lua2CSTimerOptions
---@return integer registration_id
function Lua2CSPlugin:timer(seconds, callback, options) end

---@param seconds number
---@param callback fun()
---@return integer registration_id
function Lua2CSPlugin:after(seconds, callback) end

---@param seconds number
---@param callback fun()
---@param options? Lua2CSTimerOptions
---@return integer registration_id
function Lua2CSPlugin:every(seconds, callback, options) end

---@param callback fun()
---@return integer registration_id
function Lua2CSPlugin:next_frame(callback) end

---@param callback fun()
---@return integer registration_id
function Lua2CSPlugin:next_world_update(callback) end

---@param ticks integer 1 到 1000000
---@param callback fun()
---@return integer registration_id
function Lua2CSPlugin:after_ticks(ticks, callback) end

---@param registration_id integer
---@return boolean
function Lua2CSPlugin:cancel(registration_id) end

---@class Lua2CSServer
---@field print_chat_all fun(message: string)
---@field print_console fun(message: string)
---@field execute fun(command: string)
---@field info fun(): Lua2CSServerInfo
---@field maps fun(): string[]
---@field is_map_valid fun(map_name: string): boolean
---@field precache_model fun(model_name: string)

---@class Lua2CSPlayers
---@field all fun(): Lua2CSPlayer[]
---@field humans fun(): Lua2CSPlayer[]
---@field bots fun(): Lua2CSPlayer[]
---@field count fun(): integer
---@field get fun(slot: integer): Lua2CSPlayer?
---@field get_userid fun(userid: integer): Lua2CSPlayer?
---@field get_steamid fun(steam_id: string): Lua2CSPlayer?
---@field find fun(query: string|integer): Lua2CSPlayer[]
---@field target fun(pattern: string, caller: Lua2CSPlayer?): Lua2CSPlayer[]

---@class Lua2CSWeapons
---@field get fun(handle: integer): Lua2CSWeapon?
---@field find fun(designer_name: string, limit: integer?): Lua2CSWeapon[]

---@class Lua2CSEntities
---@field find fun(designer_name: string, limit: integer?): Lua2CSEntity[]
---@field get fun(handle: integer): Lua2CSEntity?
---@field create fun(designer_name: string, spawn: boolean?): Lua2CSEntity? 不要手动创建世界武器

---@class Lua2CSMenu
---@field open fun(player: Lua2CSPlayer, spec: Lua2CSMenuSpec, callback: fun(player: Lua2CSPlayer, index: integer)): boolean
---@field close fun(player: Lua2CSPlayer): boolean

---@class Lua2CSStorage
---@field get fun(key: string, default: any?): string|number|boolean|nil
---@field has fun(key: string): boolean
---@field set fun(key: string, value: string|number|boolean|nil): boolean
---@field delete fun(key: string): boolean
---@field clear fun()
---@field all fun(): table<string, string|number|boolean>

---@class Lua2CSConVars
---@field get fun(name: string): string?
---@field set fun(name: string, value: string|number|boolean): boolean

---@class Lua2CSGame
---@field rules fun(): Lua2CSGameRules?
---@field terminate_round fun(delay: number, reason: string): boolean

---@class Lua2CSNav
---@field areas fun(limit: integer?): Lua2CSNavArea[]
---@field closest fun(position: Lua2CSVector, maximum_distance: number?): Lua2CSNavArea?

---@class Lua2CSCapabilities
---@field events fun(): string[]
---@field listeners fun(): string[]

---@class Lua2CSLog
---@field debug fun(message: any)
---@field info fun(message: any)
---@field warn fun(message: any)
---@field error fun(message: any)

---@class Lua2CSColors
---@field default string
---@field green string
---@field lime string
---@field red string
---@field yellow string
---@field blue string
---@field purple string
---@field grey string
---@field gold string
---@field orange string

---@class Lua2CSTeamConstants
---@field none integer
---@field spectator integer
---@field terrorist integer
---@field t integer
---@field counter_terrorist integer
---@field ct integer

---@class Lua2CSRoundEndConstants
---@field target_bombed string
---@field bomb_defused string
---@field ct_win string
---@field terrorist_win string
---@field draw string
---@field all_hostages_rescued string
---@field target_saved string
---@field game_commencing string

---@class Lua2CSVoiceConstants
---@field normal integer
---@field muted integer
---@field all integer
---@field listen_all integer
---@field team integer
---@field listen_team integer

---@class Lua2CSButtonConstants
---@field attack integer
---@field jump integer
---@field duck integer
---@field forward integer
---@field back integer
---@field use integer
---@field left integer
---@field right integer
---@field move_left integer
---@field move_right integer
---@field attack2 integer
---@field reload integer
---@field speed integer
---@field walk integer
---@field zoom integer

---@class Lua2CS
---@field continue Lua2CSHookResult
---@field changed Lua2CSHookResult
---@field handled Lua2CSHookResult
---@field stop Lua2CSHookResult
---@field plugin fun(spec: Lua2CSPluginSpec): Lua2CSPlugin
---@field vec3 fun(x: number, y: number, z: number): Lua2CSVector
---@field server Lua2CSServer
---@field players Lua2CSPlayers
---@field weapons Lua2CSWeapons
---@field entities Lua2CSEntities
---@field menu Lua2CSMenu
---@field storage Lua2CSStorage
---@field convars Lua2CSConVars
---@field game Lua2CSGame
---@field nav Lua2CSNav
---@field capabilities Lua2CSCapabilities
---@field log Lua2CSLog
---@field colors Lua2CSColors
---@field team Lua2CSTeamConstants
---@field round_end Lua2CSRoundEndConstants
---@field voice Lua2CSVoiceConstants
---@field buttons Lua2CSButtonConstants

---@type Lua2CS
cs = {}
