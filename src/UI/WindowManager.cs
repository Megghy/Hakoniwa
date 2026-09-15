using System;
using System.Collections.Generic;
using Hakoniwa.UI.Windows;

namespace Hakoniwa.UI;

public sealed class WindowManager
{
    private readonly List<IWindow> _windows = [];

    public IReadOnlyList<IWindow> Windows => _windows;

    public void Add(IWindow window)
    {
        if (window is null) throw new ArgumentNullException(nameof(window));
        _windows.Add(window);
    }

    public T Get<T>() where T : class, IWindow
    {
        foreach (var window in _windows)
        {
            if (window is T match)
                return match;
        }

        throw new InvalidOperationException($"Window {typeof(T).Name} is not registered.");
    }

    public void DrawAll()
    {
        foreach (var window in _windows)
            window.Draw();
    }
}
