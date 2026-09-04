# Lua 开发与诊断

## Lua Language Server

Lua2CS 安装包会在插件目录附带 `types/Lua2CS.lua`，并在 `scripts` 目录附带 `.luarc.json.example`。推荐使用支持 Lua Language Server 的编辑器打开 `scripts` 目录，并将该示例配置改名为 `.luarc.json`。

配置中的 `../types` 相对于 `scripts` 目录，因此会指向同级的 `types` 目录。启用后可以获得以下能力：

- `cs`、`plugin`、玩家、实体、武器、菜单和定时器接口的补全与参数提示。
- 常用事件如 `player_death`、`player_hurt`、`player_spawn`、`player_chat`、`round_start`、`round_end` 与炸弹事件的字段提示。
- 以 Lua 5.4 规则检查语法，并将 `cs` 识别为合法全局变量。

类型库不参与服务器运行。不要在任何脚本中 `require("Lua2CS")`、`dofile` 或复制类型库内容；它仅供编辑器索引。

如果开发目录不在服务器插件目录中，可以把 `workspace.library` 改为 `Lua2CS.lua` 所在目录的绝对路径。仓库内的源文件位于 `lua-types/Lua2CS.lua`。

## 本地校验

本仓库提供跨平台的 .NET 校验器。它会使用与宿主相同的 Lua 5.4 运行时执行脚本顶层代码，验证 `cs.plugin` 元数据、Lua 语法、模块加载和注册参数；不会连接 CS2 服务端、注册游戏事件或执行事件回调。

```bash
dotnet run --project tools/Lua2CS.Validate -- examples/tpa.lua
dotnet run --project tools/Lua2CS.Validate -- examples
```

传入目录时，校验器只读取目录顶层且不以下划线开头的 `.lua` 文件，与 Lua2CS 的自动加载规则一致。它还会检测同目录脚本重复注册的 Lua 自定义命令。传入单一文件时，该文件也必须符合独立脚本规则。

默认使用安全 Lua 环境；只有确实需要模拟服务器开启 `AllowUnsafeLibraries` 的情况，才加上 `--allow-unsafe-libraries`。无论是否使用校验器，脚本顶层都不应修改 ConVar、创建实体、执行服务器命令或写入持久化数据，否则本地预检和热重载预检都可能产生副作用。
