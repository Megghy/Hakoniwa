using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent.UI.Chat;
using Terraria.UI.Chat;

namespace Hakoniwa.Core;

internal static partial class NativeTextInput
{
    private readonly record struct HistLine(int Y, string Text);

    private static readonly List<HistLine> _hist = [];
    private static bool _mouseWasUp = true;
    private static bool _inputDrag;
    private static bool _histDrag;
    private static bool _histFocus;
    private static int _selLine0, _selCol0, _selLine1, _selCol1;

    private static void HandlePointer()
    {
        bool down = Mouse.GetState().LeftButton == ButtonState.Pressed;
        bool tap = down && _mouseWasUp;
        _mouseWasUp = !down;
        int mx = Main.mouseX;
        int my = Main.mouseY;
        bool shift = Main.keyState.PressingShift();

        if (Main.drawingPlayerChat)
        {
            CollectHist();
            HandleChatPointer(mx, my, down, tap, shift);
            return;
        }

        _histDrag = _histFocus = false;
        if (!Main.editChest)
        {
            _inputDrag = false;
            return;
        }

        float x = 504f;
        float y = Main.instance.invBottom;
        bool hit = mx >= x && my >= y && my <= y + 24;
        if (tap && hit)
        {
            _inputDrag = true;
            Editor.Click(IndexAt(Editor.Text, mx - x), shift);
            ShowCaret();
        }
        else if (_inputDrag && down && hit)
            Editor.Click(IndexAt(Editor.Text, mx - x), true);
        else if (!down)
            _inputDrag = false;
    }

    private static void HandleChatPointer(int mx, int my, bool down, bool tap, bool shift)
    {
        if (tap)
        {
            int caret = CaretAt(mx, my);
            if (caret >= 0)
            {
                _inputDrag = true;
                _histDrag = _histFocus = false;
                Editor.Click(caret, shift);
                ShowCaret();
                BlockMouse();
                return;
            }

            if (HitHist(mx, my, out int line, out int col))
            {
                _histDrag = _histFocus = true;
                _inputDrag = false;
                if (shift)
                {
                    _selLine1 = line;
                    _selCol1 = col;
                }
                else
                {
                    _selLine0 = _selLine1 = line;
                    _selCol0 = _selCol1 = col;
                }

                BlockMouse();
            }

            return;
        }

        if (_histDrag && down)
        {
            LocateHist(mx, my, out int line, out int col);
            _selLine1 = line;
            _selCol1 = col;
            BlockMouse();
            return;
        }

        if (_inputDrag && down)
        {
            int caret = CaretAt(mx, my);
            if (caret < 0)
                caret = my < Main.screenHeight - 40 ? 0 : Editor.Text.Length;
            Editor.Click(caret, true);
            BlockMouse();
            return;
        }

        if (!down)
            _histDrag = _inputDrag = false;
    }

    private static void CollectHist()
    {
        _hist.Clear();
        if (Main.chatMonitor is not RemadeChatMonitor monitor)
            return;

        int skip = monitor._startChatLine;
        int msg = 0;
        int line = 0;
        while (skip > 0 && msg < monitor._messages.Count)
        {
            int take = Math.Min(skip, monitor._messages[msg].LineCount);
            skip -= take;
            line += take;
            if (line == monitor._messages[msg].LineCount)
            {
                line = 0;
                msg++;
            }
        }

        int shown = 0;
        while (shown < monitor._showCount && msg < monitor._messages.Count)
        {
            var container = monitor._messages[msg];
            if (!container.Prepared || !Main.drawingPlayerChat && !container.CanBeShownWhenChatIsClosed)
                break;

            var snippets = container.GetSnippetWithInversedIndex(line);
            _hist.Add(new HistLine(Main.screenHeight - 58 - shown * 22, Flatten(snippets)));
            shown++;
            line++;
            if (line >= container.LineCount)
            {
                line = 0;
                msg++;
            }
        }
    }

    private static bool HitHist(int mx, int my, out int line, out int col)
    {
        line = col = 0;
        if (mx < 88)
            return false;
        for (int i = 0; i < _hist.Count; i++)
        {
            int y = _hist[i].Y;
            if (my >= y && my < y + 22)
            {
                line = i;
                col = IndexAt(_hist[i].Text, mx - 88f);
                return true;
            }
        }

        return false;
    }

