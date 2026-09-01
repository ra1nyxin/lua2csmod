local plugin = cs.plugin({
    name = "枪械升级战",
    version = "0.0.1",
    description = "每次击杀升级武器，完成全部等级即可赢下回合"
})

local config = {
    levels = {
        "weapon_glock",
        "weapon_deagle",
        "weapon_mp9",
        "weapon_nova",
        "weapon_ak47",
        "weapon_ssg08",
        "weapon_awp",
        "weapon_knife"
    },
    equip_delay = 0.15
}

local levels = {}
local round_finished = false
local generation = 0

local function human(player)
    return player ~= nil and not player.is_bot and not player.is_hltv
end

local function current_level(steam_id)
    return levels[steam_id] or 1
end

local function equip(steam_id)
    local player = cs.players.get_steamid(steam_id)
    if player == nil or not player.is_alive or round_finished then return end

    local level = current_level(steam_id)
    local weapon = config.levels[level]
    player:remove_weapons()
    -- 最终刀战不重复发刀；其他等级始终保留近战备用。
    player:give_item("weapon_knife")
    if weapon ~= "weapon_knife" then player:give_item(weapon) end
    player:set_armor(100)
    player:print_center(string.format("等级 %d/%d\n%s", level, #config.levels, weapon))
end

local function schedule_equip(player)
    local steam_id = player.steam_id
    local expected_generation = generation
    plugin:after(config.equip_delay, function()
        -- 延迟回调重新按 SteamID 查找，不保留会过期的 Pawn 快照。
        if generation == expected_generation then equip(steam_id) end
    end)
end

plugin:on("round_start", function()
    generation = generation + 1
    levels = {}
    round_finished = false
    cs.server.print_chat_all(cs.colors.gold .. "[枪械升级战] 击杀即可升级，最终用刀击杀获胜！")
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    if human(event.player) then schedule_equip(event.player) end
    return cs.continue
end)

plugin:on("player_death", function(event)
    if round_finished then return cs.continue end
    local attacker = event.attacker
    local victim = event.player
    if not human(attacker) or victim == nil or attacker.steam_id == victim.steam_id then
        return cs.continue
    end

    local old_level = current_level(attacker.steam_id)
    if old_level >= #config.levels then
        round_finished = true
        cs.server.print_chat_all(cs.colors.gold .. attacker.name .. " 完成最终刀杀，赢得枪械升级战！")
        cs.game.terminate_round(3, cs.round_end.draw)
        return cs.continue
    end

    local new_level = old_level + 1
    levels[attacker.steam_id] = new_level
    attacker:set_score(new_level - 1)
    attacker:print_chat(string.format(
        "%s升级到 %d/%d：%s",
        cs.colors.green,
        new_level,
        #config.levels,
        config.levels[new_level]
    ))
    schedule_equip(attacker)
    return cs.continue
end)

plugin:command("css_gungame", {
    description = "查看自己的枪械升级进度",
    allow_console = false
}, function(player, command)
    local level = current_level(player.steam_id)
    command:reply(string.format("当前等级 %d/%d：%s", level, #config.levels, config.levels[level]))
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    levels = {}
    round_finished = false
end)
