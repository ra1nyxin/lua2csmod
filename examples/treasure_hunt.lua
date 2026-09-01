local plugin = cs.plugin({
    name = "导航寻宝赛",
    version = "0.0.1",
    description = "利用地图导航网格随机藏宝，玩家依靠方向和距离提示争夺宝物"
})

local config = {
    pickup_radius = 100,
    scan_interval = 0.25,
    score_to_win = 5,
    reward_money = 1000,
    money_cap = 16000,
    nav_limit = 4096
}

local game = nil

local function distance(a, b)
    local dx, dy, dz = a.x - b.x, a.y - b.y, a.z - b.z
    return math.sqrt(dx * dx + dy * dy + dz * dz), dx, dy
end

local function direction(dx, dy)
    local horizontal = math.abs(dx) > 80 and (dx > 0 and "东" or "西") or ""
    local vertical = math.abs(dy) > 80 and (dy > 0 and "北" or "南") or ""
    local result = vertical .. horizontal
    return result ~= "" and result or "附近"
end

local function choose_treasure()
    if game == nil or #game.areas == 0 then return end
    local area = game.areas[math.random(#game.areas)]
    game.target = { x = area.center.x, y = area.center.y, z = area.center.z + 16 }
    game.serial = game.serial + 1
    cs.server.print_chat_all(cs.colors.gold .. "[导航寻宝] 新宝物已经藏好，跟随中央提示寻找！")
end

local function finish(player)
    cs.server.print_chat_all(cs.colors.gold .. player.name .. " 集齐 " .. config.score_to_win .. " 件宝物，赢得寻宝赛！")
    game = nil
end

plugin:command("css_treasure_start", {
    description = "在当前地图开始导航寻宝赛",
    permission = "@css/generic",
    allow_console = true
}, function(_, command)
    if game ~= nil then
        command:reply("寻宝赛已经开始。")
        return
    end
    local areas = cs.nav.areas(config.nav_limit)
    if #areas < 8 then
        command:reply("当前地图导航区域不足，无法可靠随机藏宝。")
        return
    end
    -- 过滤特别狭小的导航块，减少目标落在门缝、梯子边缘等难以触发的位置。
    local candidates = {}
    for _, area in ipairs(areas) do
        if area.width >= 80 and area.height >= 80 then candidates[#candidates + 1] = area end
    end
    if #candidates < 8 then
        command:reply("当前地图可用的大型导航区域不足。")
        return
    end
    game = { areas = candidates, target = nil, scores = {}, serial = 0 }
    choose_treasure()
    command:reply("寻宝赛已开始，共有 " .. #candidates .. " 个候选区域。")
end)

plugin:command("css_treasure", {
    description = "查看自己的寻宝积分",
    allow_console = false
}, function(player, command)
    command:reply(string.format("寻宝积分：%d/%d。", game and (game.scores[player.steam_id] or 0) or 0, config.score_to_win))
end)

plugin:every(config.scan_interval, function()
    if game == nil or game.target == nil then return end
    local captured_by = nil
    for _, player in ipairs(cs.players.humans()) do
        if not player.is_hltv and player.is_alive and player.position ~= nil then
            local meters, dx, dy = distance(player.position, game.target)
            if meters <= config.pickup_radius and captured_by == nil then
                captured_by = player
            else
                player:print_html(string.format(
                    "<font color='#ffd34d'>宝物在%s</font><br>距离 %.0f",
                    direction(dx, dy),
                    meters
                ), 1)
            end
        end
    end
    if captured_by == nil or game == nil then return end

    local steam_id = captured_by.steam_id
    game.scores[steam_id] = (game.scores[steam_id] or 0) + 1
    if captured_by.money ~= nil then
        captured_by:set_money(math.min(config.money_cap, captured_by.money + config.reward_money))
    end
    cs.server.print_chat_all(string.format("%s%s 找到宝物，当前 %d/%d！",
        cs.colors.green, captured_by.name, game.scores[steam_id], config.score_to_win))
    if game.scores[steam_id] >= config.score_to_win then
        finish(captured_by)
    else
        choose_treasure()
    end
end)

plugin:on("round_end", function()
    game = nil
    return cs.continue
end)

plugin:listen("OnMapStart", function() game = nil end)
plugin:on_unload(function() game = nil end)
