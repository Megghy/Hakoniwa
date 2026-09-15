using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using Hakoniwa.Core;
using Xunit;

namespace Hakoniwa.Tests;

public sealed class HookManagerTests
{
    [Fact]
    public void Dispose_IsIdempotent_AndRejectsRegisterAfterwards()
    {
        var manager = new HookManager();
        manager.Dispose();
        manager.Dispose();
        Assert.Throws<ObjectDisposedException>(() =>
            manager.RegisterDetour(typeof(Probe).GetMethod(nameof(Probe.Inc))!, new Func<Func<int, int>, int, int>(Hooked)));
    }

    [Fact]
    public void RegisterDetour_InterceptsAndRestores()
    {
        MethodInfo method = typeof(Probe).GetMethod(nameof(Probe.Inc))!;
        Assert.Equal(2, Probe.Inc(1));
        using (var manager = new HookManager())
        {
            manager.RegisterDetour(method, new Func<Func<int, int>, int, int>(Hooked));
            Assert.Equal(4, Probe.Inc(1));
        }

        Assert.Equal(2, Probe.Inc(1));
    }

    private static int Hooked(Func<int, int> orig, int x) => orig(x) * 2;

    public static class Probe
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static int Inc(int x) => x + 1;
    }
}
