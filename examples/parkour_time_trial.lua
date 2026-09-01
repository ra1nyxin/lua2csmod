local plugin = cs.plugin({
    name = "跑酷竞速计时",
    version = "0.0.1",
    description = "管理员现场设置赛道，支持起跑校验、个人最佳和排行榜"
})

local config = {
    trigger_radius = 110,
    max_run_seconds = 600,
    scan_interval = 0.1
}

local course = { start = nil, finish = nil }
local runs = {}

local function distance(a, b)
    local dx, dy, dz = a.x - b.x, a.y - b.y, a.z - b.z
    return math.sqrt(dx * dx + dy * dy + dz * dz)
end

local function now()
    return cs.server.info().engine_time
end

local function best_key(steam_id)
    return "best:" .. cs.server.info().map_name .. ":" .. steam_id
end

local function set_point(player, name, command)
    if player == nil or not player.is_alive or player.position == nil then
        command:reply("请由存活的管理员在目标位置执行。")
        return
    end
    course[name] = { x = player.position.x, y = player.position.y, z = player.position.z }
    runs = {}
    command:reply(name == "start" and "起点已设置。" or "终点已设置。")
end

plugin:command("css_parkour_startpoint", {
    description = "把当前位置设为跑酷起点",
    permission = "@css/root",
    allow_console = false
}, function(player, command)
    set_point(player, "start", command)
end)

plugin:command("css_parkour_finishpoint", {
    description = "把当前位置设为跑酷终点",
    permission = "@css/root",
    allow_console = false
}, function(player, command)
    set_point(player, "finish", command)
end)

plugin:command("css_parkour", {
    description = "从起点开始一次跑酷计时",
    allow_console = false
}, function(player, command)
    if course.start == nil or course.finish == nil then
        command:reply("管理员尚未设置完整赛道。")
        return
    end
    if not player.is_alive or player.position == nil then
        command:reply("只有存活玩家可以起跑。")
        return
    end
    if distance(player.position, course.start) > config.trigger_radius then
        command:reply("请先站到起点附近。")
        return
    end

    runs[player.steam_id] = { started_at = now(), name = player.name }
    player:print_center("计时开始！")
end)

plugin:command("css_parkour_top", {
    description = "查看当前地图跑酷排行榜",
    allow_console = true
}, function(_, command)
    local prefix = "best:" .. cs.server.info().map_name .. ":"
    local rows = {}
    for key, value in pairs(cs.storage.all()) do
        if key:sub(1, #prefix) == prefix and type(value) == "string" then
            local seconds, name = value:match("^([%d%.]+)|(.+)$")
            if seconds then rows[#rows + 1] = { seconds = tonumber(seconds), name = name } end
        end
    end
    table.sort(rows, function(a, b) return a.seconds < b.seconds end)
    if #rows == 0 then
        command:reply("当前地图还没有完赛记录。")
        return
    end
    for index = 1, math.min(5, #rows) do
        command:reply(string.format("#%d %s - %.3f 秒", index, rows[index].name, rows[index].seconds))
    end
end)

plugin:every(config.scan_interval, function()
    if course.finish == nil then return end
    local timestamp = now()
    for steam_id, run in pairs(runs) do
        local player = cs.players.get_steamid(steam_id)
        if player == nil or not player.is_alive or player.position == nil then
            runs[steam_id] = nil
        elseif timestamp - run.started_at > config.max_run_seconds then
            runs[steam_id] = nil
            player:print_chat("跑酷计时已超时。")
        elseif distance(player.position, course.finish) <= config.trigger_radius then
            local elapsed = timestamp - run.started_at
            runs[steam_id] = nil
            local key = best_key(steam_id)
            local old = cs.storage.get(key)
            local old_seconds = old and tonumber(old:match("^([%d%.]+)|")) or nil
            if old_seconds == nil or elapsed < old_seconds then
                cs.storage.set(key, string.format("%.3f|%s", elapsed, player.name))
                cs.server.print_chat_all(string.format("%s%s 刷新跑酷纪录：%.3f 秒！", cs.colors.gold, player.name, elapsed))
            else
                player:print_chat(string.format("完赛：%.3f 秒；个人最佳：%.3f 秒。", elapsed, old_seconds))
            end
        end
    end
end)

plugin:on("player_death", function(event)
    if event.player ~= nil then runs[event.player.steam_id] = nil end
    return cs.continue
end)

plugin:listen("OnMapStart", function()
    course = { start = nil, finish = nil }
    runs = {}
end)
