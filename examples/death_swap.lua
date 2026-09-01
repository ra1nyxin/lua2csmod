local plugin = cs.plugin({
    name = "死亡换位",
    version = "0.0.1",
    description = "随机配对存活玩家，周期性交换完整位置、视角和速度"
})

local config = {
    swap_seconds = 25,
    countdown_from = 5,
    minimum_players = 2
}

local game = nil

local function now()
    return cs.server.info().engine_time
end

local function alive_humans()
    local result = {}
    for _, player in ipairs(cs.players.humans()) do
        if not player.is_hltv and player.is_alive and player.position ~= nil then
            result[#result + 1] = player
        end
    end
    return result
end

local function shuffle(values)
    for index = #values, 2, -1 do
        local other = math.random(index)
        values[index], values[other] = values[other], values[index]
    end
end

local function swap_pair(pair)
    local first = cs.players.get_steamid(pair[1])
    local second = cs.players.get_steamid(pair[2])
    if first == nil or second == nil or not first.is_alive or not second.is_alive
        or first.position == nil or second.position == nil then
        return false
    end

    -- 先复制两边快照，再依次传送；速度也互换，因此空中、坠落和地图外状态都会保留。
    local first_position = { x = first.position.x, y = first.position.y, z = first.position.z }
    local first_angles = first.eye_angles and {
        x = first.eye_angles.x, y = first.eye_angles.y, z = first.eye_angles.z
    } or nil
    local first_velocity = first.velocity and {
        x = first.velocity.x, y = first.velocity.y, z = first.velocity.z
    } or nil
    local second_position = { x = second.position.x, y = second.position.y, z = second.position.z }
    local second_angles = second.eye_angles and {
        x = second.eye_angles.x, y = second.eye_angles.y, z = second.eye_angles.z
    } or nil
    local second_velocity = second.velocity and {
        x = second.velocity.x, y = second.velocity.y, z = second.velocity.z
    } or nil

    local first_ok = first:teleport(second_position, second_angles, second_velocity)
    local second_ok = second:teleport(first_position, first_angles, first_velocity)
    if first_ok and second_ok then
        first:print_alert("已与 " .. second.name .. " 换位")
        second:print_alert("已与 " .. first.name .. " 换位")
        return true
    end
    cs.log.warn("死亡换位部分失败：" .. first.steam_id .. " <-> " .. second.steam_id)
    return false
end

local function perform_swap()
    if game == nil then return end
    local success = 0
    for _, pair in ipairs(game.pairs) do
        if swap_pair(pair) then success = success + 1 end
    end
    cs.server.print_chat_all(string.format("%s[死亡换位] 已交换 %d 组存活玩家！", cs.colors.red, success))
    game.next_swap = now() + config.swap_seconds
    game.last_announced = nil
end

plugin:command("css_deathswap_start", {
    description = "将所有存活玩家随机配对并开始死亡换位",
    permission = "@css/generic",
    allow_console = true
}, function(_, command)
    if game ~= nil then
        command:reply("死亡换位已经运行。")
        return
    end
    local players = alive_humans()
    if #players < config.minimum_players then
        command:reply("至少需要两名存活玩家。")
        return
    end
    shuffle(players)
    local pairs = {}
    local partners = {}
    for index = 1, #players - 1, 2 do
        local first, second = players[index], players[index + 1]
        pairs[#pairs + 1] = { first.steam_id, second.steam_id }
        partners[first.steam_id] = second.steam_id
        partners[second.steam_id] = first.steam_id
        first:print_chat("你的换位搭档是 " .. second.name .. "。")
        second:print_chat("你的换位搭档是 " .. first.name .. "。")
    end
    if #players % 2 == 1 then players[#players]:print_chat("人数为奇数，你本局轮空。") end
    game = { pairs = pairs, partners = partners, next_swap = now() + config.swap_seconds, last_announced = nil }
    cs.server.print_chat_all(cs.colors.gold .. "[死亡换位] 游戏开始，交换时不会检查落点安全！")
end)

plugin:command("css_deathswap", {
    description = "查看死亡换位搭档和倒计时",
    allow_console = false
}, function(player, command)
    if game == nil then
        command:reply("死亡换位尚未开始。")
        return
    end
    local partner_id = game.partners[player.steam_id]
    local partner = partner_id and cs.players.get_steamid(partner_id) or nil
    command:reply(string.format("搭档：%s，下次换位：%.0f 秒。",
        partner and partner.name or "无/已离线", math.max(0, game.next_swap - now())))
end)

plugin:every(0.5, function()
    if game == nil then return end
    local remaining = game.next_swap - now()
    if remaining <= 0 then
        perform_swap()
        return
    end
    local whole = math.ceil(remaining)
    if whole <= config.countdown_from and whole ~= game.last_announced then
        game.last_announced = whole
        for _, player in ipairs(cs.players.humans()) do player:print_center("死亡换位：" .. whole) end
    end
end)

plugin:on("round_end", function()
    game = nil
    return cs.continue
end)

plugin:listen("OnMapStart", function() game = nil end)
plugin:on_unload(function() game = nil end)
