using System;
using System.Numerics;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Tools;
using Hexa.NET.ImGui;
using Terraria;

namespace Hakoniwa.UI;

public sealed class EditorToolbar
{
    private const float Cell = 30f;
    private const float Grip = 16f;
    private const float Pad = 4f;
    private const float Margin = 8f;
    private const float Snap = 28f;

    private static readonly (EditorTool Tool, string Icon, string Tip, bool Shapes)[] Tools =
    [
        (EditorTool.Marquee, Icons.Crop, "选区", true),
        (EditorTool.Brush, Icons.Brush, "笔刷", true),
        (EditorTool.Fill, Icons.Colors, "油漆桶", false),
        (EditorTool.Eraser, Icons.Eraser, "橡皮擦", true),
        (EditorTool.Eyedropper, Icons.Pencil, "吸管", false),
        (EditorTool.Replace, Icons.Reload, "替换\n左键替换，右键取样匹配源", false),
        (EditorTool.Shape, Icons.Section, "形状", true),
    ];

    private Vector2 _pos = new(Margin, 140f);
    private Vector2 _target = new(Margin, 140f);
    private bool _dragging;
    private bool _ready;

    public void Draw()
    {
        if (Main.gameMenu)
            return;

        float width = Pad * 2f + Cell;
        float height = Pad * 2f + Grip + 2f + Cell * Tools.Length;
        var display = ImGui.GetIO().DisplaySize;
        var size = new Vector2(width, height);
        if (!_ready)
        {
            if (CheatState.ToolbarX is float x && CheatState.ToolbarY is float y)
                _pos = _target = Dock(new Vector2(x, y), size, display);
            _ready = true;
        }
        if (!_dragging)
            _pos = Vector2.Lerp(_pos, _target, 0.28f);
        _pos.X = Math.Max(0f, Math.Min(_pos.X, display.X - width));
        _pos.Y = Math.Max(0f, Math.Min(_pos.Y, display.Y - height));

        if (!Ui.BeginChrome("##HakoniwaToolStrip", _pos, new Vector2(width, height)))
            return;

        var dl = ImGui.GetWindowDrawList();
        var wp = ImGui.GetWindowPos();
        dl.AddRectFilled(wp, wp + new Vector2(width, height), 0xF216141F, 8f);
        dl.AddRect(wp, wp + new Vector2(width, height), 0xFF3A3648, 8f);

        DrawGrip(dl, new Vector2(width, height), display);
        for (int i = 0; i < Tools.Length; i++)
            DrawTool(dl, i);

        Ui.EndChrome();
    }

    private void DrawGrip(ImDrawListPtr dl, Vector2 size, Vector2 display)
    {
        ImGui.SetCursorPos(new Vector2(Pad, Pad));
        ImGui.InvisibleButton("##grip", new Vector2(Cell, Grip));
        if (ImGui.IsItemActive())
        {
            _dragging = true;
            _pos += ImGui.GetIO().MouseDelta;
            _target = _pos;
        }
        else if (_dragging)
        {
            _dragging = false;
            _target = Dock(_pos, size, display);
            CheatState.ToolbarX = _target.X;
            CheatState.ToolbarY = _target.Y;
        }

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        Icons.DrawDirect(dl, (min + max) * 0.5f, Icons.Drag, 255, 0xFF8A8794);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("拖动工具条");
    }

    private static void DrawTool(ImDrawListPtr dl, int i)
    {
        var (tool, icon, tip, shapes) = Tools[i];
        ImGui.SetCursorPos(new Vector2(Pad, Pad + Grip + 2f + i * Cell));
        if (ImGui.InvisibleButton($"##tool{i}", new Vector2(Cell, Cell)))
            EditorSession.Tool = tool;

        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        bool on = EditorSession.Tool == tool;
        bool hover = ImGui.IsItemHovered();
        if (on || hover)
            dl.AddRectFilled(min, max, on ? 0xE02A2440 : 0x80221E2C, 5f);
        if (on)
            dl.AddRectFilled(min, new Vector2(min.X + 2f, max.Y), Ui.ChipOn, 1f);
        if (tool == EditorTool.Shape)
            DrawKindIcon(dl, (min + max) * 0.5f, on ? Ui.ChipOn : 0xFFD6D3E0);
        else
            Icons.DrawDirect(dl, (min + max) * 0.5f, icon);

        if (hover)
            ImGui.SetTooltip(tool == EditorTool.Shape
                ? $"形状 · {Ui.DrawNames[(int)EditorSession.DrawKind]}\n右键选择直线 / 矩形 / 圆形"
                : shapes ? $"{tip}\n右键选择形态" : tip);
        if (!shapes || !ImGui.BeginPopupContextItem($"##shape{i}"))
            return;

        if (tool == EditorTool.Shape)
        {
            for (int s = 0; s < Ui.DrawNames.Length; s++)
            {
                if (ImGui.Selectable(Ui.DrawNames[s], (int)EditorSession.DrawKind == s))
                    EditorSession.DrawKind = (DrawKind)s;
            }
        }
        else
        {
            ref var shape = ref tool == EditorTool.Marquee ? ref EditorSession.SelectionShape : ref EditorSession.BrushShape;
            for (int s = 0; s < Ui.ShapeNames.Length; s++)
            {
                if (ImGui.Selectable(Ui.ShapeNames[s], (int)shape == s))
                    shape = (BrushShape)s;
            }
        }

        ImGui.EndPopup();
    }

    private static void DrawKindIcon(ImDrawListPtr dl, Vector2 c, uint color)
    {
        switch (EditorSession.DrawKind)
        {
            case DrawKind.Line:
                dl.AddLine(c + new Vector2(-7f, 6f), c + new Vector2(7f, -6f), color, 1.6f);
                break;
            case DrawKind.Circle:
                dl.AddCircle(c, 8f, color, 20, 1.6f);
                break;
            default:
                dl.AddRect(c - new Vector2(7f, 7f), c + new Vector2(7f, 7f), color, 0f, ImDrawFlags.None, 1.6f);
                break;
        }
    }

    private static Vector2 Dock(Vector2 pos, Vector2 size, Vector2 display)
    {
        if (pos.X < Snap) pos.X = Margin;
        else if (pos.X + size.X > display.X - Snap) pos.X = display.X - size.X - Margin;
        if (pos.Y < Snap) pos.Y = Margin;
        else if (pos.Y + size.Y > display.Y - Snap) pos.Y = display.Y - size.Y - Margin;
        return pos;
    }
}
