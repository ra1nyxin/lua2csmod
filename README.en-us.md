> **[📖 English](README.en-us.md)**
> **[📖 简体中文(大陆)](README.md)**

# 🌙 Lua2CS

Lua2CS is a Lua 5.4 plugin host for CounterStrikeSharp. Install the C# host once, then write, load, and hot-reload Lua gameplay scripts directly without repeatedly compiling DLLs or restarting the CS2 server.

## 🧩 Requirements

- Linux x64 or Windows x64 CS2 dedicated server
- Metamod:Source
- CounterStrikeSharp API 1.0.373 or later with the .NET 10 runtime installed

## 📦 Installation

1. Download the package for your server platform from the [preview release](https://github.com/ra1nyxin/lua2csmod/releases/tag/preview): use `Lua2CS-preview-linux-x64.zip` for Linux or `Lua2CS-preview-win-x64.zip` for Windows.
2. Extract the package into the server's `game/csgo` directory.
3. Restart the server, or run `css_plugins load Lua2CS`.
4. The package enables TPA by default; place additional Lua scripts in `addons/counterstrikesharp/plugins/Lua2CS/scripts`.

Both packages include NLua, KeraLua, and the Lua 5.4 native library for their target platform, so no separate Lua installation is required. The Linux package contains `liblua54.so`, while the Windows package contains `lua54.dll`; do not mix them. Lua 5.4.8 in the Linux package is built with private ELF symbol binding to prevent the older Lua symbols exported by CS2's `libvscript.so` from intercepting NLua's internal calls.

On startup, Lua2CS immediately creates a temporary Lua VM to verify that the native library is available and really is Lua 5.4. After installation, run `css_lua status` in the server console to view the Lua and CounterStrikeSharp versions, runtime platform, automatic reload state, and effective script directory. This self-check runs even when the `scripts` directory is empty.

## 📍 Default TPA

A fresh installation enables `scripts/tpa.lua` immediately, allowing every in-game player to use:

```text
!tpalist
!tpa <query>
!tpaid <userid>
!tpaslot <slot>
!tpaname <name>
!tpahere <query>
!tpahereid <userid>
!tpahereslot <slot>
!tpaherename <name>
!tpaccept [player]
!tpdeny [player]
!tpcancel
```

`!tpalist` lists userids and slots. The queries accepted by `!tpa` and `!tpahere` support slots, userids, SteamID64 values, and name fragments; the corresponding `id`, `slot`, and `name` commands force one lookup mode. `!tpahere` invites the target player to teleport to the requester. Without a player name, `!tpaccept` or `!tpdeny` handles the newest valid incoming request; with a name, it handles the specified request. Requests expire after 30 seconds, and each requester can have only one pending request.

TPA does not restrict teams, round phases, freeze time, combat state, airborne positions, navigation meshes, or map boundaries. It attempts a teleport as long as both players remain online and alive and the anchor player has a valid position. After acceptance, the player who moves is placed at a random horizontal offset of 48 units from the anchor, with their existing velocity cleared. The commands require no administrator permission. Delete `scripts/tpa.lua` and run `css_lua unload tpa` to disable it; extracting a new release package again restores the official TPA file.

## 🧪 Script Example

```lua
local plugin = cs.plugin({
    name = "Hello Lua",
    version = "1.0.0"
})

plugin:on("player_chat", function(event)
    if event.player ~= nil and event.text:lower() == "hello" then
        event.player:print_chat("Hello from Lua 5.4!")
    end
    return cs.continue
end)

plugin:command("css_luahello", function(player, command)
    command:reply("Lua command executed successfully.")
end)
```

Every top-level `.lua` file in `scripts` is an independent plugin with its own Lua VM. Files whose names begin with an underscore and files in subdirectories are loaded only as modules, not as independent plugins.

## 🛠️ Management Commands

```text
css_lua list
css_lua status
css_lua load <script>
css_lua reload <script>
css_lua unload <script>
css_lua reload_all
```

The commands require `@css/root` permission by default. The server console and RCON may always use management commands. They can also be invoked through CounterStrikeSharp's command trigger in game chat, for example `!lua list`. `css_lua doctor` is an alias for `css_lua status`.

With automatic reload enabled, changing a top-level script reloads only that plugin, while changing a shared module in a subdirectory reloads every Lua plugin. A new script is syntax-checked, executed, and registration-validated in an isolated VM before it replaces the current version. If reload fails, the old version is retained or restored.

## ⚙️ Configuration

CounterStrikeSharp generates `addons/counterstrikesharp/configs/plugins/Lua2CS/Lua2CS.json`:

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

- `ScriptsDirectory`: Script directory relative to the Lua2CS plugin directory; it cannot escape the plugin directory.
- `AutoReload`: Watches `.lua` file changes and reloads them automatically.
- `ReloadDebounceMilliseconds`: File-change debounce interval, from 100 to 5000 milliseconds.
- `AdminPermission`: CounterStrikeSharp permission required for in-game management commands; leave empty to disable permission checks.
- `AllowUnsafeLibraries`: Restores the Lua file and operating-system libraries. `luanet` remains unavailable even when enabled.

By default, Lua scripts cannot access `luanet`, native modules, process execution, or direct file I/O. `require` can still load Lua modules from the current script directory.

## 🔌 API Coverage

Lua scripts can use the following capabilities:

- Game events, listeners, custom commands, pre/post native command listeners, timers, and frame/tick scheduling.
- Player lists, native CSS target syntax, and player lookup by slot, userid, SteamID64, or name.
- Player health, armor, money, coordinates, view angles, weapons, buttons, team, and round-state snapshots.
- Player chat, console, center text, HTML HUD, permissions, client commands, sounds, teleportation, respawning, teams, and weapon operations.
- Server maps, time, ticks, map lists, model precaching, and console commands.
- ConVar reading and modification, plus enumeration of official event and listener names.
- Game rules, round phases, bomb state, and team score queries.
- Plugin-level JSON persistence and entity lookup, creation, input, teleportation, and deletion with full CHandle validation.
- Chat/console menus, round-end control, crosshair targets, and navigation-mesh queries.
- Maximum health, gravity, velocity modifiers, models, and render color control for players and entities.
- Detailed player weapon and ammunition snapshots, existing weapon queries, item granting, ammunition changes, and economic appearance modification.
- Full CHandle validation for weapons and general entities, with refresh, teleportation, and deferred cleanup support.
- Scoreboard data, voice flags, per-client ConVars, and bot client ConVar control.

Strings passed between Lua and C# consistently use UTF-8, so plugin names, command descriptions, chat text, and persisted content can contain non-ASCII text directly. Operations that modify health, weapons, teams, entities, positions, or ConVars affect the server immediately and should be exposed only to trusted scripts.

## 📚 Example Templates

`examples` contains 59 independently loadable templates and one shared-module example. Alongside basic commands, events, administration, players, weapons, entities, navigation, persistence, HUDs, and modular scripts, the collection now includes advanced modes for killstreaks, weapon shops, map voting, gun game, parkour timing, juggernaut battles, vampirism, player bounties, chaos rounds, hot potato, one-in-the-chamber, zombie infection, Russian roulette, reaction races, trivia, king of the hill, navigation treasure hunts, and death swaps. Templates document key flows, snapshot lifetimes, identity validation, round cleanup, hot-reload recovery, and risk points.

Templates other than TPA do not run by default. The package places TPA in both `scripts` and `examples`, while all other templates are installed only in `examples`. Copy a selected template into the sibling `scripts` directory to load it; later saves are hot-reloaded automatically.

See the complete [Lua scripting API](docs/lua-api.md), with runnable templates in [examples](examples).

## 🏗️ Local Build

```bash
dotnet restore
dotnet test -c Release
./package.sh
```

Linux packaging also requires `curl`, `tar`, a glibc C compiler, and GNU binutils. The build script downloads a pinned Lua 5.4.8 source archive, verifies its SHA-256 digest, and produces a native library with `SYMBOLIC` binding and compatibility up to glibc 2.35. Packaging fails if the output unexpectedly references newer glibc symbols. Set `LUA2CS_NATIVE_CACHE` to choose the source cache directory.

The script generates both server installation packages:

- `artifacts/Lua2CS-preview-linux-x64.zip`
- `artifacts/Lua2CS-preview-win-x64.zip`
