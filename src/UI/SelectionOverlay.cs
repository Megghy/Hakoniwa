using System;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public static class SelectionOverlay
{
    private const float Handle = 7f;
    private const float BtnH = 22f;
    private const float BtnPad = 4f;
    private static readonly string[] Labels = ["复制", "剪切", "粘贴", "水平", "垂直", "旋转", "删除", "取消"];

    private enum Part { None, Inside, N, S, E, W, NE, NW, SE, SW }
    private enum Drag { None, Create, Move, Resize }

    private static Drag _drag;
    private static Part _resize;
    private static int _anchorX, _anchorY;
    private static int _minX, _minY, _maxX, _maxY;
    private static bool _left;
    private static int _pressBtn = -1;
    private static double _lastClick;
    private static Num.Vector2 _lastClickPos;
    private static Num.Vector2[] _btnMin = new Num.Vector2[Labels.Length];
    private static Num.Vector2[] _btnMax = new Num.Vector2[Labels.Length];

    public static bool ShouldBlock()
    {
        if (Main.gameMenu || Main.mapFullscreen || CheatState.WaitingSelectKey)
            return false;
        if (CheatState.SelectHeld(Keyboard.GetState()))
            return true;
        if (_drag != Drag.None)
            return true;
        if (!EditorSession.Selection.Active)
            return false;
        var sp = MouseScreen();
        return HitButton(sp) >= 0 || HitPart(sp) != Part.None;
    }

    public static bool Update()
    {
        if (Main.gameMenu || Main.mapFullscreen)
        {
            _drag = Drag.None;
            _left = false;
            return false;
        }

        bool left = Mouse.GetState().LeftButton == ButtonState.Pressed;
        var sp = MouseScreen();
        int tx = (int)(Main.MouseWorld.X / 16f);
        int ty = (int)(Main.MouseWorld.Y / 16f);
        bool mod = CheatState.SelectHeld(Keyboard.GetState());

        if (CheatState.WaitingSelectKey)
        {
            _left = left;
            return false;
        }

        if (left && _drag != Drag.None)
            ContinueDrag(tx, ty);
        else if (!left)
            _drag = Drag.None;

        if (left && !_left)
            OnPress(sp, tx, ty, mod);
        else if (!left && _left && _pressBtn >= 0)
        {
            if (HitButton(sp) == _pressBtn)
                Invoke(_pressBtn);
            _pressBtn = -1;
        }

        _left = left;
        return ShouldBlock();
    }

    public static void Draw()
    {
        var sel = EditorSession.Selection;
        if (!sel.Active || Main.gameMenu || Main.mapFullscreen)
            return;

        ScreenRect(out var min, out var max);
        var list = ImGui.GetBackgroundDrawList();
        uint fill = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.33f, 0.55f, 0.95f, 0.18f));
        uint line = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.55f, 0.75f, 1f, 0.95f));
        list.AddRectFilled(min, max, fill);
        list.AddRect(min, max, line, 0f, ImDrawFlags.None, 2f);
        DrawHandle(list, min, line);
        DrawHandle(list, new Num.Vector2(max.X, min.Y), line);
        DrawHandle(list, new Num.Vector2(min.X, max.Y), line);
        DrawHandle(list, max, line);
        DrawHandle(list, new Num.Vector2((min.X + max.X) * 0.5f, min.Y), line);
        DrawHandle(list, new Num.Vector2((min.X + max.X) * 0.5f, max.Y), line);
        DrawHandle(list, new Num.Vector2(min.X, (min.Y + max.Y) * 0.5f), line);
        DrawHandle(list, new Num.Vector2(max.X, (min.Y + max.Y) * 0.5f), line);
        DrawButtons(min, max);
    }

    private static void OnPress(Num.Vector2 sp, int tx, int ty, bool mod)
    {
        _pressBtn = -1;
        if (mod)
        {
            EditorSession.Selection.Begin(tx, ty);
            _drag = Drag.Create;
            return;
        }

        int btn = HitButton(sp);
        if (btn >= 0)
        {
            _pressBtn = btn;
            return;
        }

        var part = HitPart(sp);
        if (part is >= Part.N and <= Part.SW)
        {
            var sel = EditorSession.Selection;
            _minX = sel.MinX;
            _minY = sel.MinY;
            _maxX = sel.MaxX;
            _maxY = sel.MaxY;
            _resize = part;
            _drag = Drag.Resize;
            return;
        }

        if (part == Part.Inside)
        {
            _anchorX = tx;
            _anchorY = ty;
            _drag = Drag.Move;
            return;
        }

        double now = Main.gameTimeCache.TotalGameTime.TotalSeconds;
        if (now - _lastClick < 0.35 && Num.Vector2.Distance(sp, _lastClickPos) < 8f)
            EditorSession.Selection.Clear();
        _lastClick = now;
        _lastClickPos = sp;
    }

    private static void ContinueDrag(int tx, int ty)
    {
        if (_drag == Drag.Create)
            EditorSession.Selection.DragTo(tx, ty);
        else if (_drag == Drag.Move)
        {
            EditorSession.Selection.Offset(tx - _anchorX, ty - _anchorY);
            _anchorX = tx;
            _anchorY = ty;
        }
        else if (_drag == Drag.Resize)
            ResizeTo(tx, ty);
    }

    private static void ResizeTo(int tx, int ty)
    {
        int minX = _minX, minY = _minY, maxX = _maxX, maxY = _maxY;
        if (_resize is Part.W or Part.NW or Part.SW) minX = tx;
        if (_resize is Part.E or Part.NE or Part.SE) maxX = tx;
        if (_resize is Part.N or Part.NE or Part.NW) minY = ty;
        if (_resize is Part.S or Part.SE or Part.SW) maxY = ty;
        EditorSession.Selection.Set(minX, minY, maxX, maxY);
    }

    private static void Invoke(int i)
    {
        switch (i)
        {
            case 0: EditorSession.Copy(); break;
            case 1: EditorSession.Cut(); break;
            case 2: EditorSession.Paste(); break;
            case 3: EditorSession.FlipHorizontal(); break;
            case 4: EditorSession.FlipVertical(); break;
            case 5: EditorSession.Rotate90(); break;
            case 6: EditorSession.Delete(); break;
            case 7: EditorSession.Selection.Clear(); break;
        }
    }

    private static void DrawButtons(Num.Vector2 min, Num.Vector2 max)
    {
        var display = ImGui.GetIO().DisplaySize;
        float width = 0f;
        var sizes = new Num.Vector2[Labels.Length];
        for (int i = 0; i < Labels.Length; i++)
        {
            sizes[i] = ImGui.CalcTextSize(Labels[i]) + new Num.Vector2(12f, 6f);
            sizes[i].Y = BtnH;
            width += sizes[i].X + BtnPad;
        }

        width -= BtnPad;
        float x = Math.Max(4f, Math.Min((min.X + max.X - width) * 0.5f, Math.Max(4f, display.X - width - 4f)));
        float y = min.Y - BtnH - 8f;
        if (y < 4f)
            y = Math.Min(display.Y - BtnH - 4f, max.Y + 8f);

        var fg = ImGui.GetForegroundDrawList();
        for (int i = 0; i < Labels.Length; i++)
        {
            var a = new Num.Vector2(x, y);
            var b = a + sizes[i];
            _btnMin[i] = a;
            _btnMax[i] = b;
            bool hover = HitButton(MouseScreen()) == i;
            fg.AddRectFilled(a, b, hover ? 0xF02A2440 : 0xE01A162B, 3f);
            fg.AddRect(a, b, hover ? 0xFF38BDF8 : 0xFF6D28D9, 3f);
            var text = ImGui.CalcTextSize(Labels[i]);
            fg.AddText(a + new Num.Vector2((sizes[i].X - text.X) * 0.5f, (BtnH - text.Y) * 0.5f), 0xFFF0F5FC, Labels[i]);
            x += sizes[i].X + BtnPad;
        }
    }

    private static void DrawHandle(ImDrawListPtr list, Num.Vector2 c, uint color)
    {
        list.AddRectFilled(c - new Num.Vector2(Handle, Handle), c + new Num.Vector2(Handle, Handle), 0xFF1A162B);
        list.AddRect(c - new Num.Vector2(Handle, Handle), c + new Num.Vector2(Handle, Handle), color, 0f, ImDrawFlags.None, 1.5f);
    }

    private static int HitButton(Num.Vector2 sp)
    {
        if (!EditorSession.Selection.Active)
            return -1;
        for (int i = 0; i < Labels.Length; i++)
        {
            if (sp.X >= _btnMin[i].X && sp.X <= _btnMax[i].X && sp.Y >= _btnMin[i].Y && sp.Y <= _btnMax[i].Y)
                return i;
        }
        return -1;
    }

    private static Part HitPart(Num.Vector2 sp)
    {
        if (!EditorSession.Selection.Active)
            return Part.None;
        ScreenRect(out var min, out var max);
        float m = Handle + 2f;
        bool nearN = Math.Abs(sp.Y - min.Y) <= m && sp.X >= min.X - m && sp.X <= max.X + m;
        bool nearS = Math.Abs(sp.Y - max.Y) <= m && sp.X >= min.X - m && sp.X <= max.X + m;
        bool nearW = Math.Abs(sp.X - min.X) <= m && sp.Y >= min.Y - m && sp.Y <= max.Y + m;
        bool nearE = Math.Abs(sp.X - max.X) <= m && sp.Y >= min.Y - m && sp.Y <= max.Y + m;
        if (nearN && nearW) return Part.NW;
        if (nearN && nearE) return Part.NE;
        if (nearS && nearW) return Part.SW;
        if (nearS && nearE) return Part.SE;
        if (nearN) return Part.N;
        if (nearS) return Part.S;
        if (nearW) return Part.W;
        if (nearE) return Part.E;
        if (sp.X >= min.X && sp.X <= max.X && sp.Y >= min.Y && sp.Y <= max.Y)
            return Part.Inside;
        return Part.None;
    }

    private static void ScreenRect(out Num.Vector2 min, out Num.Vector2 max)
    {
        var sel = EditorSession.Selection;
        min = WorldToScreen(new Vector2(sel.MinX * 16, sel.MinY * 16));
        max = WorldToScreen(new Vector2((sel.MaxX + 1) * 16, (sel.MaxY + 1) * 16));
        if (max.X < min.X) (min.X, max.X) = (max.X, min.X);
        if (max.Y < min.Y) (min.Y, max.Y) = (max.Y, min.Y);
    }

    private static Num.Vector2 MouseScreen()
    {
        var mouse = Mouse.GetState();
        var scale = PlayerInput.RawMouseScale;
        return new Num.Vector2(mouse.X * scale.X, mouse.Y * scale.Y);
    }

    private static Num.Vector2 WorldToScreen(Vector2 world)
    {
        var screen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
        return new Num.Vector2(screen.X, screen.Y);
    }
}
