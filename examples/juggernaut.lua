local plugin = cs.plugin({
    name = "重装 Boss 战",
    version = "0.0.1",
    description = "每回合随机一名玩家化身高血量重装 Boss"
})

local config = {
    health_per_enemy = 180,
    minimum_health = 500,
    boss_gravity = 0.85,
    boss_weapon = "weapon_m249",
    reveal_interval = 8
}

local boss_id = nil
local round_generation = 0

local function humans(alive_only)
    local result = {}
    for _, player in ipairs(cs.players.humans()) do
        if not player.is_hltv and (not alive_only or player.is_alive) then
            result[#result + 1] = player
        end
    end
    return result
end

local function restore(player)
    player:set_max_health(100)
    if player.health ~= nil and player.health > 100 then player:set_health(100) end
    player:set_gravity(1)
    player:set_velocity_modifier(1)
    player:set_render_color(255, 255, 255, 255)
end

local function equip_boss(player, enemy_count)
    local health = math.max(config.minimum_health, enemy_count * config.health_per_enemy)
    player:remove_weapons()
    player:give_item("weapon_knife")
    player:give_item(config.boss_weapon)
    player:give_item("weapon_hegrenade")
    player:set_max_health(health)
    player:set_health(health)
    player:set_armor(100)
    player:set_gravity(config.boss_gravity)
    player:set_velocity_modifier(1.08)
    player:set_render_color(255, 70, 35, 255)
    player:print_alert("你是本回合重装 Boss！")
    cs.server.print_chat_all(string.format(
        "%s%s 成为重装 Boss：%d HP，对抗其余所有玩家！",
        cs.colors.red,
        player.name,
        health
    ))
end

local function select_boss(expected_generation)
    if expected_generation ~= round_generation then return end
    local players = humans(true)
    if #players < 2 then
        cs.server.print_chat_all("[重装 Boss] 至少需要两名存活玩家。")
        return
    end
    local boss = players[math.random(#players)]
    boss_id = boss.steam_id
    equip_boss(boss, #players - 1)
end

plugin:on("round_start", function()
    round_generation = round_generation + 1
    boss_id = nil
    local expected_generation = round_generation
    -- 等待正常出生和发枪结束后再覆盖 Boss 装备。
    plugin:after(1, function() select_boss(expected_generation) end)
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    local player = event.player
    if player == nil or player.is_bot or player.is_hltv then return cs.continue end
    if player.steam_id ~= boss_id then restore(player) end
    return cs.continue
end)

plugin:on("player_death", function(event)
    local victim = event.player
    if victim == nil or victim.steam_id ~= boss_id then return cs.continue end

    boss_id = nil
    cs.server.print_chat_all(cs.colors.green .. "重装 Boss 已被击败！")
    -- 由原始游戏规则决定阵营胜负，此处不强行覆盖正常回合结算。
    return cs.continue
end)

plugin:every(config.reveal_interval, function()
    if boss_id == nil then return end
    local boss = cs.players.get_steamid(boss_id)
    if boss == nil or not boss.is_alive then
        boss_id = nil
        return
    end
    cs.server.print_chat_all(string.format(
        "%s[Boss 情报] %s 剩余 %d HP，位置：%.0f %.0f %.0f",
        cs.colors.orange,
        boss.name,
        boss.health or 0,
        boss.position.x,
        boss.position.y,
        boss.position.z
    ))
end)

plugin:listen("OnMapStart", function()
    round_generation = round_generation + 1
    boss_id = nil
end)

plugin:on_unload(function()
    round_generation = round_generation + 1
    if boss_id ~= nil then
        local boss = cs.players.get_steamid(boss_id)
        if boss ~= nil then restore(boss) end
    end
end)
