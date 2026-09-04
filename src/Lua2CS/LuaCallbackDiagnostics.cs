namespace Lua2CS;

public sealed record LuaCallbackDiagnosticsSnapshot(
    long InvocationCount,
    long FailureCount,
    long SlowCallbackCount,
    double TotalMilliseconds,
    double MaximumMilliseconds,
    string? LastFailureSource,
    string? LastFailureMessage,
    DateTimeOffset? LastFailureAt,
    string? LastSlowSource,
    double? LastSlowMilliseconds,
    DateTimeOffset? LastSlowAt)
{
    public double AverageMilliseconds => InvocationCount == 0 ? 0 : TotalMilliseconds / InvocationCount;

    internal static LuaCallbackDiagnosticsSnapshot Combine(IEnumerable<LuaCallbackDiagnosticsSnapshot> snapshots)
    {
        var values = snapshots.ToArray();
        var latestFailure = values
            .Where(snapshot => snapshot.LastFailureAt is not null)
            .MaxBy(snapshot => snapshot.LastFailureAt);
        var latestSlow = values
            .Where(snapshot => snapshot.LastSlowAt is not null)
            .MaxBy(snapshot => snapshot.LastSlowAt);

        return new LuaCallbackDiagnosticsSnapshot(
            values.Sum(snapshot => snapshot.InvocationCount),
            values.Sum(snapshot => snapshot.FailureCount),
            values.Sum(snapshot => snapshot.SlowCallbackCount),
            values.Sum(snapshot => snapshot.TotalMilliseconds),
            values.Select(snapshot => snapshot.MaximumMilliseconds).DefaultIfEmpty().Max(),
            latestFailure?.LastFailureSource,
            latestFailure?.LastFailureMessage,
            latestFailure?.LastFailureAt,
            latestSlow?.LastSlowSource,
            latestSlow?.LastSlowMilliseconds,
            latestSlow?.LastSlowAt);
    }
}

internal sealed class LuaCallbackDiagnostics(int slowCallbackMilliseconds)
{
    private static readonly TimeSpan SlowLogCooldown = TimeSpan.FromSeconds(30);
    private readonly object _gate = new();
    private readonly Dictionary<string, DateTimeOffset> _lastSlowWarningAt = new(StringComparer.Ordinal);
    private long _invocationCount;
    private long _failureCount;
    private long _slowCallbackCount;
    private double _totalMilliseconds;
    private double _maximumMilliseconds;
    private string? _lastFailureSource;
    private string? _lastFailureMessage;
    private DateTimeOffset? _lastFailureAt;
    private string? _lastSlowSource;
    private double? _lastSlowMilliseconds;
    private DateTimeOffset? _lastSlowAt;

    internal int SlowCallbackMilliseconds { get; } = slowCallbackMilliseconds;

    /// <summary>记录一次回调，并返回是否应写入慢回调警告。</summary>
    internal bool Record(string source, TimeSpan elapsed, Exception? failure)
    {
        var milliseconds = elapsed.TotalMilliseconds;
        var now = DateTimeOffset.UtcNow;
        lock (_gate)
        {
            _invocationCount++;
            _totalMilliseconds += milliseconds;
            _maximumMilliseconds = Math.Max(_maximumMilliseconds, milliseconds);

            if (failure is not null)
            {
                _failureCount++;
                _lastFailureSource = source;
                _lastFailureMessage = failure.GetBaseException().Message;
                _lastFailureAt = now;
            }

            if (milliseconds < SlowCallbackMilliseconds) return false;

            _slowCallbackCount++;
            _lastSlowSource = source;
            _lastSlowMilliseconds = milliseconds;
            _lastSlowAt = now;
            if (_lastSlowWarningAt.TryGetValue(source, out var lastWarning)
                && now - lastWarning < SlowLogCooldown)
            {
                return false;
            }

            _lastSlowWarningAt[source] = now;
            return true;
        }
    }

    internal LuaCallbackDiagnosticsSnapshot Snapshot()
    {
        lock (_gate)
        {
            return new LuaCallbackDiagnosticsSnapshot(
                _invocationCount,
                _failureCount,
                _slowCallbackCount,
                _totalMilliseconds,
                _maximumMilliseconds,
                _lastFailureSource,
                _lastFailureMessage,
                _lastFailureAt,
                _lastSlowSource,
                _lastSlowMilliseconds,
                _lastSlowAt);
        }
    }
}

public sealed record LuaOperationFailure(
    DateTimeOffset OccurredAt,
    string Key,
    string Operation,
    string Message);
