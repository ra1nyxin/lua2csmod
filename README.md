> **[📖 English](README.en-us.md)**
> **[📖 简体中文(大陆)](README.md)**

# 🌙 Lua2CS

Lua2CS 是面向 CounterStrikeSharp 的 Lua 5.4 插件宿主。C# 宿主只需安装一次，之后即可直接编写、加载和热重载 Lua 玩法脚本，无需反复编译 DLL 或重启 CS2 服务器。

## 🧩 环境要求

- Linux x64 或 Windows x64 CS2 专用服务器
- Metamod:Source
- CounterStrikeSharp API 1.0.373 或更高版本，并安装 .NET 10 运行时

## 📦 安装

1. 从 [预发布版本](https://github.com/ra1nyxin/lua2csmod/releases/tag/preview) 下载服务器系统对应的安装包：Linux 使用 `Lua2CS-preview-linux-x64.zip`，Windows 使用 `Lua2CS-preview-win-x64.zip`。
2. 将压缩包解压到服务器的 `game/csgo` 目录。
3. 重启服务器，或执行 `css_plugins load Lua2CS`。
4. 安装包默认启用 TPA；其他 Lua 脚本可继续放入 `addons/counterstrikesharp/plugins/Lua2CS/scripts`。

两个压缩包都已包含 NLua、KeraLua 及对应系统的 Lua 5.4 原生库，不需要在服务器上额外安装 Lua。Linux 包携带 `liblua54.so`，Windows 包携带 `lua54.dll`，请勿混用。Linux 包中的 Lua 5.4.8 使用 ELF 私有符号绑定构建，可避免 CS2 `libvscript.so` 导出的旧版 Lua 符号抢占 NLua 内部调用。

Lua2CS 在加载时会立即创建临时 Lua VM，检查原生库确实可用且版本为 Lua 5.4。安装后可在服务器控制台执行 `css_lua status`，查看 Lua、CounterStrikeSharp、运行平台、自动重载状态和实际脚本目录。即使 `scripts` 目录为空，这项自检也会执行。

## 📍 默认 TPA

首次安装会直接启用 `scripts/tpa.lua`，所有游戏内玩家均可使用：

```text
!tpalist
!tpa <查询>
!tpaid <userid>
!tpaslot <slot>
!tpaname <名字>
!tpahere <查询>
!tpahereid <userid>
!tpahereslot <slot>
!tpaherename <名字>
!tpaccept [玩家]
!tpdeny [玩家]
!tpcancel
```

`!tpalist` 会列出 userid 和 slot。`!tpa` 与 `!tpahere` 的查询支持 slot、userid、SteamID64 和名字片段；对应的 `id`、`slot`、`name` 命令可强制按一种方式查询。`!tpahere` 会邀请目标玩家传送到自己身边。`!tpaccept` 或 `!tpdeny` 不写玩家名时会处理最新收到的有效请求，写玩家名时可处理指定请求。请求 30 秒后过期，每名请求者同时只能保留一个请求。

TPA 不限制敌我阵营、回合阶段、冻结时间、交战状态、空中位置、导航网格或地图边界；只要双方仍在线、存活且锚点玩家具有有效位置，就会尝试传送。接受后，实际移动的一方会落在锚点玩家水平随机 48 单位的位置，并清除原有移动速度。命令没有管理员权限要求。删除 `scripts/tpa.lua` 并执行 `css_lua unload tpa` 即可停用；重新解压新版安装包时会恢复官方 TPA 文件。

## 🧪 脚本示例

```lua
local plugin = cs.plugin({
    name = "你好 Lua",
    version = "1.0.0"
})

plugin:on("player_chat", function(event)
    if event.player ~= nil and event.text:lower() == "hello" then
        event.player:print_chat("你好，消息来自 Lua 5.4！")
    end
    return cs.continue
end)

plugin:command("css_luahello", function(player, command)
    command:reply("Lua 命令执行成功。")
end)
```

`scripts` 目录中的每个顶层 `.lua` 文件都是一个独立插件，并拥有独立的 Lua VM。以下划线开头的文件和子目录中的文件只会作为模块使用，不会被当作独立插件加载。

## 🛠️ 管理命令

```text
css_lua list
css_lua status
css_lua load <脚本名>
css_lua reload <脚本名>
css_lua unload <脚本名>
css_lua reload_all
```

默认要求 `@css/root` 权限。服务器控制台和 RCON 始终可以使用管理命令。在游戏聊天中也可通过 CounterStrikeSharp 的命令触发方式执行，例如 `!lua list`。`css_lua doctor` 是 `css_lua status` 的别名。

开启自动重载后，修改顶层脚本只会重载对应插件；修改子目录中的公共模块会重载全部 Lua 插件。新脚本会先在独立 VM 中完成语法检查、执行和注册验证，再替换当前版本。重载失败时会保留或恢复旧版本。

## ⚙️ 配置

CounterStrikeSharp 会生成 `addons/counterstrikesharp/configs/plugins/Lua2CS/Lua2CS.json`：

```json
{
  "ScriptsDirectory": "scripts",
  "AutoReload": true,
  "ReloadDebounceMilliseconds": 400,
  "AdminPermission": "@css/root",
  "AllowUnsafeLibraries": false,
  "ConfigVersion": 1
}
```

- `ScriptsDirectory`：相对于 Lua2CS 插件目录的脚本目录，不允许逃逸到插件目录之外。
- `AutoReload`：监听 `.lua` 文件变化并自动重载。
- `ReloadDebounceMilliseconds`：文件变化防抖时间，允许范围为 100 到 5000 毫秒。
- `AdminPermission`：游戏内管理命令要求的 CounterStrikeSharp 权限；留空表示不检查权限。
- `AllowUnsafeLibraries`：恢复 Lua 文件和操作系统库。即使开启，也不会暴露 `luanet`。

默认情况下，Lua 脚本不能访问 `luanet`、原生模块、进程执行或直接文件 I/O。`require` 仍可加载当前脚本目录中的 Lua 模块。

## 🔌 接口范围

Lua 脚本可以使用以下能力：

- 游戏事件、Listener、自定义命令、原生命令前后置监听、定时器和帧/Tick 调度。
- 玩家列表、CSS 原生目标语法，以及按槽位、userid、SteamID64 和名字查询玩家。
- 玩家生命、护甲、金钱、坐标、视角、武器、按键、队伍和回合状态快照。
- 玩家聊天、控制台、中央提示、HTML HUD、权限、客户端命令、声音、传送、复活、队伍和武器操作。
- 服务器地图、时间、Tick、地图列表、模型预缓存和控制台命令。
- ConVar 读取与修改、官方事件名和 Listener 名枚举。
- 游戏规则、回合阶段、炸弹状态和双方比分查询。
- 插件级 JSON 持久化，以及带完整 CHandle 校验的实体查询、创建、输入、传送和删除。
- 聊天/控制台菜单、回合结束控制、准星目标和导航网格查询。
- 玩家与实体的最大生命、重力、速度倍率、模型和渲染颜色控制。
- 玩家详细武器与弹药快照、现存武器查询、发放、弹药和经济外观修改。
- 武器与通用实体均使用完整 CHandle 校验，可刷新、传送和延迟清理。
- 计分板数据、语音标志、单客户端 ConVar 和机器人客户端 ConVar 控制。

Lua 与 C# 之间的字符串统一使用 UTF-8，可直接使用中文插件名、命令说明、聊天文本和持久化内容。修改生命、武器、队伍、实体、传送和 ConVar 等操作会直接影响服务器状态，应只向可信脚本开放。

## 📚 示例模板

`examples` 中包含 59 个可独立加载的中文模板和 1 个公共模块示例。除基础命令、事件、管理、玩家、武器、实体、导航、持久化、HUD 和模块化脚本外，还包含连杀竞技场、武器商店、地图投票、枪械升级、跑酷计时、重装 Boss、吸血、玩家悬赏、混沌回合、烫手山芋、一发入魂、僵尸感染、俄罗斯轮盘、反应赛、知识问答、山丘之王、导航寻宝和死亡换位等进阶玩法。模板包含关键流程、快照时效、身份校验、回合清理、热重载恢复和风险点的中文注释。

除 TPA 外的模板默认不会自动运行。安装包会把 TPA 同时放入 `scripts` 和 `examples`，其余模板只放在 `examples`；选中其他模板后复制到同级 `scripts` 目录即可加载，后续保存文件会自动热重载。

完整接口参见 [Lua 脚本接口](docs/lua-api.md)，可运行示例位于 [examples](examples)。

## 🏗️ 本地构建

```bash
dotnet restore
dotnet test -c Release
./package.sh
```

Linux 打包还需要 `curl`、`tar`、glibc C 编译器和 GNU binutils。构建脚本会下载固定版本的 Lua 5.4.8 源码、校验 SHA-256，再生成带 `SYMBOLIC` 标记且最高兼容到 glibc 2.35 的原生库；若产物意外引用更新的 glibc 符号，打包会直接失败。可通过 `LUA2CS_NATIVE_CACHE` 指定源码缓存目录。

脚本会同时生成以下服务器安装包：

- `artifacts/Lua2CS-preview-linux-x64.zip`
- `artifacts/Lua2CS-preview-win-x64.zip`
