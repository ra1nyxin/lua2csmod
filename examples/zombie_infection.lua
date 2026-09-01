local plugin = cs.plugin({
    name = "僵尸感染",
    version = "0.0.1",
    description = "随机母体感染人类，僵尸死亡复生，最后一名人类陷入围攻"
})

local config = {
    setup_delay = 1.2,
    respawn_delay = 1.5,
    zombie_health = 260,
    mother_health = 500,
    zombie_speed = 1.22,
    zombie_gravity = 0.82
}

local zombies = {}
local mother_id = nil
local generation = 0
local active = false

local function human_players(alive_only)
    local result = {}
    for _, player in ipairs(cs.players.humans()) do
        if not player.is_hltv and (not alive_only or player.is_alive) then result[#result + 1] = player end
    end
    return result
end

local function restore_attributes(player)
    player:set_max_health(100)
    if player.health ~= nil and player.health > 100 then player:set_health(100) end
    player:set_gravity(1)
    player:set_velocity_modifier(1)
    player:set_render_color(255, 255, 255, 255)
end

local function equip(player)
    if player == nil or not player.is_alive or not active then return end
    restore_attributes(player)
    player:remove_weapons()
    player:give_item("weapon_knife")

    if zombies[player.steam_id] then
        local health = player.steam_id == mother_id and config.mother_health or config.zombie_health
        player:switch_team(cs.team.t)
        player:set_max_health(health)
        player:set_health(health)
        player:set_gravity(config.zombie_gravity)
        player:set_velocity_modifier(config.zombie_speed)
        player:set_render_color(80, 210, 80, 255)
        player:print_center(player.steam_id == mother_id and "你是僵尸母体" or "你已被感染")
    else
        player:switch_team(cs.team.ct)
        player:give_item("weapon_m4a1_silencer")
        player:give_item("weapon_deagle")
        player:set_armor(100)
        player:print_center("活下去，不要被感染")
    end
end

local function delayed_respawn(steam_id, expected_generation)
    plugin:after(config.respawn_delay, function()
        if not active or generation ~= expected_generation then return end
        local player = cs.players.get_steamid(steam_id)
        if player == nil then return end
        if not player.is_alive then player:respawn() end
        plugin:next_frame(function()
            if generation ~= expected_generation then return end
            equip(cs.players.get_steamid(steam_id))
        end)
    end)
end

local function remaining_humans()
    local count = 0
    local last = nil
    for _, player in ipairs(human_players(false)) do
        if not zombies[player.steam_id] then
            count = count + 1
            last = player
        end
    end
    return count, last
end

local function begin(expected_generation)
    if generation ~= expected_generation then return end
    local players = human_players(true)
    if #players < 2 then
        cs.server.print_chat_all("[僵尸感染] 至少需要两名存活玩家。")
        return
    end
    active = true
    local mother = players[math.random(#players)]
    mother_id = mother.steam_id
    zombies[mother_id] = true
    for _, player in ipairs(players) do equip(player) end
    cs.server.print_chat_all(cs.colors.green .. mother.name .. " 成为僵尸母体，感染开始！")
end

plugin:on("round_start", function()
    generation = generation + 1
    zombies = {}
    mother_id = nil
    active = false
    local expected_generation = generation
    plugin:after(config.setup_delay, function() begin(expected_generation) end)
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    if not active or event.player == nil or event.player.is_bot or event.player.is_hltv then return cs.continue end
    local steam_id = event.player.steam_id
    local expected_generation = generation
    plugin:next_frame(function()
        if generation == expected_generation then equip(cs.players.get_steamid(steam_id)) end
    end)
    return cs.continue
end)

plugin:on("player_death", function(event)
    if not active or event.player == nil then return cs.continue end
    local victim = event.player
    local attacker = event.attacker

    if zombies[victim.steam_id] then
        delayed_respawn(victim.steam_id, generation)
        return cs.continue
    end
    if attacker == nil or not zombies[attacker.steam_id] then return cs.continue end

    zombies[victim.steam_id] = true
    cs.server.print_chat_all(cs.colors.green .. victim.name .. " 被 " .. attacker.name .. " 感染！")
    local count, last = remaining_humans()
    if count == 0 then
        active = false
        cs.server.print_chat_all(cs.colors.red .. "所有人类均已感染，僵尸获胜！")
        cs.game.terminate_round(3, cs.round_end.terrorist_win)
    else
        if count == 1 and last ~= nil then
            cs.server.print_chat_all(cs.colors.gold .. last.name .. " 是最后一名人类！")
        end
        delayed_respawn(victim.steam_id, generation)
    end
    return cs.continue
end)

plugin:on("round_end", function()
    generation = generation + 1
    active = false
    return cs.continue
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    zombies = {}
    active = false
end)

plugin:on_unload(function()
    generation = generation + 1
    active = false
    for _, player in ipairs(cs.players.humans()) do restore_attributes(player) end
end)
