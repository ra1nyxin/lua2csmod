local plugin = cs.plugin({
    name = "俄罗斯轮盘",
    version = "0.0.1",
    description = "玩家报名后轮流扣动扳机，空枪继续，命中者淘汰"
})

local config = {
    turn_seconds = 12,
    start_permission = "@css/generic"
}

local joined = {}
local game = nil
local generation = 0

local function broadcast(message)
    cs.server.print_chat_all(cs.colors.gold .. "[俄罗斯轮盘] " .. cs.colors.default .. message)
end

local function valid_player(steam_id)
    local player = cs.players.get_steamid(steam_id)
    if player == nil or player.is_bot or player.is_hltv or not player.is_alive then return nil end
    return player
end

local function compact_players()
    local result = {}
    for _, steam_id in ipairs(game.players) do
        if valid_player(steam_id) ~= nil then result[#result + 1] = steam_id end
    end
    game.players = result
    if game.index > #result then game.index = 1 end
end

local begin_turn

local function resolve_trigger(steam_id, expected_turn)
    if game == nil or game.turn ~= expected_turn then return end
    if game.players[game.index] ~= steam_id then return end
    local player = valid_player(steam_id)
    if player == nil then
        compact_players()
        begin_turn()
        return
    end

    if game.chamber == game.live_chamber then
        broadcast(player.name .. " 扣动扳机……砰！")
        table.remove(game.players, game.index)
        player:kill(false, true)
        game.chamber = 1
        game.live_chamber = math.random(1, 6)
    else
        broadcast(player.name .. " 扣动扳机……咔哒，空枪！")
        game.chamber = game.chamber + 1
        game.index = game.index + 1
    end
    begin_turn()
end

begin_turn = function()
    if game == nil then return end
    compact_players()
    if #game.players <= 1 then
        local winner = #game.players == 1 and valid_player(game.players[1]) or nil
        broadcast(winner and (winner.name .. " 活到最后，赢得轮盘！") or "无人幸存。")
        game = nil
        return
    end
    if game.index > #game.players then game.index = 1 end

    game.turn = game.turn + 1
    local turn = game.turn
    local player = valid_player(game.players[game.index])
    if player == nil then
        compact_players()
        begin_turn()
        return
    end
    broadcast(player.name .. " 的回合：输入 !trigger，限时 " .. config.turn_seconds .. " 秒。")
    player:print_alert("轮到你了：!trigger")
    local steam_id = player.steam_id
    plugin:after(config.turn_seconds, function()
        -- 超时自动扣动，turn 代次可使上一回合的旧定时器失效。
        resolve_trigger(steam_id, turn)
    end)
end

plugin:command("css_rrjoin", {
    description = "报名俄罗斯轮盘",
    allow_console = false
}, function(player, command)
    if game ~= nil then
        command:reply("轮盘已经开始，请等待下一局。")
        return
    end
    if not player.is_alive then
        command:reply("只有存活玩家可以报名。")
        return
    end
    joined[player.steam_id] = player.name
    command:reply("报名成功，等待管理员输入 !rrstart。")
end)

plugin:command("css_rrstart", {
    description = "开始俄罗斯轮盘",
    permission = config.start_permission,
    allow_console = true
}, function(_, command)
    if game ~= nil then
        command:reply("轮盘已经开始。")
        return
    end
    local players = {}
    for steam_id, _ in pairs(joined) do
        if valid_player(steam_id) ~= nil then players[#players + 1] = steam_id end
    end
    if #players < 2 then
        command:reply("至少需要两名已报名的存活玩家。")
        return
    end
    generation = generation + 1
    game = { players = players, index = 1, chamber = 1, live_chamber = math.random(1, 6), turn = 0 }
    joined = {}
    broadcast("轮盘开始，共 " .. #players .. " 名玩家。")
    begin_turn()
end)

plugin:command("css_trigger", {
    description = "在自己的轮盘回合扣动扳机",
    allow_console = false
}, function(player, command)
    if game == nil or game.players[game.index] ~= player.steam_id then
        command:reply("现在还没轮到你。")
        return
    end
    resolve_trigger(player.steam_id, game.turn)
end)

plugin:on("round_end", function()
    generation = generation + 1
    joined = {}
    game = nil
    return cs.continue
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    joined = {}
    game = nil
end)
