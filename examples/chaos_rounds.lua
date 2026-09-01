local plugin = cs.plugin({
    name = "混沌回合",
    version = "0.0.1",
    description = "每回合随机启用一种夸张规则，并在结束或热重载时恢复属性"
})

local config = {
    announce_delay = 0.5,
    modes = {
        { id = "moon", name = "月球漫步", detail = "所有玩家低重力并略微加速" },
        { id = "tank", name = "钢铁洪流", detail = "所有玩家 300 HP、满甲和重机枪" },
        { id = "sniper", name = "一镜定生死", detail = "只保留刀和 AWP" },
        { id = "grenadier", name = "爆破艺术", detail = "只保留刀、沙鹰和完整投掷物" },
        { id = "knife", name = "刀锋狂欢", detail = "只允许刀，移动和重力大幅强化" }
    }
}

local active_mode = nil
local generation = 0

local function human(player)
    return player ~= nil and not player.is_bot and not player.is_hltv
end

local function restore(player)
    player:set_max_health(100)
    if player.health ~= nil and player.health > 100 then player:set_health(100) end
    player:set_gravity(1)
    player:set_velocity_modifier(1)
    player:set_render_color(255, 255, 255, 255)
end

local function apply_mode(player)
    if not human(player) or not player.is_alive or active_mode == nil then return end
    restore(player)

    if active_mode.id == "moon" then
        player:set_gravity(0.35)
        player:set_velocity_modifier(1.15)
        player:set_render_color(130, 190, 255, 255)
    elseif active_mode.id == "tank" then
        player:remove_weapons()
        player:give_item("weapon_knife")
        player:give_item("weapon_m249")
        player:set_max_health(300)
        player:set_health(300)
        player:set_armor(100)
        player:set_velocity_modifier(0.82)
        player:set_render_color(90, 110, 90, 255)
    elseif active_mode.id == "sniper" then
        player:remove_weapons()
        player:give_item("weapon_knife")
        player:give_item("weapon_awp")
    elseif active_mode.id == "grenadier" then
        player:remove_weapons()
        player:give_item("weapon_knife")
        player:give_item("weapon_deagle")
        player:give_item("weapon_hegrenade")
        player:give_item("weapon_flashbang")
        player:give_item("weapon_smokegrenade")
        player:give_item("weapon_molotov")
    elseif active_mode.id == "knife" then
        player:remove_weapons()
        player:give_item("weapon_knife")
        player:set_gravity(0.55)
        player:set_velocity_modifier(1.35)
        player:set_render_color(255, 70, 220, 255)
    end
    player:print_center("混沌规则：" .. active_mode.name)
end

plugin:on("round_start", function()
    generation = generation + 1
    active_mode = config.modes[math.random(#config.modes)]
    local expected_generation = generation
    plugin:after(config.announce_delay, function()
        if expected_generation ~= generation or active_mode == nil then return end
        cs.server.print_chat_all(string.format("%s[混沌回合] %s：%s",
            cs.colors.gold, active_mode.name, active_mode.detail))
        for _, player in ipairs(cs.players.humans()) do apply_mode(player) end
    end)
    return cs.continue
end)

plugin:on("player_spawn", function(event)
    local player = event.player
    if not human(player) then return cs.continue end
    local steam_id = player.steam_id
    local expected_generation = generation
    plugin:after(0.2, function()
        if expected_generation ~= generation then return end
        apply_mode(cs.players.get_steamid(steam_id))
    end)
    return cs.continue
end)

plugin:on("round_end", function()
    generation = generation + 1
    active_mode = nil
    for _, player in ipairs(cs.players.humans()) do restore(player) end
    return cs.continue
end)

plugin:command("css_chaos", {
    description = "查看本回合混沌规则",
    allow_console = true
}, function(_, command)
    if active_mode == nil then
        command:reply("当前没有混沌规则。")
    else
        command:reply(active_mode.name .. "：" .. active_mode.detail)
    end
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    active_mode = nil
end)

plugin:on_unload(function()
    generation = generation + 1
    for _, player in ipairs(cs.players.humans()) do restore(player) end
end)
