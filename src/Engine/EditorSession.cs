using System;
using Hakoniwa.Core;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Terraria;

namespace Hakoniwa.Engine;

public enum EditorTool
{
    Marquee = 0,
    Brush = 1,
    Fill = 2,
    Eraser = 3,
}

public static class EditorSession
{
    public static readonly Selection Selection = new();
    public static readonly HistoryStack History = new();
    public static Schematic? Clipboard;
    public static EditorTool Tool;
    public static int BrushRadius = 3;
    public static BrushShape BrushShape = BrushShape.Circle;
    public static BrushShape SelectionShape = BrushShape.Square;

    public static void Delete()
    {
        if (!Selection.Active)
            return;
        ToolEngine.ClearRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, TileLayer.All, History, Selection.Shape);
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        Notices.Post($"已删除选区 {Selection.Width}x{Selection.Height}");
    }

    public static void Copy()
    {
        if (!TryCopy())
            return;
        Notices.Post($"已复制 {Selection.Width}x{Selection.Height}");
    }

    public static void Cut()
    {
        if (!TryCopy())
            return;
        ToolEngine.ClearRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, TileLayer.All, History, Selection.Shape);
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        Notices.Post($"已剪切 {Selection.Width}x{Selection.Height}");
    }

    public static void Paste()
    {
        if (Clipboard is null || !Main.LocalPlayer.active)
            return;
        Origin(out int x, out int y);
        ToolEngine.Paste(WorldTiles.Instance, Clipboard, x, y, TileLayer.All, History);
        WorldTiles.Refresh(x, y, Clipboard.Width, Clipboard.Height);
        Notices.Post($"已粘贴 {Clipboard.Width}x{Clipboard.Height}");
    }

    public static void DropMove(Schematic payload, int srcX, int srcY, bool cut)
    {
        Origin(out int dstX, out int dstY);
        ToolEngine.Relocate(WorldTiles.Instance, payload, srcX, srcY, dstX, dstY, cut, TileLayer.All, History);
        WorldTiles.Refresh(srcX, srcY, payload.Width, payload.Height);
        WorldTiles.Refresh(dstX, dstY, payload.Width, payload.Height);
        Notices.Post(cut ? "已移动选区" : "已复制并移动选区");
    }

    public static void FlipHorizontal()
    {
        if (Clipboard is null)
            return;
        Clipboard = TransformEngine.FlipHorizontal(Clipboard);
        Notices.Post("剪贴板已水平翻转");
    }

    public static void FlipVertical()
    {
        if (Clipboard is null)
            return;
        Clipboard = TransformEngine.FlipVertical(Clipboard);
        Notices.Post("剪贴板已垂直翻转");
    }

    public static void Rotate90()
    {
        if (Clipboard is null)
            return;
        Clipboard = TransformEngine.Rotate90Clockwise(Clipboard);
        Notices.Post("剪贴板已旋转 90°");
    }

    public static void Undo()
    {
        if (!History.Undo(WorldTiles.Instance, out int x, out int y, out int w, out int h))
            return;
        WorldTiles.Refresh(x, y, w, h);
        Notices.Post("已撤销");
    }

    public static void Redo()
    {
        if (!History.Redo(WorldTiles.Instance, out int x, out int y, out int w, out int h))
            return;
        WorldTiles.Refresh(x, y, w, h);
        Notices.Post("已重做");
    }

    public static bool TryCopy()
    {
        if (!Selection.Active)
            return false;
        var world = WorldTiles.Instance;
        int x1 = Math.Max(0, Selection.MinX);
        int y1 = Math.Max(0, Selection.MinY);
        int x2 = Math.Min(world.Width - 1, Selection.MaxX);
        int y2 = Math.Min(world.Height - 1, Selection.MaxY);
        if (x1 > x2 || y1 > y2)
            return false;
        var schem = ToolEngine.Extract(world, x1, y1, x2 - x1 + 1, y2 - y1 + 1);
        if (Selection.Shape != BrushShape.Square)
        {
            for (int iy = 0; iy < schem.Height; iy++)
            {
                for (int ix = 0; ix < schem.Width; ix++)
                {
                    if (!ToolEngine.InRectShape(x1 + ix, y1 + iy, x1, y1, x2, y2, Selection.Shape))
                        schem[ix, iy].Skip = true;
                }
            }
        }

        Clipboard = schem;
        return true;
    }

    public static void ApplyToolAtCursor()
    {
        if (Main.gameMenu || !Main.LocalPlayer.active || Tool == EditorTool.Marquee)
            return;
        int x = (int)(Main.MouseWorld.X / 16f);
        int y = (int)(Main.MouseWorld.Y / 16f);
        switch (Tool)
        {
            case EditorTool.Brush:
                ToolEngine.Paint(WorldTiles.Instance, x, y, BrushRadius, BrushShape, StampFromHeld(), TileLayer.All, History);
                WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
                break;
            case EditorTool.Fill:
                if (ToolEngine.FloodFill(WorldTiles.Instance, x, y, StampFromHeld(), TileLayer.All, History) > 0 &&
                    History.TryGetLastBounds(out int fx, out int fy, out int fw, out int fh))
                    WorldTiles.Refresh(fx, fy, fw, fh);
                break;
            case EditorTool.Eraser:
                ToolEngine.Erase(WorldTiles.Instance, x, y, BrushRadius, BrushShape, TileLayer.All, History);
                WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
                break;
        }
    }

    private static void Origin(out int x, out int y)
    {
        if (Selection.Active)
        {
            x = Math.Max(0, Selection.MinX);
            y = Math.Max(0, Selection.MinY);
            return;
        }

        x = (int)(Main.MouseWorld.X / 16f);
        y = (int)(Main.MouseWorld.Y / 16f);
    }

    private static TileDataBlock StampFromHeld()
    {
        var item = Main.LocalPlayer.HeldItem;
        var stamp = new TileDataBlock();
        if (item.createTile >= 0)
        {
            stamp.HasTile = true;
            stamp.TileType = (ushort)item.createTile;
        }

        if (item.createWall > 0)
            stamp.WallType = (ushort)item.createWall;
        return stamp;
    }
}
