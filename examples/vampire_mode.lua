local plugin = cs.plugin({
    name = "猩红吸血模式",
    version = "0.0.1",
    description = "造成伤害即可吸血，击杀还会永久提高本条生命的上限"
})

local config = {
    lifesteal_ratio = 0.45,
    kill_bonus = 25,
    base_max_health = 100,
    absolute_max_health = 300
}

local empowered = {}

local function human(player)
    return player ~= nil and not player.is_bot and not player.is_hltv
end

local function heal_from_damage(steam_id, amount)
    local player = cs.players.get_steamid(steam_id)
    if player == nil or not player.is_alive or amount <= 0 then return end
    local maximum = player.max_health or config.base_max_health
    local health = player.health or 0
    player:set_health(math.min(maximum, health + math.max(1, math.floor(amount * config.lifesteal_ratio))))
end

plugin:on("player_hurt", function(event)
    local attacker = event.attacker
    local victim = event.player
    if not human(attacker) or victim == nil or attacker.steam_id == victim.steam_id then
        return cs.continue
    end

    local damage = tonumber(event.dmg_health) or 0
    local steam_id = attacker.steam_id
    -- 在伤害事件完成后再读取最新血量，避免用事件期间的旧 Pawn 快照覆盖状态。
    plugin:next_frame(function() heal_from_damage(steam_id, damage) end)
    return cs.continue
end)

plugin:on("player_death", function(event)
    local attacker = event.attacker
    local victim = event.player
    if not human(attacker) or victim == nil or attacker.steam_id == victim.steam_id then
        return cs.continue
    end

    local new_max = math.min(
        config.absolute_max_health,
        (attacker.max_health or config.base_max_health) + config.kill_bonus
    )
    empowered[attacker.steam_id] = new_max
    attacker:set_max_health(new_max)
    attacker:set_health(math.min(new_max, (attacker.health or 0) + config.kill_bonus))
    attacker:set_render_color(255, math.max(40, 255 - new_max), math.max(40, 255 - new_max), 255)
    attacker:print_chat(string.format("%s鲜血强化：生命上限提升至 %d。", cs.colors.red, new_max))
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    local player = event.player
    if not human(player) then return cs.continue end
    empowered[player.steam_id] = nil
    plugin:next_frame(function()
        local current = cs.players.get_steamid(player.steam_id)
        if current == nil or not current.is_alive then return end
        current:set_max_health(config.base_max_health)
        current:set_health(config.base_max_health)
        current:set_render_color(255, 255, 255, 255)
    end)
    return cs.continue
end)

plugin:command("css_vampire", {
    description = "查看吸血模式状态",
    allow_console = false
}, function(player, command)
    command:reply(string.format(
        "吸血比例 %.0f%%，当前生命上限 %d/%d。",
        config.lifesteal_ratio * 100,
        empowered[player.steam_id] or player.max_health or config.base_max_health,
        config.absolute_max_health
    ))
end)

plugin:listen("OnMapStart", function() empowered = {} end)

plugin:on_unload(function()
    for _, player in ipairs(cs.players.humans()) do
        player:set_max_health(config.base_max_health)
        if player.health ~= nil and player.health > config.base_max_health then
            player:set_health(config.base_max_health)
        end
        player:set_render_color(255, 255, 255, 255)
    end
end)
