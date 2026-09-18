using System;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Tools;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI;

internal static class EditorOverlay
{
    private static float _brushHudTimer;

    public static void TriggerBrushHud() => _brushHudTimer = 1.3f;

    public static void Draw()
    {
        if (Main.gameMenu || Main.mapFullscreen || !FocusHelper.AllowInputProcessing)
            return;
        DrawReplacePreview();
        DrawStrokePreview();
        DrawBrushPreview();
        DrawCursorHud();
        DrawStrokeHud();
        DrawBrushHud();
    }

    private static void DrawBrushPreview()
    {
        if (Ui.Mouse || EditorSession.Stroking)
            return;
        if (EditorSession.Tool is not (EditorTool.Brush or EditorTool.Eraser) &&
            (EditorSession.Tool != EditorTool.Shape || EditorSession.DrawKind != DrawKind.Line))
            return;

        EditorSession.CursorTile(out int cx, out int cy);
        DrawBrushCells(cx, cy, EditorSession.BrushRadius, EditorSession.BrushShape,
            EditorSession.Tool == EditorTool.Eraser ? 0x2E201018u : 0x2A10243Cu,
            EditorSession.Tool == EditorTool.Eraser ? 0xFF5B40F4u : 0xFFF8BD38u);
    }

    private static void DrawStrokePreview()
    {
        if (!EditorSession.Stroking || EditorSession.Tool != EditorTool.Shape)
            return;
        if (EditorSession.DrawKind is DrawKind.Rect or DrawKind.Circle)
        {
            var shape = EditorSession.DrawKind == DrawKind.Circle ? BrushShape.Circle : BrushShape.Square;
            int minX = Math.Min(EditorSession.StrokeX0, EditorSession.StrokeX1);
            int minY = Math.Min(EditorSession.StrokeY0, EditorSession.StrokeY1);
            int maxX = Math.Max(EditorSession.StrokeX0, EditorSession.StrokeX1);
            int maxY = Math.Max(EditorSession.StrokeY0, EditorSession.StrokeY1);
            DrawShapeCells(minX, minY, maxX, maxY, shape, 0x3310A5FAu, 0xFFF8BD38u);
            return;
        }

        int r = EditorSession.BrushRadius;
        var brush = EditorSession.BrushShape;
        ToolEngine.ForEachLineCell(EditorSession.StrokeX0, EditorSession.StrokeY0, EditorSession.StrokeX1, EditorSession.StrokeY1,
            (x, y) => DrawBrushCells(x, y, r, brush, 0x2A10243Cu, 0xFFF8BD38u));
    }

    private static void DrawReplacePreview()
    {
        if (EditorSession.Tool != EditorTool.Replace || !EditorSession.HasMatch)
            return;
        if (!EditorSession.ReplaceDomain(out int x, out int y, out int w, out int h, out var shape))
            return;
        EditorSession.VisibleTiles(out int vx, out int vy, out int vw, out int vh);
        int x0 = Math.Max(x, vx);
        int y0 = Math.Max(y, vy);
        int x1 = Math.Min(x + w - 1, vx + vw - 1);
        int y1 = Math.Min(y + h - 1, vy + vh - 1);
        var world = WorldTiles.Instance;
        var match = EditorSession.Match;
        var layers = EditorSession.Layers;
        var list = Ui.WorldList;
        for (int ty = y0; ty <= y1; ty++)
        {
            for (int tx = x0; tx <= x1; tx++)
            {
                if (!world.InBounds(tx, ty) || !ToolEngine.InRectShape(tx, ty, x, y, x + w - 1, y + h - 1, shape))
                    continue;
                if (!ToolEngine.Matches(world.Get(tx, ty), match, layers))
                    continue;
                WorldTileRect(tx, ty, tx + 1, ty + 1, out var min, out var max);
                list.AddRectFilled(min, max, 0x5538BDF8u);
                list.AddRect(min, max, 0xFFF8BD38u);
            }
        }
    }

