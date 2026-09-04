using CounterStrikeSharp.API.Core;

namespace Lua2CS;

public sealed class Lua2CSConfig : BasePluginConfig
{
    public string ScriptsDirectory { get; set; } = "scripts";
    public bool AutoReload { get; set; } = true;
    public int ReloadDebounceMilliseconds { get; set; } = 400;
    public int SlowCallbackMilliseconds { get; set; } = 25;
    public string AdminPermission { get; set; } = "@css/root";
    public bool AllowUnsafeLibraries { get; set; }
}
