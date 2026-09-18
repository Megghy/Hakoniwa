using System;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public static class SelectionOverlay
{
    private const float Handle = 4f;
    private const float Icon = 22f;
    private const float Gap = 4f;
    private const float EdgePad = 6f;

    private enum Part { None, Inside, N, S, E, W, NE, NW, SE, SW }
    private enum Drag { None, Create, Move, Resize, MoveTiles }
    private enum Edge { Top, Right, Bottom, Left, TopRight, Bar }
    private enum Act { Copy, Cut, Paste, Save, FlipH, FlipV, Rotate, Fill, Replace, CopyMove, CutMove, Delete, Cancel, SkipAir, Confirm, CancelPaste }

    private static readonly (Act Id, Edge Edge, string Icon, string Tip)[] Actions =
    [
        (Act.Copy, Edge.Left, Icons.Copy, "复制"),
        (Act.Cut, Edge.Left, Icons.Cut, "剪切"),
        (Act.Paste, Edge.Left, Icons.Clipboard, "粘贴"),
        (Act.Save, Edge.Left, Icons.Archive, "存为蓝图"),
        (Act.Delete, Edge.Left, Icons.Trash, "删除"),
        (Act.FlipH, Edge.Right, Icons.FlipH, "水平翻转"),
        (Act.FlipV, Edge.Right, Icons.FlipV, "垂直翻转"),
        (Act.Rotate, Edge.Right, Icons.Reload, "旋转 90°"),
        (Act.Fill, Edge.Bottom, Icons.Colors, "填充选区\n用手持物块填满"),
        (Act.Replace, Edge.Bottom, Icons.Reload, "替换模式\n点击选区内物块，用持有物块替换同类"),
        (Act.SkipAir, Edge.Left, Icons.Earth, "跳过空气\n开启后空气格保留地图原有内容"),
        (Act.CopyMove, Edge.TopRight, Icons.SectionCopy, "复制并移动"),
        (Act.CutMove, Edge.TopRight, Icons.Move, "剪切并移动"),
        (Act.Cancel, Edge.TopRight, Icons.Close, "取消选区"),
        (Act.Confirm, Edge.Bar, "", "确认写入地图"),
        (Act.CancelPaste, Edge.Bar, "", "取消粘贴"),
    ];

    private static Drag _drag;
    private static Part _resize;
    private static int _anchorX, _anchorY, _srcX, _srcY;
    private static int _minX, _minY, _maxX, _maxY;
    private static bool _left;
    private static bool _right;
    private static double _lastClick;
    private static Num.Vector2 _lastClickPos;
    private static readonly Num.Vector2[] BtnMin = new Num.Vector2[Actions.Length];
    private static readonly Num.Vector2[] BtnMax = new Num.Vector2[Actions.Length];
    private static Schematic? _payload;
    private static bool _cutMove;
    private static bool _drawPaste;
    private static string? _tip;

    public static bool OverrideCursor { get; private set; }

    public static void TriggerBrushHud() => EditorOverlay.TriggerBrushHud();

    public static void ReleaseCursor()
    {
        OverrideCursor = false;
        CheatHooks.HideVanillaCursor = false;
    }

    public static bool ShouldBlock()
    {
        if (Main.gameMenu || Main.mapFullscreen || CheatState.WaitingSelectKey || !FocusHelper.AllowInputProcessing)
            return false;
        if (EditorSession.Pasting || CheatState.SelectHeld(Keyboard.GetState()) || _drag != Drag.None)
            return true;
        if (!EditorSession.Selection.Active)
            return false;
        var sp = MouseScreen();
        return HitPart(sp) != Part.None;
    }

    public static bool Update(bool allowPress = true)
    {
        if (!EditorSession.Selection.Active)
        {
            EditorSession.ReplaceMode = false;
            if (EditorSession.Pasting)
                EditorSession.CancelPaste();
        }
        if (Main.gameMenu || Main.mapFullscreen || !FocusHelper.AllowInputProcessing)
        {
            _drag = Drag.None;
            _left = true;
            _right = true;
            ReleaseCursor();
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
            _right = Mouse.GetState().RightButton == ButtonState.Pressed;
            ReleaseCursor();
            return false;
        }

        if (left && _drag != Drag.None)
            ContinueDrag(tx, ty);
        else if (!left && _left && _drag == Drag.MoveTiles)
            FinishMove();
        if (!left)
            _drag = Drag.None;

        if (allowPress && left && !_left)
            OnPress(sp, tx, ty, mod);

        bool right = Mouse.GetState().RightButton == ButtonState.Pressed;
        if (allowPress && right && !_right && HitButton(sp) < 0 && (EditorSession.Selection.Active || EditorSession.Pasting))
        {
            EditorSession.CancelPaste();
            EditorSession.ReplaceMode = false;
            EditorSession.Selection.Clear();
            _drag = Drag.None;
        }

        _left = left;
        _right = right;
        OverrideCursor = _drag is Drag.Move or Drag.Resize or Drag.MoveTiles
            || allowPress && EditorSession.Selection.Active && (HitPart(sp) != Part.None || HitButton(sp) >= 0);
        CheatHooks.HideVanillaCursor = OverrideCursor;
        return ShouldBlock();
    }

    public static void Draw()
    {
        if (Main.gameMenu || Main.mapFullscreen || !FocusHelper.AllowInputProcessing)
            return;

        EditorOverlay.Draw();

        var sel = EditorSession.Selection;
        if (sel.Active)
        {
            ScreenRect(out var min, out var max);
            var list = Ui.WorldList;
            uint fill = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.33f, 0.55f, 0.95f, 0.18f));
            uint line = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.55f, 0.75f, 1f, 0.95f));
            DrawShape(list, min, max, fill, line, sel.Shape);
            if (!EditorSession.Pasting)
                DrawHandles(list, min, max, line);
            DrawButtons(min, max);
            int cells = ToolEngine.CountRectShape(sel.MinX, sel.MinY, sel.MaxX, sel.MaxY, sel.Shape);
            EditorOverlay.DrawEdgeChip(min, max, $"{sel.Width} × {sel.Height}  ·  {cells} 格");
            SetCursor();
            if (_tip is not null)
                ImGui.SetTooltip(_tip);
        }
    }

    private static void OnPress(Num.Vector2 sp, int tx, int ty, bool mod)
    {
        var sel = EditorSession.Selection;
        if (!sel.Active)
        {
            if (!mod)
                return;
            sel.Begin(tx, ty, EditorSession.SelectionShape);
            _drag = Drag.Create;
            return;
        }

        if (HitButton(sp) >= 0)
            return;

        if (EditorSession.Pasting)
        {
            if (HitPart(sp) == Part.Inside)
            {
                _anchorX = tx;
                _anchorY = ty;
                _drag = Drag.Move;
            }
            return;
        }

        var part = HitPart(sp);
        if (part is >= Part.N and <= Part.SW)
        {
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
            if (EditorSession.ReplaceMode)
            {
                EditorSession.ReplaceSelectionAt(tx, ty);
                return;
            }
            _anchorX = tx;
            _anchorY = ty;
            _drag = Drag.Move;
            return;
        }

        double now = Main.gameTimeCache.TotalGameTime.TotalSeconds;
        bool dbl = now - _lastClick < 0.35 && Num.Vector2.Distance(sp, _lastClickPos) < 8f;
        _lastClick = now;
        _lastClickPos = sp;
        if (dbl)
            sel.Clear();
        else
            Notices.Post("已有选区，请先取消或双击外部");
    }

    private static void BeginHoldMove(int tx, int ty, bool cut)
    {
        if (!EditorSession.TryCopy())
            return;
        _payload = EditorSession.Clipboard;
        _srcX = Math.Max(0, EditorSession.Selection.MinX);
        _srcY = Math.Max(0, EditorSession.Selection.MinY);
        _anchorX = tx;
        _anchorY = ty;
        _cutMove = cut;
        _drag = Drag.MoveTiles;
    }

    private static void ContinueDrag(int tx, int ty)
    {
        if (_drag == Drag.Create)
            EditorSession.Selection.DragTo(tx, ty);
        else if (_drag is Drag.Move or Drag.MoveTiles)
        {
            EditorSession.Selection.Offset(tx - _anchorX, ty - _anchorY);
            _anchorX = tx;
            _anchorY = ty;
        }
        else if (_drag == Drag.Resize)
            ResizeTo(tx, ty);
    }

    private static void FinishMove()
    {
        if (_payload is null)
            return;
        var sel = EditorSession.Selection;
        if (sel.MinX != _srcX || sel.MinY != _srcY)
            EditorSession.DropMove(_payload, _srcX, _srcY, _cutMove);
        _payload = null;
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
        switch (Actions[i].Id)
        {
            case Act.Copy: EditorSession.Copy(); break;
            case Act.Cut: EditorSession.Cut(); break;
            case Act.Paste: EditorSession.BeginPaste(); break;
            case Act.Save: HakoniwaUi.SaveSelectionAsSchematic(); break;
            case Act.FlipH: EditorSession.FlipHorizontal(); break;
            case Act.FlipV: EditorSession.FlipVertical(); break;
            case Act.Rotate: EditorSession.Rotate90(); break;
            case Act.Fill: EditorSession.FillSelection(); break;
            case Act.Confirm: EditorSession.CommitPaste(); break;
            case Act.CancelPaste: EditorSession.CancelPaste(); break;
            case Act.Delete: EditorSession.Delete(); break;
            case Act.Cancel:
                EditorSession.ReplaceMode = false;
                EditorSession.Selection.Clear();
                break;
        }
    }

    private static void DrawShape(ImDrawListPtr list, Num.Vector2 min, Num.Vector2 max, uint fill, uint line, BrushShape shape)
    {
        var c = (min + max) * 0.5f;
        var r = (max - min) * 0.5f;
        if (shape == BrushShape.Circle)
        {
            list.AddEllipseFilled(c, r, fill);
            list.AddEllipse(c, r, line, 0, 32, 2f);
            return;
        }

        if (shape == BrushShape.Diamond)
        {
            var n = new Num.Vector2(c.X, min.Y);
            var e = new Num.Vector2(max.X, c.Y);
            var s = new Num.Vector2(c.X, max.Y);
            var w = new Num.Vector2(min.X, c.Y);
            list.AddQuadFilled(n, e, s, w, fill);
            list.AddQuad(n, e, s, w, line, 2f);
            return;
        }

        list.AddRectFilled(min, max, fill);
        list.AddRect(min, max, line, 0f, ImDrawFlags.None, 2f);
    }

    private static void DrawHandles(ImDrawListPtr list, Num.Vector2 min, Num.Vector2 max, uint line)
    {
        var sp = MouseScreen();
        var part = HitPart(sp);
        DrawHandle(list, min, line, part == Part.NW);
        DrawHandle(list, new Num.Vector2(max.X, min.Y), line, part == Part.NE);
        DrawHandle(list, new Num.Vector2(min.X, max.Y), line, part == Part.SW);
        DrawHandle(list, max, line, part == Part.SE);
        DrawHandle(list, new Num.Vector2((min.X + max.X) * 0.5f, min.Y), line, part == Part.N);
        DrawHandle(list, new Num.Vector2((min.X + max.X) * 0.5f, max.Y), line, part == Part.S);
        DrawHandle(list, new Num.Vector2(min.X, (min.Y + max.Y) * 0.5f), line, part == Part.W);
        DrawHandle(list, new Num.Vector2(max.X, (min.Y + max.Y) * 0.5f), line, part == Part.E);
    }

    private static void DrawHandle(ImDrawListPtr list, Num.Vector2 c, uint color, bool hover)
    {
        float r = hover ? 5.5f : Handle;
        list.AddCircleFilled(c, r, hover ? 0xFF38BDF8 : 0xFF1A162B, 16);
        list.AddCircle(c, r, color, 16, hover ? 2f : 1.5f);
    }

    private static void DrawButtons(Num.Vector2 min, Num.Vector2 max)
    {
        _drawPaste = EditorSession.Pasting;
        var display = ImGui.GetIO().DisplaySize;
        PlaceEdge(Edge.Top, min, max, display);
        PlaceEdge(Edge.Bottom, min, max, display);
        PlaceEdge(Edge.Left, min, max, display);
        PlaceEdge(Edge.Right, min, max, display);
        PlaceEdge(Edge.TopRight, min, max, display);
        PlaceEdge(Edge.Bar, min, max, display);
        _tip = null;
        for (int i = 0; i < Actions.Length; i++)
        {
            if (Shown(Actions[i].Id))
                DrawButton(i);
        }
    }

    private static bool Shown(Act id) => _drawPaste
        ? id is Act.FlipH or Act.FlipV or Act.Rotate or Act.SkipAir or Act.Confirm or Act.CancelPaste
        : id is not (Act.SkipAir or Act.Confirm or Act.CancelPaste);

    private static void DrawButton(int i)
    {
        var id = Actions[i].Id;
        bool big = id is Act.Confirm or Act.CancelPaste;
        var size = big ? BtnMax[i] - BtnMin[i] : new Num.Vector2(Icon, Icon);
        if (!Ui.BeginChrome($"##selAct{i}", BtnMin[i], size))
            return;
        bool move = id is Act.CopyMove or Act.CutMove;
        bool on = id == Act.Replace && EditorSession.ReplaceMode
            || id == Act.SkipAir && EditorSession.PasteSkipAir
            || _drag == Drag.MoveTiles && (id == (_cutMove ? Act.CutMove : Act.CopyMove));
        Ui.Invisible("##a", BtnMin[i], size);
        bool hover = ImGui.IsItemHovered();
        if (id == Act.Replace && ImGui.IsItemClicked())
            EditorSession.ReplaceMode = !EditorSession.ReplaceMode;
        else if (id == Act.SkipAir && ImGui.IsItemClicked())
            EditorSession.PasteSkipAir = !EditorSession.PasteSkipAir;
        else if (move && !EditorSession.ReplaceMode && ImGui.IsItemActive() && _drag != Drag.MoveTiles)
            BeginHoldMove((int)(Main.MouseWorld.X / 16f), (int)(Main.MouseWorld.Y / 16f), id == Act.CutMove);
        else if (!move && id is not (Act.Replace or Act.SkipAir) && ImGui.IsItemClicked())
            Invoke(i);
        if (hover)
            _tip = Actions[i].Tip;

        var dl = ImGui.GetWindowDrawList();
        float round = big ? 6f : 11f;
        dl.AddRectFilled(BtnMin[i], BtnMax[i], hover || on ? Ui.ChipHover : Ui.ChipIdle, round);
        dl.AddRect(BtnMin[i], BtnMax[i], hover || on || id == Act.Confirm ? Ui.ChipOn : Ui.ChipLine, round);
        if (big)
        {
            string label = id == Act.Confirm ? "确认" : "取消";
            var ts = ImGui.CalcTextSize(label);
            dl.AddText((BtnMin[i] + BtnMax[i] - ts) * 0.5f, 0xFFF1F5F9, label);
        }
        else
            Icons.DrawDirect(dl, (BtnMin[i] + BtnMax[i]) * 0.5f, Actions[i].Icon);
        Ui.EndChrome();
    }

    private static void PlaceEdge(Edge edge, Num.Vector2 min, Num.Vector2 max, Num.Vector2 display)
    {
        int count = 0;
        for (int i = 0; i < Actions.Length; i++)
        {
            if (Actions[i].Edge == edge && Shown(Actions[i].Id))
                count++;
        }
        if (count == 0)
            return;

        const float barW = 80f;
        const float barH = 28f;
        const float barGap = 8f;
        float cell = edge == Edge.Bar ? barW : Icon;
        float gap = edge == Edge.Bar ? barGap : Gap;
        float span = count * cell + (count - 1) * gap;
        bool vertical = edge is Edge.Left or Edge.Right;
        float x, y;
        if (edge == Edge.Bar)
        {
            x = Math.Max(4f, Math.Min((min.X + max.X - span) * 0.5f, display.X - span - 4f));
            y = max.Y + EdgePad + 4f;
            if (y + barH > display.Y - 4f)
                y = min.Y - barH - EdgePad;
            y = Math.Max(4f, Math.Min(y, display.Y - barH - 4f));
        }
        else if (edge == Edge.TopRight)
        {
            x = max.X - span;
            y = min.Y - Icon - EdgePad;
            if (y < 4f) y = max.Y + EdgePad;
            if (y + Icon > display.Y - 4f) y = min.Y + 4f;
            x = Math.Max(4f, Math.Min(x, display.X - span - 4f));
        }
        else if (vertical)
        {
            y = Math.Max(4f, Math.Min((min.Y + max.Y - span) * 0.5f, display.Y - span - 4f));
            x = edge == Edge.Left ? min.X - Icon - EdgePad : max.X + EdgePad;
            if (x < 4f) x = min.X + 4f;
            if (x + Icon > display.X - 4f) x = max.X - Icon - 4f;
        }
        else
        {
            x = Math.Max(4f, Math.Min((min.X + max.X - span) * 0.5f, display.X - span - 4f));
            y = edge == Edge.Top ? min.Y - Icon - EdgePad : max.Y + EdgePad;
            if (y < 4f) y = max.Y + EdgePad;
            if (y + Icon > display.Y - 4f) y = min.Y - Icon - EdgePad;
        }

        for (int i = 0; i < Actions.Length; i++)
        {
            if (Actions[i].Edge != edge || !Shown(Actions[i].Id))
                continue;
            BtnMin[i] = new Num.Vector2(x, y);
            BtnMax[i] = BtnMin[i] + new Num.Vector2(cell, edge == Edge.Bar ? barH : Icon);
            if (vertical) y += cell + gap;
            else x += cell + gap;
        }
    }

    private static void SetCursor()
    {
        switch (HitPart(MouseScreen()))
        {
            case Part.N or Part.S: ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNs); break;
            case Part.E or Part.W: ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeEw); break;
            case Part.NE or Part.SW: ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNesw); break;
            case Part.NW or Part.SE: ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeNwse); break;
            case Part.Inside:
                if (!EditorSession.ReplaceMode)
                    ImGui.SetMouseCursor(ImGuiMouseCursor.ResizeAll);
                break;
        }
    }

    private static int HitButton(Num.Vector2 sp)
    {
        if (!EditorSession.Selection.Active)
            return -1;
        for (int i = 0; i < Actions.Length; i++)
        {
            if (!Shown(Actions[i].Id))
                continue;
            if (sp.X >= BtnMin[i].X && sp.X <= BtnMax[i].X && sp.Y >= BtnMin[i].Y && sp.Y <= BtnMax[i].Y)
                return i;
        }
        return -1;
    }

    private static Part HitPart(Num.Vector2 sp)
    {
        if (!EditorSession.Selection.Active)
            return Part.None;
        ScreenRect(out var min, out var max);
        float m = Handle + 4f;
        bool nearN = Math.Abs(sp.Y - min.Y) <= m && sp.X >= min.X - m && sp.X <= max.X + m;
        bool nearS = Math.Abs(sp.Y - max.Y) <= m && sp.X >= min.X - m && sp.X <= max.X + m;
        bool nearW = Math.Abs(sp.X - min.X) <= m && sp.Y >= min.Y - m && sp.Y <= max.Y + m;
        bool nearE = Math.Abs(sp.X - max.X) <= m && sp.Y >= min.Y - m && sp.Y <= max.Y + m;
        bool inside = sp.X >= min.X && sp.X <= max.X && sp.Y >= min.Y && sp.Y <= max.Y;
        if (EditorSession.Pasting)
            return inside ? Part.Inside : Part.None;
        if (nearN && nearW) return Part.NW;
        if (nearN && nearE) return Part.NE;
        if (nearS && nearW) return Part.SW;
        if (nearS && nearE) return Part.SE;
        if (nearN) return Part.N;
        if (nearS) return Part.S;
        if (nearW) return Part.W;
        if (nearE) return Part.E;
        if (inside)
            return Part.Inside;
        return Part.None;
    }

    private static void ScreenRect(out Num.Vector2 min, out Num.Vector2 max)
    {
        var sel = EditorSession.Selection;
        WorldTileRect(sel.MinX, sel.MinY, sel.MaxX + 1, sel.MaxY + 1, out min, out max);
    }

    private static void WorldTileRect(int x0, int y0, int x1, int y1, out Num.Vector2 min, out Num.Vector2 max)
    {
        min = WorldToScreen(new Vector2(x0 * 16, y0 * 16));
        max = WorldToScreen(new Vector2(x1 * 16, y1 * 16));
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
