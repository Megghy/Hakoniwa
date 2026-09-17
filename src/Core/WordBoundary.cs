using System;

namespace Hakoniwa.Core;

public static class WordBoundary
{
    public static int Left(string text, int caret)
    {
        int i = Math.Max(0, Math.Min(caret, text.Length));
        if (i == 0)
            return 0;
        if (TryTag(text, i - 1, out int start, out _))
            return start;

        int p = i - 1;
        var kind = Kind(text[p]);
        if (kind == CharKind.Space)
        {
            while (p > 0 && Kind(text[p - 1]) == CharKind.Space)
                p--;
            return p == 0 ? 0 : Left(text, p);
        }

        if (kind == CharKind.Cjk)
            return p;

        while (p > 0 && Kind(text[p - 1]) == kind && !TryTag(text, p - 1, out _, out _))
            p--;
        return p;
    }

    public static int Right(string text, int caret)
    {
        int n = text.Length;
        int i = Math.Max(0, Math.Min(caret, n));
        if (i >= n)
            return n;
        if (TryTag(text, i, out _, out int end))
            i = end;
        else
        {
            var kind = Kind(text[i]);
            if (kind == CharKind.Cjk)
                i++;
            else
            {
                i++;
                while (i < n && Kind(text[i]) == kind && !TryTag(text, i, out _, out _))
                    i++;
            }
        }

        while (i < n && Kind(text[i]) == CharKind.Space)
            i++;
        return i;
    }

    public static bool TryTag(string text, int index, out int start, out int end)
    {
        start = end = index;
        if ((uint)index >= (uint)text.Length)
            return false;
        int open = text.LastIndexOf('[', index);
        if (open < 0 || open + 1 >= text.Length || !char.IsLetter(text[open + 1]))
            return false;
        int close = text.IndexOf(']', open + 1);
        if (close < 0 || index > close)
            return false;
        start = open;
        end = close + 1;
        return true;
    }

    private static CharKind Kind(char c)
    {
        if (char.IsWhiteSpace(c))
            return CharKind.Space;
        if (IsCjk(c))
            return CharKind.Cjk;
        if (char.IsLetterOrDigit(c) || c == '_')
            return CharKind.Latin;
        return CharKind.Other;
    }

    private static bool IsCjk(char c) =>
        c is (>= '\u3040' and <= '\u30FF')
            or (>= '\u3400' and <= '\u9FFF')
            or (>= '\uAC00' and <= '\uD7AF')
            or (>= '\uF900' and <= '\uFAFF');

    private enum CharKind { Space, Latin, Cjk, Other }
}