    private static void DrawCursorHud()
    {
        if (Main.gameMenu || !Main.LocalPlayer.active || HakoniwaUi.ChatOpen)
            return;
        EditorSession.CursorTile(out int x, out int y);
        string text;
        if (!WorldTiles.Instance.InBounds(x, y))
        {
            text = $"{x}, {y}";
        }
        else
        {
            var tile = WorldTiles.Instance.Get(x, y);
            string wires = (tile.RedWire ? "R" : "") + (tile.BlueWire ? "B" : "") + (tile.GreenWire ? "G" : "") + (tile.YellowWire ? "Y" : "");
            if (wires.Length == 0)
                wires = "-";
            text = $"{x}, {y}  物块 {tile.TileType}  墙 {tile.WallType}  帧 {tile.TileFrameX},{tile.TileFrameY}  漆 {tile.Color}/{tile.WallColor}  液 {tile.Liquid}  线 {wires}";
        }

        var size = ImGui.CalcTextSize(text) + new Num.Vector2(16f, 10f);
        var display = ImGui.GetIO().DisplaySize;
        var pos = new Num.Vector2(8f, display.Y - size.Y - 8f);
        if (!Ui.BeginChrome("##tileHud", pos, size))
            return;
        var dl = ImGui.GetWindowDrawList();
        dl.AddRectFilled(pos, pos + size, 0xF216141F);
        dl.AddRect(pos, pos + size, 0xFF3A3648);
        dl.AddText(pos + new Num.Vector2(8f, 5f), 0xFFE2E8F0, text);
        Ui.EndChrome();
    }

    private static void DrawStrokeHud()
    {
        if (!EditorSession.Stroking || EditorSession.Tool != EditorTool.Shape)
            return;
        int w = Math.Abs(EditorSession.StrokeX1 - EditorSession.StrokeX0) + 1;
        int h = Math.Abs(EditorSession.StrokeY1 - EditorSession.StrokeY0) + 1;
        int n = EditorSession.DrawKind switch
        {
            DrawKind.Line => ToolEngine.CountLine(EditorSession.StrokeX0, EditorSession.StrokeY0, EditorSession.StrokeX1, EditorSession.StrokeY1, EditorSession.BrushRadius, EditorSession.BrushShape),
            DrawKind.Circle => ToolEngine.CountRectShape(EditorSession.StrokeX0, EditorSession.StrokeY0, EditorSession.StrokeX1, EditorSession.StrokeY1, BrushShape.Circle),
            _ => ToolEngine.CountRectShape(EditorSession.StrokeX0, EditorSession.StrokeY0, EditorSession.StrokeX1, EditorSession.StrokeY1, BrushShape.Square),
        };
        DrawMouseChip($"{Ui.DrawNames[(int)EditorSession.DrawKind]}  {w} × {h}", $"{n} 格");
    }

    internal static void DrawEdgeChip(Num.Vector2 min, Num.Vector2 max, string text)
    {
        var size = ImGui.CalcTextSize(text) + new Num.Vector2(14f, 8f);
        var display = ImGui.GetIO().DisplaySize;
        var pos = new Num.Vector2((min.X + max.X - size.X) * 0.5f, max.Y + 28f);
        if (pos.Y + size.Y > display.Y - 4f)
            pos.Y = min.Y - size.Y - 28f;
        pos.X = Math.Max(4f, Math.Min(pos.X, display.X - size.X - 4f));
        pos.Y = Math.Max(4f, Math.Min(pos.Y, display.Y - size.Y - 4f));
        var list = Ui.WorldList;
        list.AddRectFilled(pos, pos + size, 0xF216141F);
        list.AddRect(pos, pos + size, 0xFF38BDF8);
        list.AddText(pos + new Num.Vector2(7f, 4f), 0xFFF1F5F9, text);
    }

    private static void DrawBrushHud()
    {
        if (EditorSession.Stroking || _brushHudTimer <= 0f)
            return;
        _brushHudTimer -= Math.Max(1f / 60f, (float)Main.gameTimeCache.ElapsedGameTime.TotalSeconds);
        float alpha = Math.Min(1f, _brushHudTimer / 0.25f);
        if (alpha <= 0f)
            return;
        int cells = ToolEngine.CountBrush(EditorSession.BrushRadius, EditorSession.BrushShape);
        bool isEraser = EditorSession.Tool == EditorTool.Eraser;
        DrawMouseChip(
            $"{(isEraser ? "橡皮擦" : "笔刷")} · {Ui.ShapeNames[(int)EditorSession.BrushShape]}",
            $"半径 {EditorSession.BrushRadius} · {cells} 格",
            isEraser ? 0x00F87171u : 0x0038BDF8u,
            alpha);
    }

