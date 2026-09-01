local plugin = cs.plugin({
    name = "烫手山芋",
    version = "0.0.1",
    description = "随机玩家携带倒计时炸弹，攻击别人即可把它传出去"
})

local config = {
    start_delay = 3,
    fuse_seconds = 18,
    transfer_cooldown = 0.8,
    minimum_players = 2
}

local holder_id = nil
local deadline = 0
local generation = 0
local last_transfer = 0

local function now()
    return cs.server.info().engine_time
end

local function alive_humans(except_id)
    local result = {}
    for _, player in ipairs(cs.players.humans()) do
        if not player.is_hltv and player.is_alive and player.steam_id ~= except_id then
            result[#result + 1] = player
        end
    end
    return result
end

local function tint(player, active)
    if player == nil then return end
    if active then
        player:set_render_color(255, 55, 20, 255)
    else
        player:set_render_color(255, 255, 255, 255)
    end
end

local function give_potato(player, reset_fuse)
    local old = holder_id and cs.players.get_steamid(holder_id) or nil
    tint(old, false)
    holder_id = player.steam_id
    if reset_fuse then deadline = now() + config.fuse_seconds end
    last_transfer = now()
    tint(player, true)
    cs.server.print_chat_all(cs.colors.red .. "[烫手山芋] 炸弹现在在 " .. player.name .. " 手里！")
end

local function begin_round(expected_generation)
    if expected_generation ~= generation then return end
    local players = alive_humans(nil)
    if #players < config.minimum_players then
        cs.server.print_chat_all("[烫手山芋] 存活玩家不足，玩法本回合暂停。")
        return
    end
    give_potato(players[math.random(#players)], true)
end

local function pass_after_loss(old_holder_id, expected_generation)
    plugin:next_frame(function()
        if generation ~= expected_generation or holder_id ~= old_holder_id then return end
        local players = alive_humans(old_holder_id)
        if #players == 0 then
            holder_id = nil
            return
        end
        give_potato(players[math.random(#players)], true)
    end)
end

plugin:on("round_start", function()
    generation = generation + 1
    holder_id = nil
    deadline = 0
    local expected_generation = generation
    plugin:after(config.start_delay, function() begin_round(expected_generation) end)
    return cs.continue
end)

plugin:on("player_hurt", function(event)
    local attacker = event.attacker
    local victim = event.player
    if holder_id == nil or attacker == nil or victim == nil then return cs.continue end
    if attacker.steam_id ~= holder_id or victim.steam_id == holder_id then return cs.continue end
    if attacker.is_bot or victim.is_bot or attacker.is_hltv or victim.is_hltv then return cs.continue end
    if now() - last_transfer < config.transfer_cooldown then return cs.continue end

    -- 传递不重置引信，越到后期越需要冒险贴近别人。
    give_potato(victim, false)
    return cs.continue
end)

plugin:on("player_death", function(event)
    local victim = event.player
    if victim ~= nil and victim.steam_id == holder_id then
        tint(victim, false)
        pass_after_loss(holder_id, generation)
    end
    return cs.continue
end)

plugin:every(0.25, function()
    if holder_id == nil then return end
    local holder = cs.players.get_steamid(holder_id)
    if holder == nil or not holder.is_alive then
        pass_after_loss(holder_id, generation)
        return
    end

    local remaining = deadline - now()
    if remaining <= 0 then
        local doomed_id = holder_id
        holder_id = nil
        cs.server.print_chat_all(cs.colors.red .. holder.name .. " 没能传出炸弹，爆炸！")
        holder:kill(true, true)
        tint(holder, false)
        -- 爆炸后本回合不再生成第二枚，交给正常胜负规则收尾。
        cs.log.info("烫手山芋已引爆：" .. doomed_id)
    else
        holder:print_html(string.format("<font color='#ff4020'>炸弹 %.1f 秒</font><br>攻击别人传递", remaining), 1)
    end
end)

plugin:on("round_end", function()
    generation = generation + 1
    tint(holder_id and cs.players.get_steamid(holder_id) or nil, false)
    holder_id = nil
    return cs.continue
end)

plugin:listen("OnMapStart", function()
    generation = generation + 1
    holder_id = nil
end)

plugin:on_unload(function()
    generation = generation + 1
    tint(holder_id and cs.players.get_steamid(holder_id) or nil, false)
end)
