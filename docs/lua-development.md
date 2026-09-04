# Lua 开发与诊断

## Lua Language Server

Lua2CS 安装包会在插件目录附带 `types/Lua2CS.lua`，并在 `scripts` 目录附带 `.luarc.json.example`。推荐使用支持 Lua Language Server 的编辑器打开 `scripts` 目录，并将该示例配置改名为 `.luarc.json`。

配置中的 `../types` 相对于 `scripts` 目录，因此会指向同级的 `types` 目录。启用后可以获得以下能力：

- `cs`、`plugin`、玩家、实体、武器、菜单和定时器接口的补全与参数提示。
- 常用事件如 `player_death`、`player_hurt`、`player_spawn`、`player_chat`、`round_start`、`round_end` 与炸弹事件的字段提示。
- 以 Lua 5.4 规则检查语法，并将 `cs` 识别为合法全局变量。

类型库不参与服务器运行。不要在任何脚本中 `require("Lua2CS")`、`dofile` 或复制类型库内容；它仅供编辑器索引。

如果开发目录不在服务器插件目录中，可以把 `workspace.library` 改为 `Lua2CS.lua` 所在目录的绝对路径。仓库内的源文件位于 `lua-types/Lua2CS.lua`。
