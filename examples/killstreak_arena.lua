local plugin = cs.plugin({
    name = "连杀竞技场",
    version = "0.0.1",
    description = "用连杀等级驱动即时补给、强化和全服播报"
})

local config = {
    heal_per_kill = 25,
    max_health = 160,
    milestones = {
        [2] = { text = "双杀：护甲补满", armor = 100 },
        [3] = { text = "三杀：获得加速", speed = 1.12 },
        [5] = { text = "五杀：晋升狂战士", max_health = 160, weapon = "weapon_m249" },
        [8] = { text = "八杀：进入传说状态", gravity = 0.65, weapon = "weapon_awp" }
    }
}

-- 长期状态只按 SteamID 保存，slot 只用于即时显示，避免槽位复用串号。
local streaks = {}
local best = {}

local function human(player)
    return player ~= nil and not player.is_bot and not player.is_hltv
end

local function reset_buffs(player)
    if player == nil then return end
    player:set_max_health(100)
    player:set_gravity(1)
    player:set_velocity_modifier(1)
    player:set_render_color(255, 255, 255, 255)
end

local function show_status(player)
    local count = streaks[player.steam_id] or 0
    player:print_html(string.format(
        "<font color='#ffcc33'>连杀 %d</font><br><font color='#eeeeee'>本局最高 %d</font>",
        count,
        best[player.steam_id] or count
    ), 2)
end

local function apply_milestone(player, count)
    local reward = config.milestones[count]
    if reward == nil then return end

    if reward.armor then player:set_armor(reward.armor) end
    if reward.speed then player:set_velocity_modifier(reward.speed) end
    if reward.max_health then
        player:set_max_health(reward.max_health)
        player:set_health(reward.max_health)
        player:set_render_color(255, 80, 80, 255)
    end
    if reward.gravity then player:set_gravity(reward.gravity) end
    if reward.weapon then player:give_item(reward.weapon) end

    cs.server.print_chat_all(cs.colors.gold .. player.name .. " " .. reward.text .. "！")
    player:emit_sound("sounds/ui/achievement_earned.vsnd_c", 0.8, 0)
end

plugin:on("player_death", function(event)
    local victim = event.player
    local attacker = event.attacker

    if human(victim) then
        local lost = streaks[victim.steam_id] or 0
        if lost >= 3 then
            cs.server.print_chat_all(cs.colors.grey .. victim.name .. " 的 " .. lost .. " 连杀被终结。")
        end
        streaks[victim.steam_id] = 0
    end

    if not human(attacker) or victim == nil or attacker.steam_id == victim.steam_id then
        return cs.continue
    end

    local count = (streaks[attacker.steam_id] or 0) + 1
    streaks[attacker.steam_id] = count
    best[attacker.steam_id] = math.max(best[attacker.steam_id] or 0, count)

    -- 击杀后只恢复而不直接改伤害，避免依赖武器伤害事件的版本差异。
    local current_health = attacker.health or 100
    attacker:set_health(math.min(config.max_health, current_health + config.heal_per_kill))
    attacker:set_ammo(nil, 120)
    apply_milestone(attacker, count)
    show_status(attacker)
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    local player = event.player
    if not human(player) then return cs.continue end

    -- 死亡时重置了计数；出生时负责清除上一条生命留下的 Pawn 强化。
    plugin:next_frame(function()
        local current = cs.players.get_steamid(player.steam_id)
        if current ~= nil and current.is_alive then reset_buffs(current) end
    end)
    return cs.continue
end)

plugin:command("css_streak", {
    description = "查看当前连杀和本局最高连杀",
    allow_console = false
}, function(player, command)
    command:reply(string.format(
        "当前连杀：%d，本局最高：%d",
        streaks[player.steam_id] or 0,
        best[player.steam_id] or 0
    ))
end)

plugin:listen("OnMapStart", function()
    streaks = {}
    best = {}
end)

plugin:on_unload(function()
    for _, player in ipairs(cs.players.humans()) do reset_buffs(player) end
end)
