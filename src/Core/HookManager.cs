using System;
using System.Collections.Generic;
using System.Reflection;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;

namespace Hakoniwa.Core;

/// <summary>
/// MonoMod Hook 集中生命周期管理器
/// 遵循 A04/KISS 原则：统一挂载与逆序安全释放，杜绝 Hook 泄漏与重入冲突
/// </summary>
public sealed class HookManager : IDisposable
{
    private readonly List<IDisposable> _hooks = [];
    private bool _disposed;

    public void RegisterDetour(MethodBase from, MethodInfo to)
    {
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));
        if (_disposed) throw new ObjectDisposedException(nameof(HookManager));
        _hooks.Add(new Hook(from, to));
    }

    public void RegisterDetour(MethodBase from, Delegate to)
    {
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (to is null) throw new ArgumentNullException(nameof(to));
        if (_disposed) throw new ObjectDisposedException(nameof(HookManager));
        _hooks.Add(new Hook(from, to));
    }

    public void RegisterILHook(MethodBase from, ILContext.Manipulator manipulator)
    {
        if (from is null) throw new ArgumentNullException(nameof(from));
        if (manipulator is null) throw new ArgumentNullException(nameof(manipulator));
        if (_disposed) throw new ObjectDisposedException(nameof(HookManager));
        _hooks.Add(new ILHook(from, manipulator));
    }

    public void Register(IDisposable hook)
    {
        if (hook is null) throw new ArgumentNullException(nameof(hook));
        if (_disposed) throw new ObjectDisposedException(nameof(HookManager));
        _hooks.Add(hook);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        Exception? first = null;
        for (int i = _hooks.Count - 1; i >= 0; i--)
        {
            try
            {
                _hooks[i].Dispose();
            }
            catch (Exception ex)
            {
                first ??= ex;
            }
        }
        _hooks.Clear();
        if (first is not null)
            throw first;
    }
}