    private static void LocateHist(int mx, int my, out int line, out int col)
    {
        if (HitHist(mx, my, out line, out col))
            return;
        if (_hist.Count == 0)
        {
            line = col = 0;
            return;
        }

        if (my >= _hist[0].Y + 22)
        {
            line = 0;
            col = mx < 88 ? 0 : _hist[0].Text.Length;
            return;
        }

        line = _hist.Count - 1;
        col = mx < 88 ? 0 : _hist[line].Text.Length;
    }

    private static int CaretAt(int mx, int my)
    {
        var lines = Editor.Text.Split('\n');
        int shown = lines.Length < 1 ? 1 : lines.Length > 4 ? 4 : lines.Length;
        int lineH = 22;
        int top = Main.screenHeight - 8 - shown * lineH;
        int barTop = top < Main.screenHeight - 36 ? top : Main.screenHeight - 36;
        if (mx < 88 || my < barTop || my > Main.screenHeight)
            return -1;
        int row = (my - top) / lineH;
        if (row < 0)
            row = 0;
        if (row >= shown)
            row = shown - 1;
        int consumed = 0;
        for (int i = 0; i < row; i++)
            consumed += (i < lines.Length ? lines[i].Length : 0) + 1;
        string line = row < lines.Length ? lines[row] : "";
        return consumed + IndexAt(line, mx - 88f);
    }

    private static void SelectAllHist()
    {
        if (_hist.Count == 0)
            return;
        _histFocus = true;
        _selLine0 = _hist.Count - 1;
        _selCol0 = 0;
        _selLine1 = 0;
        _selCol1 = _hist[0].Text.Length;
    }

    private static string HistSelected()
    {
        if (_hist.Count == 0)
            return "";
        Norm(out int topL, out int topC, out int botL, out int botC);
        if ((uint)topL >= (uint)_hist.Count || (uint)botL >= (uint)_hist.Count)
            return "";
        if (topL == botL && topC == botC)
            return _hist[topL].Text;

        var sb = new StringBuilder();
        for (int i = topL; i >= botL; i--)
        {
            string text = _hist[i].Text;
            int a = i == topL ? topC : 0;
            int b = i == botL ? botC : text.Length;
            if (a < 0)
                a = 0;
            if (b > text.Length)
                b = text.Length;
            if (b < a)
                (a, b) = (b, a);
            sb.Append(text[a..b]);
            if (i != botL)
                sb.Append('\n');
        }

        return sb.ToString();
    }

    private static void DrawHistSelection()
    {
        if (!_histFocus || _hist.Count == 0)
            return;
        Norm(out int topL, out int topC, out int botL, out int botC);
        for (int i = topL; i >= botL; i--)
        {
            if ((uint)i >= (uint)_hist.Count)
                continue;
            string text = _hist[i].Text;
            int a = i == topL ? topC : 0;
            int b = i == botL ? botC : text.Length;
            if (topL == botL && a == b)
            {
                a = 0;
                b = text.Length;
            }

            DrawLineSelection(new Vector2(88f, _hist[i].Y), text, 0, a, b);
        }
    }

    private static void Norm(out int topL, out int topC, out int botL, out int botC)
    {
        bool startOnTop = _selLine0 > _selLine1 || _selLine0 == _selLine1 && _selCol0 <= _selCol1;
        if (startOnTop)
        {
            topL = _selLine0;
            topC = _selCol0;
            botL = _selLine1;
            botC = _selCol1;
        }
        else
        {
            topL = _selLine1;
            topC = _selCol1;
            botL = _selLine0;
            botC = _selCol0;
        }
    }

    private static string Flatten(TextSnippet[] snippets)
    {
        var sb = new StringBuilder();
        foreach (var snippet in snippets)
            sb.Append(snippet.Text);
        return sb.ToString();
    }

    private static void BlockMouse()
    {
        Main.blockMouse = true;
        if (Main.LocalPlayer.active)
            Main.LocalPlayer.mouseInterface = true;
    }

    private static void ShowCaret()
    {
        Main.instance.textBlinkerState = 1;
        Main.instance.textBlinkerCount = 0;
    }
}
