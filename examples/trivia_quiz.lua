local plugin = cs.plugin({
    name = "战场知识问答",
    version = "0.0.1",
    description = "内置中文题库、限时作答、连对奖励和本场积分榜"
})

local config = {
    questions_per_match = 6,
    answer_seconds = 12,
    between_questions = 2,
    correct_money = 300,
    streak_bonus = 200,
    money_cap = 16000
}

local bank = {
    { q = "经典拆弹模式中，反恐精英的英文缩写是什么？", answers = { "ct" } },
    { q = "AK-47 属于步枪还是冲锋枪？", answers = { "步枪", "rifle" } },
    { q = "通常一局竞技比赛每队最多几名首发玩家？", answers = { "5", "五", "5名" } },
    { q = "拆弹器通常由哪个阵营购买？", answers = { "ct", "反恐精英", "警察" } },
    { q = "AWP 开镜时移动会提高还是降低精准度？", answers = { "降低", "变低", "lower" } },
    { q = "高爆手雷的常见英文简称是什么？", answers = { "he", "hegrenade", "he grenade" } },
    { q = "炸弹成功爆炸通常判定哪个阵营胜利？", answers = { "t", "恐怖分子", "匪" } },
    { q = "按住静步键移动主要会减少什么？", answers = { "脚步声", "声音", "footstep", "footsteps" } },
    { q = "沙漠之鹰常见简称是什么？", answers = { "deagle", "沙鹰" } },
    { q = "烟雾弹英文单词是什么？", answers = { "smoke", "smokegrenade" } }
}

local quiz = nil
local generation = 0

local function normalize(text)
    return tostring(text or ""):lower():gsub("^%s+", ""):gsub("%s+$", ""):gsub("%s+", " ")
end

local function is_correct(question, answer)
    answer = normalize(answer)
    for _, expected in ipairs(question.answers) do
        if answer == normalize(expected) then return true end
    end
    return false
end

local function broadcast(message)
    cs.server.print_chat_all(cs.colors.green .. "[知识问答] " .. cs.colors.default .. message)
end

local finish_quiz
local ask_next

finish_quiz = function()
    if quiz == nil then return end
    local rows = {}
    for steam_id, score in pairs(quiz.scores) do
        local player = cs.players.get_steamid(steam_id)
        rows[#rows + 1] = { name = player and player.name or steam_id, score = score }
    end
    table.sort(rows, function(a, b) return a.score > b.score end)
    if #rows == 0 then
        broadcast("问答结束，本场无人得分。")
    else
        local result = {}
        for index = 1, math.min(3, #rows) do
            result[#result + 1] = string.format("#%d %s(%d)", index, rows[index].name, rows[index].score)
        end
        broadcast("问答结束：" .. table.concat(result, "，"))
    end
    quiz = nil
end

ask_next = function(expected_generation)
    if quiz == nil or generation ~= expected_generation then return end
    if quiz.index >= #quiz.questions then
        finish_quiz()
        return
    end
    quiz.index = quiz.index + 1
    quiz.answered = {}
    quiz.token = quiz.token + 1
    local token = quiz.token
    local question = quiz.questions[quiz.index]
    broadcast(string.format("第 %d/%d 题：%s（用 !answer 回答）",
        quiz.index, #quiz.questions, question.q))

    plugin:after(config.answer_seconds, function()
        if quiz == nil or quiz.token ~= token then return end
        broadcast("时间到，参考答案：" .. question.answers[1])
        plugin:after(config.between_questions, function() ask_next(expected_generation) end)
    end)
end

plugin:command("css_quiz_start", {
    description = "开始一场战场知识问答",
    permission = "@css/generic",
    allow_console = true
}, function(_, command)
    if quiz ~= nil then
        command:reply("已经有问答正在进行。")
        return
    end
    local pool = {}
    for index = 1, #bank do pool[index] = bank[index] end
    for index = #pool, 2, -1 do
        local other = math.random(index)
        pool[index], pool[other] = pool[other], pool[index]
    end
    local selected = {}
    for index = 1, math.min(config.questions_per_match, #pool) do selected[index] = pool[index] end

    generation = generation + 1
    quiz = { questions = selected, index = 0, token = 0, answered = {}, scores = {}, streaks = {} }
    broadcast("问答开始，每题每人只能回答一次，连续答对有额外金钱。")
    ask_next(generation)
end)

plugin:command("css_answer", {
    description = "回答当前知识题",
    allow_console = false,
    min_args = 1,
    usage = "<答案>"
}, function(player, command)
    if quiz == nil or quiz.index < 1 then
        command:reply("当前没有题目。")
        return
    end
    local steam_id = player.steam_id
    if quiz.answered[steam_id] then
        command:reply("这一题你已经回答过了。")
        return
    end
    quiz.answered[steam_id] = true
    local question = quiz.questions[quiz.index]
    if not is_correct(question, table.concat(command.args, " ")) then
        quiz.streaks[steam_id] = 0
        command:reply("回答错误，等待下一题。")
        return
    end

    local streak = (quiz.streaks[steam_id] or 0) + 1
    quiz.streaks[steam_id] = streak
    quiz.scores[steam_id] = (quiz.scores[steam_id] or 0) + 1
    local reward = config.correct_money + math.max(0, streak - 1) * config.streak_bonus
    if player.money ~= nil then player:set_money(math.min(config.money_cap, player.money + reward)) end
    broadcast(string.format("%s 回答正确，连续 %d 题，获得 $%d！", player.name, streak, reward))
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    quiz = nil
end)

plugin:on_unload(function()
    generation = generation + 1
    quiz = nil
end)
