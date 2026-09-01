local plugin = cs.plugin({
    name = "一发入魂",
    version = "0.0.1",
    description = "每人只有一发手枪子弹，击杀才能补弹，打空后只能用刀"
})

local config = {
    pistol = "weapon_deagle",
    spawn_delay = 0.15,
    knife_kill_bonus = 2
}

local kills = {}

local function human(player)
    return player ~= nil and not player.is_bot and not player.is_hltv
end

local function find_pistol(player)
    for _, weapon in ipairs(player.weapon_details or {}) do
        if weapon.designer_name == config.pistol then return weapon end
    end
    return nil
end

local function arm(steam_id)
    local player = cs.players.get_steamid(steam_id)
    if player == nil or not player.is_alive then return end
    player:remove_weapons()
    player:give_item("weapon_knife")
    player:give_weapon(config.pistol, { clip = 1, reserve = 0 })
    player:set_armor(0)
    player:print_center("一发入魂\n击杀才能补弹")
end

local function refill(player, bullets)
    local current = cs.players.get_steamid(player.steam_id)
    if current == nil or not current.is_alive then return end
    local pistol = find_pistol(current)
    if pistol == nil then
        current:give_weapon(config.pistol, { clip = bullets, reserve = 0 })
    else
        pistol:set_ammo(bullets, 0)
    end
    current:print_chat(string.format("%s击杀奖励：手枪补充 %d 发。", cs.colors.green, bullets))
end

plugin:on("round_start", function()
    kills = {}
    cs.server.print_chat_all(cs.colors.gold .. "[一发入魂] 每人一发沙鹰；击杀补弹，打空只能拔刀！")
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    if not human(event.player) then return cs.continue end
    local steam_id = event.player.steam_id
    plugin:after(config.spawn_delay, function() arm(steam_id) end)
    return cs.continue
end)

plugin:on("player_death", function(event)
    local attacker = event.attacker
    local victim = event.player
    if not human(attacker) or victim == nil or attacker.steam_id == victim.steam_id then
        return cs.continue
    end

    kills[attacker.steam_id] = (kills[attacker.steam_id] or 0) + 1
    local used_weapon = tostring(event.weapon or "")
    local bullets = used_weapon:find("knife", 1, true) and config.knife_kill_bonus or 1
    -- 下一帧刷新武器列表，避开死亡事件中武器服务尚未完成更新的窗口。
    local steam_id = attacker.steam_id
    plugin:next_frame(function()
        local current = cs.players.get_steamid(steam_id)
        if current ~= nil then refill(current, bullets) end
    end)
    return cs.continue
end)

plugin:command("css_chamber", {
    description = "查看一发入魂击杀数和当前弹药",
    allow_console = false
}, function(player, command)
    local pistol = find_pistol(player)
    command:reply(string.format(
        "击杀 %d，沙鹰弹匣 %d，备用 %d。",
        kills[player.steam_id] or 0,
        pistol and pistol.clip or 0,
        pistol and pistol.reserve or 0
    ))
end)

plugin:listen("OnMapStart", function() kills = {} end)
