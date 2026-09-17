using System;
using System.Collections.Generic;

namespace Hakoniwa.Core;

public sealed class TextEditor
{
    private const int UndoCap = 64;

    public string Text { get; private set; } = "";
    public int Caret { get; private set; }
    public int Anchor { get; private set; }
    public bool HasSelection => Anchor != Caret;
    public int SelStart => Math.Min(Anchor, Caret);
    public int SelEnd => Math.Max(Anchor, Caret);
    public string Selected => HasSelection ? Text[SelStart..SelEnd] : "";

    private readonly List<(string Text, int Caret, int Anchor)> _undo = [];
    private int _at = -1;

    public void Bind(string text)
    {
        text ??= "";
        if (text == Text && Caret <= text.Length && Anchor <= text.Length)
            return;
        Text = text;
        Caret = Anchor = text.Length;
        _undo.Clear();
        _at = -1;
        Push();
    }

    public void Adopt(string text)
    {
        text ??= "";
        if (text == Text)
            return;
        Text = text;
        Caret = Clamp(Caret);
        Anchor = Clamp(Anchor);
        Push();
    }

    public void Insert(string value)
    {
        if (value.Length == 0 && !HasSelection)
            return;
        Replace(SelStart, SelEnd, value);
    }

    public void Backspace()
    {
        if (HasSelection)
        {
            Replace(SelStart, SelEnd, "");
            return;
        }

        if (Caret == 0)
            return;
        Replace(Caret - 1, Caret, "");
    }

    public void DeleteForward()
    {
        if (HasSelection)
        {
            Replace(SelStart, SelEnd, "");
            return;
        }

        if (Caret >= Text.Length)
            return;
        Replace(Caret, Caret + 1, "");
    }

    public void DeleteRange(int start, int end) => Replace(Clamp(start), Clamp(end), "");

    public void Move(int dir, bool select, bool word)
    {
        Caret = word
            ? (dir < 0 ? WordBoundary.Left(Text, Caret) : WordBoundary.Right(Text, Caret))
            : Clamp(Caret + dir);
        if (!select)
            Anchor = Caret;
    }

    public void DeleteWord(int dir)
    {
        if (HasSelection)
        {
            Replace(SelStart, SelEnd, "");
            return;
        }

        int to = dir < 0 ? WordBoundary.Left(Text, Caret) : WordBoundary.Right(Text, Caret);
        if (to != Caret)
            Replace(Math.Min(to, Caret), Math.Max(to, Caret), "");
    }

    public void Home(bool select)
    {
        int breakAt = Text.LastIndexOf('\n', Math.Max(0, Caret - 1));
        Caret = breakAt < 0 ? 0 : breakAt + 1;
        if (!select)
            Anchor = Caret;
    }

    public void End(bool select)
    {
        int breakAt = Text.IndexOf('\n', Caret);
        Caret = breakAt < 0 ? Text.Length : breakAt;
        if (!select)
            Anchor = Caret;
    }

    public void SelectAll()
    {
        Anchor = 0;
        Caret = Text.Length;
    }

    public void Click(int index, bool select)
    {
        Caret = Clamp(index);
        if (!select)
            Anchor = Caret;
    }

    public void Undo()
    {
        if (_at <= 0)
            return;
        Apply(_undo[--_at]);
    }

    public void Redo()
    {
        if (_at < 0 || _at >= _undo.Count - 1)
            return;
        Apply(_undo[++_at]);
    }

    private void Replace(int start, int end, string value)
    {
        if (end < start)
            (start, end) = (end, start);
        start = Clamp(start);
        end = Clamp(end);
        Text = Text[..start] + value + Text[end..];
        Caret = Anchor = start + value.Length;
        Push();
    }

    private void Push()
    {
        if (_at >= 0 && _at < _undo.Count - 1)
            _undo.RemoveRange(_at + 1, _undo.Count - _at - 1);
        if (_undo.Count > 0 && _undo[^1].Text == Text)
        {
            _undo[^1] = (Text, Caret, Anchor);
            _at = _undo.Count - 1;
            return;
        }

        _undo.Add((Text, Caret, Anchor));
        if (_undo.Count > UndoCap)
            _undo.RemoveAt(0);
        _at = _undo.Count - 1;
    }

    private void Apply((string Text, int Caret, int Anchor) snap)
    {
        Text = snap.Text;
        Caret = Clamp(snap.Caret);
        Anchor = Clamp(snap.Anchor);
    }

    private int Clamp(int i) => i < 0 ? 0 : i > Text.Length ? Text.Length : i;
}
