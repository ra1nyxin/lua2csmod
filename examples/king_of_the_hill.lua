local plugin = cs.plugin({
    name = "山丘之王",
    version = "0.0.1",
    description = "管理员放置占领区，双方争夺并持续得分，率先达标获胜"
})

local config = {
    radius = 260,
    score_interval = 1,
    score_to_win = 45,
    round_end_delay = 3
}

local center = nil
local scores = { t = 0, ct = 0 }
local finished = false
local round_active = false

local function distance_squared(a, b)
    local dx, dy, dz = a.x - b.x, a.y - b.y, a.z - b.z
    return dx * dx + dy * dy + dz * dz
end

local function occupants()
    local result = { t = {}, ct = {} }
    if center == nil then return result end
    local radius_squared = config.radius * config.radius
    for _, player in ipairs(cs.players.humans()) do
        if not player.is_hltv and player.is_alive and player.position ~= nil
            and distance_squared(player.position, center) <= radius_squared then
            if player.team_id == cs.team.t then
                result.t[#result.t + 1] = player
            elseif player.team_id == cs.team.ct then
                result.ct[#result.ct + 1] = player
            end
        end
    end
    return result
end

local function state_text(inside)
    if #inside.t > 0 and #inside.ct > 0 then return "争夺中" end
    if #inside.t > 0 then return "T 占领" end
    if #inside.ct > 0 then return "CT 占领" end
    return "无人占领"
end

local function finish(team)
    if finished then return end
    finished = true
    local label = team == "t" and "T" or "CT"
    cs.server.print_chat_all(cs.colors.gold .. label .. " 达到 " .. config.score_to_win .. " 分，赢得山丘之王！")
    cs.game.terminate_round(
        config.round_end_delay,
        team == "t" and cs.round_end.terrorist_win or cs.round_end.ct_win
    )
end

plugin:command("css_koth_set", {
    description = "把当前位置设为山丘中心",
    permission = "@css/root",
    allow_console = false
}, function(player, command)
    if not player.is_alive or player.position == nil then
        command:reply("请以存活玩家身份站在目标中心。")
        return
    end
    center = { x = player.position.x, y = player.position.y, z = player.position.z }
    scores = { t = 0, ct = 0 }
    finished = false
    cs.server.print_chat_all(string.format("%s[山丘之王] 占领区已设置，半径 %.0f。", cs.colors.green, config.radius))
end)

plugin:command("css_koth", {
    description = "查看山丘之王状态",
    allow_console = true
}, function(_, command)
    if center == nil then
        command:reply("当前地图尚未设置占领区。")
    else
        command:reply(string.format("T %d : %d CT，目标 %d。", scores.t, scores.ct, config.score_to_win))
    end
end)

plugin:every(config.score_interval, function()
    if center == nil or finished or not round_active then return end
    local inside = occupants()
    if #inside.t > 0 and #inside.ct == 0 then
        scores.t = scores.t + 1
        if scores.t >= config.score_to_win then finish("t") end
    elseif #inside.ct > 0 and #inside.t == 0 then
        scores.ct = scores.ct + 1
        if scores.ct >= config.score_to_win then finish("ct") end
    end

    local status = state_text(inside)
    local html = string.format(
        "<font color='#f0c040'>山丘之王</font><br>T %d : %d CT<br><font color='#eeeeee'>%s</font>",
        scores.t,
        scores.ct,
        status
    )
    for _, player in ipairs(cs.players.humans()) do player:print_html(html, 1) end
end)

plugin:on("round_start", function()
    scores = { t = 0, ct = 0 }
    finished = false
    round_active = true
    if center ~= nil then
        cs.server.print_chat_all(cs.colors.green .. "[山丘之王] 占领区已激活，双方开始争夺！")
    end
    return cs.continue
end)

plugin:on("round_end", function()
    round_active = false
    return cs.continue
end)

plugin:listen("OnMapStart", function()
    center = nil
    scores = { t = 0, ct = 0 }
    finished = false
    round_active = false
end)
