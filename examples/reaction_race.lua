local plugin = cs.plugin({
    name = "极限反应赛",
    version = "0.0.1",
    description = "随机延迟后发出信号，玩家抢先输入命令得分，抢跑会扣分"
})

local config = {
    rounds = 5,
    minimum_delay = 2,
    maximum_delay = 7,
    answer_window = 3,
    between_rounds = 2
}

local match = nil
local generation = 0

local function broadcast(message)
    cs.server.print_chat_all(cs.colors.green .. "[反应赛] " .. cs.colors.default .. message)
end

local function score_of(steam_id)
    return match.scores[steam_id] or 0
end

local finish_match
local prepare_round

finish_match = function()
    if match == nil then return end
    local rows = {}
    for steam_id, score in pairs(match.scores) do
        local player = cs.players.get_steamid(steam_id)
        rows[#rows + 1] = { name = player and player.name or steam_id, score = score }
    end
    table.sort(rows, function(a, b)
        if a.score == b.score then return a.name < b.name end
        return a.score > b.score
    end)
    if #rows == 0 then
        broadcast("比赛结束，本场无人得分。")
    else
        local parts = {}
        for index = 1, math.min(3, #rows) do
            parts[#parts + 1] = string.format("#%d %s(%d)", index, rows[index].name, rows[index].score)
        end
        broadcast("比赛结束：" .. table.concat(parts, "，"))
    end
    match = nil
end

prepare_round = function(expected_generation)
    if match == nil or generation ~= expected_generation then return end
    if match.round >= config.rounds then
        finish_match()
        return
    end
    match.round = match.round + 1
    match.phase = "waiting"
    match.token = match.token + 1
    local token = match.token
    broadcast(string.format("第 %d/%d 轮准备……看到“现在”再输入 !react。", match.round, config.rounds))

    local delay = math.random(config.minimum_delay * 10, config.maximum_delay * 10) / 10
    plugin:after(delay, function()
        if match == nil or generation ~= expected_generation or match.token ~= token then return end
        match.phase = "go"
        cs.server.print_chat_all(cs.colors.red .. "[反应赛] 现在！输入 !react！")
        for _, player in ipairs(cs.players.humans()) do player:print_alert("现在！!react") end

        plugin:after(config.answer_window, function()
            if match == nil or match.token ~= token or match.phase ~= "go" then return end
            match.phase = "between"
            broadcast("本轮无人及时抢到。")
            plugin:after(config.between_rounds, function() prepare_round(expected_generation) end)
        end)
    end)
end

plugin:command("css_reaction_start", {
    description = "开始一场极限反应赛",
    permission = "@css/generic",
    allow_console = true
}, function(_, command)
    if match ~= nil then
        command:reply("已经有反应赛正在进行。")
        return
    end
    generation = generation + 1
    match = { round = 0, phase = "between", token = 0, scores = {}, false_starts = {} }
    broadcast("比赛开始，共 " .. config.rounds .. " 轮，所有真实玩家均可参加。")
    prepare_round(generation)
end)

plugin:command("css_react", {
    description = "抢答当前反应信号",
    allow_console = false
}, function(player, command)
    if player.is_bot or player.is_hltv or match == nil then
        command:reply("当前没有可参加的反应赛。")
        return
    end
    local steam_id = player.steam_id
    if match.phase == "waiting" then
        -- 每轮只处罚一次抢跑，避免刷命令造成无限负分。
        if match.false_starts[steam_id] ~= match.round then
            match.false_starts[steam_id] = match.round
            match.scores[steam_id] = score_of(steam_id) - 1
            command:reply("抢跑，扣 1 分！")
        end
        return
    end
    if match.phase ~= "go" then
        command:reply("现在不是抢答阶段。")
        return
    end

    match.phase = "between"
    match.scores[steam_id] = score_of(steam_id) + 1
    broadcast(string.format("%s 抢到第 %d 轮，当前 %d 分！", player.name, match.round, match.scores[steam_id]))
    local expected_generation = generation
    plugin:after(config.between_rounds, function() prepare_round(expected_generation) end)
end)

plugin:command("css_reaction_score", {
    description = "查看自己的反应赛积分",
    allow_console = false
}, function(player, command)
    command:reply("当前反应赛积分：" .. (match and score_of(player.steam_id) or 0))
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    match = nil
end)

plugin:on_unload(function()
    generation = generation + 1
    match = nil
end)
