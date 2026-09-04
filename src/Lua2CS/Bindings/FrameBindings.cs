using CounterStrikeSharp.API;

namespace Lua2CS.Bindings;

public sealed class FrameBindings
{
    public void Validate(FrameRegistration registration) => ValidateRegistration(registration);

    internal static void ValidateRegistration(FrameRegistration registration)
    {
        if (registration.Schedule == FrameSchedule.AfterTicks && registration.TickDelay is < 1 or > 1_000_000)
        {
            throw new InvalidDataException("Lua after_ticks delay must be between 1 and 1000000 ticks.");
        }
    }

    public IRegistrationHandle Activate(LuaPlugin plugin, FrameRegistration registration)
    {
        DeferredHandle? handle = null;
        handle = new DeferredHandle(registration.Id);

        void Dispatch()
        {
            if (handle.IsDisposed || !plugin.IsActive) return;
            try
            {
                plugin.Invoke($"帧调度 {registration.Schedule} #{registration.Id}", registration.Callback);
            }
            finally
            {
                plugin.RemoveRegistration(registration.Id);
            }
        }

        switch (registration.Schedule)
        {
            case FrameSchedule.NextFrame:
                Server.NextFrame(Dispatch);
                break;
            case FrameSchedule.NextWorldUpdate:
                Server.NextWorldUpdate(Dispatch);
                break;
            case FrameSchedule.AfterTicks:
                Server.RunOnTick(checked(Server.TickCount + registration.TickDelay), Dispatch);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(registration));
        }

        return handle;
    }

    private sealed class DeferredHandle(long id) : IRegistrationHandle
    {
        private int _disposed;

        public long Id { get; } = id;
        public bool IsDisposed => Volatile.Read(ref _disposed) != 0;
        public void Dispose() => Interlocked.Exchange(ref _disposed, 1);
    }
}
