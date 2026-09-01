local plugin = cs.plugin({
    name = "战地武器商店",
    version = "0.0.1",
    description = "带余额校验、每回合限购和失败回滚的菜单商店"
})

local config = {
    command = "css_shop",
    purchase_limit = 3,
    items = {
        { name = "AK-47", weapon = "weapon_ak47", price = 2700 },
        { name = "M4A1-S", weapon = "weapon_m4a1_silencer", price = 2900 },
        { name = "AWP", weapon = "weapon_awp", price = 4750 },
        { name = "沙漠之鹰", weapon = "weapon_deagle", price = 700 },
        { name = "高爆手雷", weapon = "weapon_hegrenade", price = 300 },
        { name = "全甲", armor = 100, price = 1000 },
        { name = "紧急医疗", heal = 35, price = 1200 }
    }
}

local purchases = {}

local function human(player)
    return player ~= nil and not player.is_bot and not player.is_hltv
end

local function buy(player, item)
    local current = cs.players.get_steamid(player.steam_id)
    if current == nil or not current.is_alive then
        return false, "只有存活玩家可以购买。"
    end

    local used = purchases[current.steam_id] or 0
    if used >= config.purchase_limit then
        return false, "本回合购买次数已经用完。"
    end

    local money = current.money or 0
    if money < item.price then
        return false, string.format("余额不足：需要 $%d，当前 $%d。", item.price, money)
    end

    -- 先执行可能失败的发放，成功后才扣钱，天然具备失败回滚语义。
    local delivered = false
    if item.weapon then
        delivered = current:give_item(item.weapon)
    elseif item.armor then
        delivered = current:set_armor(item.armor)
    elseif item.heal then
        local old_health = current.health or 100
        delivered = current:set_health(math.min(current.max_health or 100, old_health + item.heal))
    end

    if not delivered then return false, "发放失败，没有扣除金钱。" end
    if not current:set_money(money - item.price) then
        -- Pawn 在发放与扣款之间失效时无法可靠收回物品，记录清晰日志供服主排查。
        cs.log.warn("商店扣款失败：" .. current.steam_id .. " / " .. item.name)
        return false, "扣款失败，请联系管理员检查日志。"
    end

    purchases[current.steam_id] = used + 1
    return true, string.format("已购买 %s，剩余 $%d。", item.name, money - item.price)
end

local function open_shop(player)
    local menu_items = {}
    for index, item in ipairs(config.items) do
        menu_items[index] = {
            text = string.format("%s  $%d", item.name, item.price)
        }
    end

    cs.menu.open(player, {
        title = string.format("战地商店（本回合 %d/%d）", purchases[player.steam_id] or 0, config.purchase_limit),
        type = "chat",
        exit_button = true,
        post_select = "close",
        items = menu_items
    }, function(selected_player, index)
        local item = config.items[index]
        if item == nil then return end
        local ok, message = buy(selected_player, item)
        selected_player:print_chat((ok and cs.colors.green or cs.colors.red) .. message)
    end)
end

plugin:command(config.command, {
    description = "打开战地武器商店",
    allow_console = false
}, function(player, command)
    if not human(player) then
        command:reply("该商店只服务真实玩家。")
        return
    end
    open_shop(player)
end)

plugin:on("round_start", function()
    purchases = {}
    for _, player in ipairs(cs.players.humans()) do
        player:print_chat(cs.colors.green .. "战地商店已营业，输入 !shop 打开菜单。")
    end
    return cs.continue
end)

plugin:listen("OnMapStart", function()
    purchases = {}
end)
