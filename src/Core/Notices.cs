using System;

namespace Hakoniwa.Core;

public enum NotifyCorner
{
    左上,
    右上,
    左下,
    右下,
    上中,
    下中,
}

public static class Notices
{
    public static event Action<string>? Posted;

    public static void Post(string text)
    {
        if (text is null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length == 0)
            return;
        Posted?.Invoke(text);
    }
}
