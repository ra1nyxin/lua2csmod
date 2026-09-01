local plugin = cs.plugin({
    name = "awa",
    version = "0.1.3",
    description = "玩家发送 awa 时回复，并播报真人玩家进服和离服"
})

plugin:on("player_chat", function(event)
    -- player_chat 是游戏事件；返回 cs.continue 保持其他插件和原事件继续处理。
    if event.player ~= nil and event.text:lower() == "awa" then
        event.player:print_chat(cs.colors.light_purple .. "awa_from_lua!" .. cs.colors.default)
    end

    return cs.continue
end)

plugin:on("player_connect_full", function(event)
    -- 过滤机器人，避免批量加 Bot 时刷屏。
    if event.player ~= nil and not event.player.is_bot and not event.player.is_hltv then
        cs.server.print_chat_all(cs.colors.light_purple .. "awa_from_lua!!!" .. cs.colors.default)
    end

    return cs.continue
end)

plugin:on("player_disconnect", function(event)
    -- 只播报曾完整进入服务器的真人；玩家对象失效时用 networkid 排除 Bot。
    if not event.ever_fully_connected then
        return cs.continue
    end

    if event.player ~= nil then
        if event.player.is_bot or event.player.is_hltv then
            return cs.continue
        end
    elseif event.networkid == nil or event.networkid == "" or event.networkid:upper() == "BOT" then
        return cs.continue
    end

    cs.server.print_chat_all(cs.colors.light_purple .. "awa_from_lua...." .. cs.colors.default)
    return cs.continue
end)
