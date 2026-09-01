local plugin = cs.plugin({
    name = "地图投票台",
    version = "0.0.1",
    description = "候选地图菜单、一人一票、倒计时和平票随机裁决"
})

local config = {
    duration = 30,
    max_candidates = 6,
    change_delay = 5,
    minimum_votes = 1
}

local vote = nil
local generation = 0

local function broadcast(message)
    cs.server.print_chat_all(cs.colors.green .. "[地图投票] " .. cs.colors.default .. message)
end

local function finish_vote(expected_generation)
    if vote == nil or generation ~= expected_generation then return end

    local highest = 0
    local winners = {}
    for index, count in ipairs(vote.counts) do
        if count > highest then
            highest = count
            winners = { index }
        elseif count == highest then
            winners[#winners + 1] = index
        end
    end

    local finished = vote
    vote = nil
    if highest < config.minimum_votes or #winners == 0 then
        broadcast("有效票数不足，本次不切换地图。")
        return
    end

    local winner_index = winners[math.random(#winners)]
    local map_name = finished.maps[winner_index]
    if not cs.server.is_map_valid(map_name) then
        broadcast("获胜地图已经无效，已取消切换。")
        return
    end

    broadcast(string.format("%s 以 %d 票胜出，%d 秒后切换。", map_name, highest, config.change_delay))
    plugin:after(config.change_delay, function()
        -- 候选来自 maplist 且再次经过 is_map_valid，不拼接玩家的任意输入。
        if cs.server.is_map_valid(map_name) then cs.server.execute("changelevel " .. map_name) end
    end)
end

local function open_ballot(player)
    if vote == nil then
        player:print_chat("当前没有正在进行的地图投票。")
        return
    end

    local items = {}
    for index, map_name in ipairs(vote.maps) do
        items[index] = { text = string.format("%s（%d 票）", map_name, vote.counts[index]) }
    end

    cs.menu.open(player, {
        title = "下一张地图",
        type = "chat",
        post_select = "close",
        items = items
    }, function(current, index)
        if vote == nil or vote.maps[index] == nil then return end
        local steam_id = current.steam_id
        if vote.voters[steam_id] ~= nil then
            current:print_chat(cs.colors.red .. "你已经投过票了。")
            return
        end
        vote.voters[steam_id] = index
        vote.counts[index] = vote.counts[index] + 1
        broadcast(current.name .. " 投给了 " .. vote.maps[index] .. "。")
    end)
end

local function start_vote(command)
    if vote ~= nil then
        command:reply("已经有一场地图投票正在进行。")
        return
    end

    local current_map = cs.server.info().map_name
    local candidates = {}
    for _, map_name in ipairs(cs.server.maps()) do
        if map_name ~= current_map and cs.server.is_map_valid(map_name) then
            candidates[#candidates + 1] = map_name
            if #candidates >= config.max_candidates then break end
        end
    end
    if #candidates < 2 then
        command:reply("maplist.txt 中至少需要两张其他有效地图。")
        return
    end

    generation = generation + 1
    vote = { maps = candidates, counts = {}, voters = {} }
    for index = 1, #candidates do vote.counts[index] = 0 end
    broadcast("投票开始，输入 !mapvote 打开菜单，持续 " .. config.duration .. " 秒。")
    local expected_generation = generation
    plugin:after(config.duration, function() finish_vote(expected_generation) end)
end

plugin:command("css_startmapvote", {
    description = "发起下一张地图投票",
    permission = "@css/generic",
    allow_console = true
}, function(_, command)
    start_vote(command)
end)

plugin:command("css_mapvote", {
    description = "打开当前地图投票菜单",
    allow_console = false
}, function(player)
    open_ballot(player)
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    vote = nil
end)

plugin:on_unload(function()
    generation = generation + 1
    vote = nil
end)
