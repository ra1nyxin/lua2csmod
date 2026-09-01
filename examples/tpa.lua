-- TPA 玩家传送请求示例（所有指令只能由玩家执行）：
-- !tpa <查询>：通用匹配并请求传送到对方；支持 slot、userid、SteamID64 和名字片段。
-- !tpaid / !tpaslot / !tpaname：分别强制按 userid、slot 或名字片段请求传送。
-- !tpahere <查询>：通用匹配并邀请对方传送到自己身边。
-- !tpahereid / !tpahereslot / !tpaherename：分别强制反向请求的匹配类型。
-- !tpalist：列出全部玩家的 userid、slot，以及自己、Bot、HLTV 标记。
-- !tpaccept [玩家] / !tpdeny [玩家]：接受或拒绝请求；省略玩家时处理最新请求。
-- !tpcancel：取消自己发出的请求。
-- 名字匹配忽略大小写并使用普通子串；多人匹配时必须输入更完整的名字。
-- 请求 30 秒后过期；每名请求者同时只能保留一个请求，接收者可收到多个请求。
-- 接受后，移动者会随机落在对方水平 48 单位外，并清除原有移动速度。
-- 双方必须在线且存活；自己、Bot 和 HLTV 不能成为目标。
-- 本示例不限制阵营、回合、冻结时间、战斗状态、空中状态或地图边界。
-- 任一方离服时，相关请求都会自动取消并通知仍在线的一方。

local plugin = cs.plugin({
    name = "玩家传送请求",
    version = "0.3.5",
    description = "提供不限制阵营和回合状态的宽松玩家传送请求"
})

local request_timeout = 30
local teleport_offset = 48
local next_request_id = 0

local styles = {
    info = { color = cs.colors.blue, marker = "[i]" },
    success = { color = cs.colors.green, marker = "[+]" },
    warning = { color = cs.colors.yellow, marker = "[!]" },
    error = { color = cs.colors.red, marker = "[x]" }
}

local function styled(kind, message)
    local style = assert(styles[kind], "未知的 TPA 消息样式：" .. tostring(kind))
    return cs.colors.blue .. "[TPA] "
        .. style.color .. style.marker .. " "
        .. cs.colors.white .. message
        .. cs.colors.default
end

local function reply(command, kind, message)
    command:reply(styled(kind, message))
end

-- 请求全部以 SteamID64 关联，不能用 slot 保存长期身份：玩家离服后 slot 会被复用。
-- outgoing 的键是请求者，incoming 的第一层键是接收者，方便从两个方向查找和清理。
local outgoing = {}
local incoming = {}

local function now()
    return cs.server.info().ticked_time
end

local function remove_request(request)
    if outgoing[request.sender_id] == request then
        outgoing[request.sender_id] = nil
    end

    local requests = incoming[request.target_id]
    if requests ~= nil then
        requests[request.sender_id] = nil
        if next(requests) == nil then incoming[request.target_id] = nil end
    end
end

local function notify_online(steam_id, kind, message)
    local player = cs.players.get_steamid(steam_id)
    if player ~= nil then player:print_chat(styled(kind, message)) end
end

local function random_nearby_position(position)
    local angle = math.random() * math.pi * 2
    return cs.vec3(
        position.x + math.cos(angle) * teleport_offset,
        position.y + math.sin(angle) * teleport_offset,
        position.z
    )
end

local function request_description(request)
    if request.mode == "here" then
        return request.sender_name .. " 邀请 " .. request.target_name .. " 传送到其身边的请求"
    end
    return request.sender_name .. " 请求传送到 " .. request.target_name .. " 身边的请求"
end

local function expire_request(request)
    -- 玩家可能取消请求后又发送了新请求，因此定时器必须确认自己仍对应当前请求。
    if outgoing[request.sender_id] ~= request then return end

    remove_request(request)
    local description = request_description(request)
    notify_online(request.sender_id, "warning", description .. "已过期。")
    notify_online(request.target_id, "warning", description .. "已过期。")
end

