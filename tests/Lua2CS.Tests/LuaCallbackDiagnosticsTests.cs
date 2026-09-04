namespace Lua2CS.Tests;

public sealed class LuaCallbackDiagnosticsTests
{
    [Fact]
    public void TracksFailuresAndThrottlesSlowCallbackWarnings()
    {
        var diagnostics = new LuaCallbackDiagnostics(10);

        Assert.False(diagnostics.Record("快速命令", TimeSpan.FromMilliseconds(2), null));
        Assert.True(diagnostics.Record("慢定时器", TimeSpan.FromMilliseconds(15), new InvalidOperationException("测试异常")));
        Assert.False(diagnostics.Record("慢定时器", TimeSpan.FromMilliseconds(12), null));

        var snapshot = diagnostics.Snapshot();
        Assert.Equal(3, snapshot.InvocationCount);
        Assert.Equal(1, snapshot.FailureCount);
        Assert.Equal(2, snapshot.SlowCallbackCount);
        Assert.Equal("慢定时器", snapshot.LastFailureSource);
        Assert.Equal("测试异常", snapshot.LastFailureMessage);
        Assert.Equal("慢定时器", snapshot.LastSlowSource);
        Assert.Equal(12, snapshot.LastSlowMilliseconds);
        Assert.Equal(15, snapshot.MaximumMilliseconds);
        Assert.True(snapshot.AverageMilliseconds > 0);
    }

    [Fact]
    public void CombinesMultiplePluginSnapshots()
    {
        var first = new LuaCallbackDiagnostics(10);
        var second = new LuaCallbackDiagnostics(10);
        first.Record("事件 player_death", TimeSpan.FromMilliseconds(4), null);
        second.Record("命令 css_test", TimeSpan.FromMilliseconds(20), new InvalidOperationException("失败"));

        var combined = LuaCallbackDiagnosticsSnapshot.Combine([first.Snapshot(), second.Snapshot()]);

        Assert.Equal(2, combined.InvocationCount);
        Assert.Equal(1, combined.FailureCount);
        Assert.Equal(1, combined.SlowCallbackCount);
        Assert.Equal(20, combined.MaximumMilliseconds);
        Assert.Equal("命令 css_test", combined.LastFailureSource);
        Assert.Equal("命令 css_test", combined.LastSlowSource);
    }
}
