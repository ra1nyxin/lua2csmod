local plugin = cs.plugin({
    name = "赏金猎场",
    version = "0.0.1",
    description = "玩家可共同悬赏目标，击杀者领取奖池，离服目标触发退款"
})

local config = {
    minimum = 500,
    maximum_per_order = 8000,
    money_cap = 16000,
    cleanup_interval = 5
}

-- bounty 的 contributors 记录每笔托管资金，用于目标离服后的精确退款。
local bounties = {}
local pending_refunds = {}

local function add_refund(steam_id, amount)
    pending_refunds[steam_id] = (pending_refunds[steam_id] or 0) + amount
end

local function refund_bounty(target_id, reason)
    local bounty = bounties[target_id]
    if bounty == nil then return end
    bounties[target_id] = nil
    for steam_id, amount in pairs(bounty.contributors) do add_refund(steam_id, amount) end
    cs.server.print_chat_all(string.format("%s对 %s 的 $%d 悬赏已%s，出资将在玩家在线时退回。",
        cs.colors.grey, bounty.target_name, bounty.total, reason))
end

local function pay_refund(player)
    local amount = pending_refunds[player.steam_id]
    if amount == nil or amount <= 0 or player.money == nil then return end
    local payable = math.min(amount, math.max(0, config.money_cap - player.money))
    if payable > 0 and player:set_money(player.money + payable) then
        pending_refunds[player.steam_id] = amount - payable
        player:print_chat(string.format("%s收到赏金退款 $%d。", cs.colors.green, payable))
    end
    if pending_refunds[player.steam_id] == 0 then pending_refunds[player.steam_id] = nil end
end

local function find_target(query, command)
    local matches = cs.players.find(query)
    local humans = {}
    for _, player in ipairs(matches) do
        if not player.is_bot and not player.is_hltv then humans[#humans + 1] = player end
    end
    if #humans ~= 1 then
        command:reply(#humans == 0 and "没有找到玩家。" or "匹配到多名玩家，请输入更完整的名字。")
        return nil
    end
    return humans[1]
end

plugin:command("css_bounty", {
    description = "悬赏一名玩家",
    allow_console = false,
    min_args = 2,
    usage = "<玩家> <金额>"
}, function(player, command)
    local target = find_target(command.args[1], command)
    local amount = math.floor(tonumber(command.args[2]) or -1)
    if target == nil then return end
    if target.steam_id == player.steam_id then
        command:reply("不能悬赏自己。")
        return
    end
    if amount < config.minimum or amount > config.maximum_per_order then
        command:reply(string.format("单笔金额必须在 $%d 到 $%d 之间。", config.minimum, config.maximum_per_order))
        return
    end
    if player.money == nil or player.money < amount then
        command:reply("余额不足。")
        return
    end

    -- 扣款成功后才写入托管账本，失败时不会产生凭空赏金。
    if not player:set_money(player.money - amount) then
        command:reply("扣款失败，悬赏未创建。")
        return
    end
    local bounty = bounties[target.steam_id] or {
        target_name = target.name,
        total = 0,
        contributors = {}
    }
    bounty.target_name = target.name
    bounty.total = bounty.total + amount
    bounty.contributors[player.steam_id] = (bounty.contributors[player.steam_id] or 0) + amount
    bounties[target.steam_id] = bounty
    cs.server.print_chat_all(string.format("%s%s 的赏金升至 $%d！", cs.colors.gold, target.name, bounty.total))
end)

plugin:on("player_death", function(event)
    local victim = event.player
    local attacker = event.attacker
    if victim == nil then return cs.continue end
    local bounty = bounties[victim.steam_id]
    if bounty == nil then return cs.continue end
    if attacker == nil or attacker.is_bot or attacker.is_hltv or attacker.steam_id == victim.steam_id then
        return cs.continue
    end

    local money = attacker.money or 0
    local payout = math.min(bounty.total, math.max(0, config.money_cap - money))
    if payout > 0 and attacker:set_money(money + payout) then
        bounties[victim.steam_id] = nil
        cs.server.print_chat_all(string.format("%s%s 击杀 %s，领取 $%d 赏金！",
            cs.colors.green, attacker.name, victim.name, payout))
    else
        attacker:print_chat("赏金暂未结算：你的余额已满或写入失败。")
    end
    return cs.continue
end)

plugin:every(config.cleanup_interval, function()
    for target_id, _ in pairs(bounties) do
        if cs.players.get_steamid(target_id) == nil then refund_bounty(target_id, "撤销") end
    end
    for _, player in ipairs(cs.players.humans()) do pay_refund(player) end
end)

plugin:command("css_bounties", {
    description = "查看当前所有悬赏",
    allow_console = true
}, function(_, command)
    local count = 0
    for _, bounty in pairs(bounties) do
        count = count + 1
        command:reply(string.format("%s：$%d", bounty.target_name, bounty.total))
    end
    if count == 0 then command:reply("当前没有悬赏。") end
end)

plugin:listen("OnMapStart", function()
    for target_id, _ in pairs(bounties) do refund_bounty(target_id, "随换图撤销") end
end)
