namespace Lua2CS.Tests;

public sealed class LuaScriptValidatorTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "lua2cs-validator-tests", Guid.NewGuid().ToString("N"));

    public LuaScriptValidatorTests() => Directory.CreateDirectory(_directory);

    [Fact]
    public void ValidatesMetadataAndRegistrationsWithoutACs2Server()
    {
        var path = Write("valid.lua", """
            local plugin = cs.plugin({ name = "验证器", version = "1.2.3" })
            plugin:on("player_death", function() return cs.continue end)
            plugin:command("css_validate", function() end)
            """);

        var result = Assert.Single(LuaScriptValidator.Validate(path));

        Assert.True(result.Success);
        Assert.Equal("valid", result.Key);
        Assert.Equal("验证器", result.Name);
        Assert.Equal("1.2.3", result.Version);
        Assert.Equal(2, result.RegistrationCount);
        Assert.Equal(["css_validate"], result.Commands);
    }

    [Fact]
    public void RejectsUnknownCounterStrikeSharpEvents()
    {
        var path = Write("bad_event.lua", """
            local plugin = cs.plugin({ name = "错误事件" })
            plugin:on("not_a_real_event", function() end)
            """);

        var result = Assert.Single(LuaScriptValidator.Validate(path));

        Assert.False(result.Success);
        Assert.Contains("Unknown CounterStrikeSharp event", result.Error);
    }

    [Fact]
    public void DetectsCommandsSharedByDirectoryScripts()
    {
        Write("first.lua", """
            local plugin = cs.plugin({ name = "First" })
            plugin:command("css_same", function() end)
            """);
        Write("second.lua", """
            local plugin = cs.plugin({ name = "Second" })
            plugin:command("css_same", function() end)
            """);
        Write("_module.lua", "return {}");
        Directory.CreateDirectory(Path.Combine(_directory, "nested"));
        File.WriteAllText(Path.Combine(_directory, "nested", "ignored.lua"), "return true");

        var results = LuaScriptValidator.Validate(_directory);

        Assert.Equal(2, results.Count);
        Assert.All(results, result =>
        {
            Assert.False(result.Success);
            Assert.Contains("css_same", result.Error);
        });
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }
}