    private static void DrawMouseChip(string title, string detail, uint accentRgb = 0x0038BDF8u, float alpha = 1f)
    {
        var titleSize = ImGui.CalcTextSize(title);
        var detailSize = ImGui.CalcTextSize(detail);
        var hudSize = new Num.Vector2(Math.Max(titleSize.X, detailSize.X) + 16f, 44f);
        var mouse = ImGui.GetIO().MousePos;
        var display = ImGui.GetIO().DisplaySize;
        var pos = mouse + new Num.Vector2(18f, 18f);
        if (pos.X + hudSize.X > display.X - 10f) pos.X = mouse.X - hudSize.X - 12f;
        if (pos.Y + hudSize.Y > display.Y - 10f) pos.Y = mouse.Y - hudSize.Y - 12f;

        var list = ImGui.GetForegroundDrawList();
        byte aByte = (byte)(alpha * 255);
        uint bg = ((uint)(aByte * 240 / 255) << 24) | 0x000F111Eu;
        uint border = ((uint)(aByte * 220 / 255) << 24) | accentRgb;
        uint textCol = ((uint)aByte << 24) | 0x00F1F5F9u;
        uint accentCol = ((uint)aByte << 24) | accentRgb;
        list.AddRectFilled(pos, pos + hudSize, bg);
        list.AddRect(pos, pos + hudSize, border, 0f, ImDrawFlags.None, 1.5f);
        list.AddText(pos + new Num.Vector2(8f, 6f), textCol, title);
        list.AddText(pos + new Num.Vector2(8f, 24f), accentCol, detail);
    }

    private static void DrawShapeCells(int minX, int minY, int maxX, int maxY, BrushShape shape, uint fill, uint line)
    {
        EditorSession.VisibleTiles(out int vx, out int vy, out int vw, out int vh);
        int x0 = Math.Max(minX, vx);
        int y0 = Math.Max(minY, vy);
        int x1 = Math.Min(maxX, vx + vw - 1);
        int y1 = Math.Min(maxY, vy + vh - 1);
        var list = Ui.WorldList;
        for (int y = y0; y <= y1; y++)
        {
            for (int x = x0; x <= x1; x++)
            {
                if (!ToolEngine.InRectShape(x, y, minX, minY, maxX, maxY, shape))
                    continue;
                WorldTileRect(x, y, x + 1, y + 1, out var min, out var max);
                list.AddRectFilled(min, max, fill);
                if (!ToolEngine.InRectShape(x, y - 1, minX, minY, maxX, maxY, shape))
                    list.AddLine(min, new Num.Vector2(max.X, min.Y), line, 2f);
                if (!ToolEngine.InRectShape(x, y + 1, minX, minY, maxX, maxY, shape))
                    list.AddLine(new Num.Vector2(min.X, max.Y), max, line, 2f);
                if (!ToolEngine.InRectShape(x - 1, y, minX, minY, maxX, maxY, shape))
                    list.AddLine(min, new Num.Vector2(min.X, max.Y), line, 2f);
                if (!ToolEngine.InRectShape(x + 1, y, minX, minY, maxX, maxY, shape))
                    list.AddLine(new Num.Vector2(max.X, min.Y), max, line, 2f);
            }
        }
    }

    private static void DrawBrushCells(int cx, int cy, int r, BrushShape shape, uint fill, uint line)
    {
        var list = Ui.WorldList;
        for (int dy = -r; dy <= r; dy++)
        {
            for (int dx = -r; dx <= r; dx++)
            {
                if (!ToolEngine.InBrush(dx, dy, r, shape))
                    continue;
                WorldTileRect(cx + dx, cy + dy, cx + dx + 1, cy + dy + 1, out var min, out var max);
                list.AddRectFilled(min, max, fill);
                if (!ToolEngine.InBrush(dx, dy - 1, r, shape))
                    list.AddLine(min, new Num.Vector2(max.X, min.Y), line, 2f);
                if (!ToolEngine.InBrush(dx, dy + 1, r, shape))
                    list.AddLine(new Num.Vector2(min.X, max.Y), max, line, 2f);
                if (!ToolEngine.InBrush(dx - 1, dy, r, shape))
                    list.AddLine(min, new Num.Vector2(min.X, max.Y), line, 2f);
                if (!ToolEngine.InBrush(dx + 1, dy, r, shape))
                    list.AddLine(new Num.Vector2(max.X, min.Y), max, line, 2f);
            }
        }
    }

    private static void WorldTileRect(int x0, int y0, int x1, int y1, out Num.Vector2 min, out Num.Vector2 max)
    {
        min = WorldToScreen(new Vector2(x0 * 16, y0 * 16));
        max = WorldToScreen(new Vector2(x1 * 16, y1 * 16));
        if (max.X < min.X) (min.X, max.X) = (max.X, min.X);
        if (max.Y < min.Y) (min.Y, max.Y) = (max.Y, min.Y);
    }

    private static Num.Vector2 WorldToScreen(Vector2 world)
    {
        var screen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
        return new Num.Vector2(screen.X, screen.Y);
    }
}