local function select_one_player(matches, query, command)
    if #matches == 0 then
        reply(command, "error", "没有找到玩家：" .. query)
        return nil
    end
    if #matches > 1 then
        reply(command, "error", "匹配到多名玩家，请输入更完整的名字、槽位、userid 或 SteamID64。")
        return nil
    end
    return matches[1]
end

local function command_query(command)
    return table.concat(command.args, " ")
end

local function find_one_player(query, command)
    return select_one_player(cs.players.find(query), query, command)
end

local function find_one_player_by_name(query, command)
    query = query:match("^%s*(.-)%s*$")
    if query == "" then
        reply(command, "error", "玩家名不能为空。")
        return nil
    end

    local matches = {}
    local lowered_query = query:lower()
    for _, candidate in ipairs(cs.players.all()) do
        if candidate.name:lower():find(lowered_query, 1, true) ~= nil then
            matches[#matches + 1] = candidate
        end
    end
    return select_one_player(matches, query, command)
end

local function parse_non_negative_integer(value, label, command)
    if value == nil or value:match("^%d+$") == nil then
        reply(command, "error", label .. " 必须是非负整数。")
        return nil
    end

    local number = tonumber(value)
    if number == nil or math.type(number) ~= "integer" or number < 0 then
        reply(command, "error", label .. " 必须是有效的非负整数。")
        return nil
    end
    return number
end

local function find_one_player_by_userid(value, command)
    local user_id = parse_non_negative_integer(value, "userid", command)
    if user_id == nil then return nil end

    local target = cs.players.get_userid(user_id)
    if target == nil then reply(command, "error", "没有找到 userid 为 " .. user_id .. " 的玩家。") end
    return target
end

local function find_one_player_by_slot(value, command)
    local slot = parse_non_negative_integer(value, "slot", command)
    if slot == nil then return nil end

    local target = cs.players.get(slot)
    if target == nil then reply(command, "error", "没有找到 slot 为 " .. slot .. " 的玩家。") end
    return target
end

local function validate_target(player, target, command)
    if target == nil then return nil end
    if target.steam_id == player.steam_id then
        reply(command, "error", "不能向自己发送传送请求。")
        return nil
    end
    if target.is_bot or target.is_hltv then
        reply(command, "error", "不能向机器人或 HLTV 发送传送请求。")
        return nil
    end
    return target
end

local function current_requests(target_id)
    local result = {}
    local requests = incoming[target_id]
    if requests == nil then return result end

    for _, request in pairs(requests) do
        if request.expires_at <= now() then
            expire_request(request)
        else
            result[#result + 1] = request
        end
    end
    return result
end

local function select_request(player, query, command)
    local requests = current_requests(player.steam_id)
    if #requests == 0 then
        reply(command, "warning", "当前没有等待处理的传送请求。")
        return nil
    end

    if query == nil then
        -- 与常见 TPA 体验一致：不写玩家名时直接处理最新收到的有效请求。
        local latest = requests[1]
        for index = 2, #requests do
            if requests[index].id > latest.id then latest = requests[index] end
        end
        return latest
    end

    local sender = find_one_player(query, command)
    if sender == nil then return nil end

    local request = outgoing[sender.steam_id]
    if request == nil or request.target_id ~= player.steam_id then
        reply(command, "warning", sender.name .. " 没有向你发送传送请求。")
        return nil
    end
    return request
end

local function create_request(player, target, mode, command)
    target = validate_target(player, target, command)
    if target == nil then return end
    local old_request = outgoing[player.steam_id]
    if old_request ~= nil then
        if old_request.expires_at <= now() then
            expire_request(old_request)
        else
            reply(command, "warning", "你已经向 " .. old_request.target_name .. " 发送过请求，请等待处理或使用 css_tpcancel 取消。")
            return
        end
    end

    next_request_id = next_request_id + 1
    local request = {
        id = next_request_id,
        sender_id = player.steam_id,
        sender_slot = player.slot,
        sender_name = player.name,
        target_id = target.steam_id,
        target_slot = target.slot,
        target_name = target.name,
        mode = mode,
        expires_at = now() + request_timeout
    }
    outgoing[request.sender_id] = request
    incoming[request.target_id] = incoming[request.target_id] or {}
    incoming[request.target_id][request.sender_id] = request

    if mode == "here" then
        reply(command, "success", "已邀请 " .. target.name .. " 传送到你身边，" .. request_timeout .. " 秒后过期。")
        target:print_chat(styled("info", player.name .. " 邀请你传送到其身边。输入 !tpaccept 接受，或 !tpdeny 拒绝。"))
    else
        reply(command, "success", "已向 " .. target.name .. " 发送传送请求，" .. request_timeout .. " 秒后过期。")
        target:print_chat(styled("info", player.name .. " 请求传送到你身边。输入 !tpaccept 接受，或 !tpdeny 拒绝。"))
    end

    plugin:after(request_timeout, function()
        -- id 检查让旧定时器无法误删同一玩家后来创建的新请求。
        local current = outgoing[request.sender_id]
        if current ~= nil and current.id == request.id then expire_request(current) end
    end, { stop_on_map_change = false })
end

local function register_request_command(name, description, usage, mode, resolver)
    plugin:command(name, {
        description = description,
        allow_console = false,
        min_args = 1,
        usage = usage
    }, function(player, command)
        create_request(player, resolver(command), mode, command)
    end)
end

plugin:command("css_tpalist", {
    description = "列出玩家的 userid 和 slot",
    allow_console = false
}, function(player, command)
    local players = cs.players.all()
    table.sort(players, function(left, right)
        if left.user_id == right.user_id then return left.slot < right.slot end
        return left.user_id < right.user_id
    end)

    reply(command, "info", "TPA 玩家列表：")
    for _, candidate in ipairs(players) do
        local flags = {}
        if candidate.steam_id == player.steam_id then flags[#flags + 1] = "自己" end
        if candidate.is_bot then flags[#flags + 1] = "BOT" end
        if candidate.is_hltv then flags[#flags + 1] = "HLTV" end
        local suffix = #flags > 0 and " [" .. table.concat(flags, "/") .. "]" or ""
        reply(command, "info", "[userid: " .. candidate.user_id .. " | slot: " .. candidate.slot .. "] " .. candidate.name .. suffix)
    end
end)

register_request_command("css_tpa", "请求传送到另一名玩家身边", "<查询>", "to_target", function(command)
    return find_one_player(command_query(command), command)
end)
register_request_command("css_tpaid", "按 userid 请求传送", "<userid>", "to_target", function(command)
    return find_one_player_by_userid(command.args[1], command)
end)
register_request_command("css_tpaslot", "按 slot 请求传送", "<slot>", "to_target", function(command)
    return find_one_player_by_slot(command.args[1], command)
end)
register_request_command("css_tpaname", "按名字片段请求传送", "<名字>", "to_target", function(command)
    return find_one_player_by_name(command_query(command), command)
end)
register_request_command("css_tpahere", "邀请另一名玩家传送到自己身边", "<查询>", "here", function(command)
    return find_one_player(command_query(command), command)
end)
register_request_command("css_tpahereid", "按 userid 邀请玩家传送到自己身边", "<userid>", "here", function(command)
    return find_one_player_by_userid(command.args[1], command)
end)
register_request_command("css_tpahereslot", "按 slot 邀请玩家传送到自己身边", "<slot>", "here", function(command)
    return find_one_player_by_slot(command.args[1], command)
end)
register_request_command("css_tpaherename", "按名字片段邀请玩家传送到自己身边", "<名字>", "here", function(command)
    return find_one_player_by_name(command_query(command), command)
end)

plugin:command("css_tpaccept", {
    description = "接受一名玩家的传送请求",
    allow_console = false,
    usage = "[玩家]"
}, function(player, command)
    local query = #command.args > 0 and command_query(command) or nil
    local request = select_request(player, query, command)
    if request == nil then return end

    -- 重新按 SteamID64 获取快照，确保请求期间没有离服或发生 slot 身份变化。
    local sender = cs.players.get_steamid(request.sender_id)
    local target = player:refresh()
    if sender == nil then
        remove_request(request)
        reply(command, "warning", request.sender_name .. " 已经离开服务器，请求已取消。")
        return
    end
    if target == nil or target.steam_id ~= request.target_id then
        return
    end
    if not sender.is_alive then
        reply(command, "warning", sender.name .. " 当前未存活，暂时无法传送。")
        return
    end
    if not target.is_alive then
        reply(command, "warning", "你当前未存活，暂时无法接受传送。")
        return
    end

    -- 这是娱乐服的宽松传送：不检查阵营、回合/冻结状态、战斗状态、
    -- 导航网格、目标是否在空中或坐标是否处于地图边界内，只要求双方仍有有效位置。
    -- 普通请求移动发送者；here 请求移动接受者。清零速度可避免带入原有动量。
    local mover = sender
    local anchor = target
    if request.mode == "here" then
        mover = target
        anchor = sender
    end
    if anchor.position == nil then
        reply(command, "error", "目标玩家的位置无效，暂时无法传送。")
        return
    end

    local destination = random_nearby_position(anchor.position)
    if not mover:teleport(destination, anchor.eye_angles, cs.vec3(0, 0, 0)) then
        reply(command, "error", "传送失败，请稍后重试。")
        return
    end

    remove_request(request)
    if request.mode == "here" then
        reply(command, "success", "已接受 " .. sender.name .. " 的邀请，已传送到其身边。")
        sender:print_chat(styled("success", target.name .. " 已接受邀请并传送到你身边。"))
    else
        reply(command, "success", "已接受 " .. sender.name .. " 的传送请求。")
        sender:print_chat(styled("success", "传送请求已被 " .. target.name .. " 接受。"))
    end
end)

plugin:command("css_tpdeny", {
    description = "拒绝一名玩家的传送请求",
    allow_console = false,
    usage = "[玩家]"
}, function(player, command)
    local query = #command.args > 0 and command_query(command) or nil
    local request = select_request(player, query, command)
    if request == nil then return end

    remove_request(request)
    if request.mode == "here" then
        reply(command, "warning", "已拒绝 " .. request.sender_name .. " 的传送邀请。")
        notify_online(request.sender_id, "warning", request.target_name .. " 拒绝了你的传送邀请。")
    else
        reply(command, "warning", "已拒绝 " .. request.sender_name .. " 的传送请求。")
        notify_online(request.sender_id, "warning", request.target_name .. " 拒绝了你的传送请求。")
    end
end)

plugin:command("css_tpcancel", {
    description = "取消自己发出的传送请求",
    allow_console = false
}, function(player, command)
    local request = outgoing[player.steam_id]
    if request == nil then
        reply(command, "warning", "你当前没有等待处理的传送请求。")
        return
    end

    remove_request(request)
    if request.mode == "here" then
        reply(command, "warning", "已取消对 " .. request.target_name .. " 的传送邀请。")
        notify_online(request.target_id, "warning", request.sender_name .. " 取消了传送邀请。")
    else
        reply(command, "warning", "已取消向 " .. request.target_name .. " 发送的传送请求。")
        notify_online(request.target_id, "warning", request.sender_name .. " 取消了传送请求。")
    end
end)

plugin:listen("OnClientDisconnect", function(slot)
    -- 此时控制器可能已进入 Disconnecting，读取完整玩家快照会触发无效原生字段。
    -- 请求创建时同时保存了连接槽位，因此可以直接定位需要清理的请求；长期身份仍用 SteamID。
    local affected = {}
    for _, request in pairs(outgoing) do
        if request.sender_slot == slot or request.target_slot == slot then
            affected[#affected + 1] = request
        end
    end

    for _, request in ipairs(affected) do
        remove_request(request)
        if request.sender_slot == slot then
            notify_online(request.target_id, "warning", request.sender_name .. " 已离开服务器，传送请求已取消。")
        else
            notify_online(request.sender_id, "warning", request.target_name .. " 已离开服务器，传送请求已取消。")
        end
    end
end)
