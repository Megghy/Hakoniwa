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
    Eyedropper = 4,
    Replace = 5,
    Shape = 6,
}

public enum DrawKind
{
    Line = 0,
    Rect = 1,
    Circle = 2,
}

public static class EditorSession
{
    public static readonly Selection Selection = new();
    public static readonly HistoryStack History = new();
    public static Schematic? Clipboard;
    public static EditorTool Tool;

    public static void SetTool(EditorTool tool)
    {
        if (Tool == tool)
            return;
        Tool = tool;
        if (tool is EditorTool.Brush or EditorTool.Eraser or EditorTool.Shape)
            Notices.Post("Ctrl+滚轮调整大小");
    }

    public static int BrushRadius = 3;
    public static BrushShape BrushShape = BrushShape.Circle;
    public static BrushShape SelectionShape = BrushShape.Square;
    public static DrawKind DrawKind;
    public static TileLayer Layers = TileLayer.All;
    public static TileDataBlock Stamp;
    public static bool HasStamp;
    public static TileDataBlock Match;
    public static bool HasMatch;
    public static bool Stroking;
    public static int StrokeX0, StrokeY0, StrokeX1, StrokeY1;

    public static TileDataBlock CurrentStamp() => HasStamp ? Stamp : FromHeld();

    public static void Delete()
    {
        if (!Selection.Active)
            return;
        SchematicWorld.RemoveRect(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        ToolEngine.ClearRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, Layers, History, Selection.Shape);
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
        SchematicWorld.RemoveRect(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        ToolEngine.ClearRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, Layers, History, Selection.Shape);
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        Notices.Post($"已剪切 {Selection.Width}x{Selection.Height}");
    }

    public static void Paste()
    {
        if (Clipboard is null || !Main.LocalPlayer.active)
            return;
        Origin(out int x, out int y);
        ToolEngine.Paste(WorldTiles.Instance, Clipboard, x, y, Layers, History);
        SchematicWorld.Paste(Clipboard, x, y);
        WorldTiles.Refresh(x, y, Clipboard.Width, Clipboard.Height);
        Notices.Post($"已粘贴 {Clipboard.Width}x{Clipboard.Height}");
    }

    public static void DropMove(Schematic payload, int srcX, int srcY, bool cut)
    {
        Origin(out int dstX, out int dstY);
        if (cut)
            SchematicWorld.RemoveAt(payload, srcX, srcY);
        ToolEngine.Relocate(WorldTiles.Instance, payload, srcX, srcY, dstX, dstY, cut, Layers, History);
        SchematicWorld.Paste(payload, dstX, dstY);
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

        SchematicWorld.Capture(schem, x1, y1);
        Clipboard = schem;
        return true;
    }

    public static void ApplyToolAtCursor()
    {
        if (Main.gameMenu || !Main.LocalPlayer.active || Tool is EditorTool.Marquee or EditorTool.Shape)
            return;
        CursorTile(out int x, out int y);
        switch (Tool)
        {
            case EditorTool.Brush:
                PaintAt(x, y);
                break;
            case EditorTool.Fill:
                FillAt(x, y);
                break;
            case EditorTool.Eraser:
                EraseAt(x, y);
                break;
            case EditorTool.Eyedropper:
                PickStamp(x, y);
                break;
            case EditorTool.Replace:
                ReplaceAt();
                break;
        }
    }

    public static void PickStamp(int x, int y)
    {
        if (!WorldTiles.Instance.InBounds(x, y))
            return;
        Stamp = WorldTiles.Instance.Get(x, y);
        HasStamp = true;
        Notices.Post($"已取样 物块 {Stamp.TileType} 墙 {Stamp.WallType}");
    }

    public static void PickMatch(int x, int y)
    {
        if (!WorldTiles.Instance.InBounds(x, y))
            return;
        Match = WorldTiles.Instance.Get(x, y);
        HasMatch = true;
        Notices.Post($"替换源 物块 {Match.TileType} 墙 {Match.WallType}");
    }

    public static void BeginStroke(int x, int y)
    {
        Stroking = true;
        StrokeX0 = StrokeX1 = x;
        StrokeY0 = StrokeY1 = y;
    }

    public static void DragStroke(int x, int y)
    {
        StrokeX1 = x;
        StrokeY1 = y;
    }

    public static void EndStroke()
    {
        if (!Stroking)
            return;
        Stroking = false;
        var stamp = CurrentStamp();
        if (Tool != EditorTool.Shape)
            return;
        int n = DrawKind switch
        {
            DrawKind.Line => ToolEngine.PaintLine(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, BrushRadius, BrushShape, stamp, Layers, History),
            DrawKind.Rect => ToolEngine.PaintRect(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, stamp, Layers, History),
            DrawKind.Circle => ToolEngine.PaintRect(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, stamp, Layers, History, BrushShape.Circle),
            _ => 0,
        };
        if (n <= 0)
            return;
        int pad = DrawKind == DrawKind.Line ? BrushRadius : 0;
        int x = Math.Min(StrokeX0, StrokeX1) - pad;
        int y = Math.Min(StrokeY0, StrokeY1) - pad;
        int w = Math.Abs(StrokeX1 - StrokeX0) + 1 + pad * 2;
        int h = Math.Abs(StrokeY1 - StrokeY0) + 1 + pad * 2;
        WorldTiles.Refresh(x, y, w, h);
    }

    public static void Origin(out int x, out int y)
    {
        if (Selection.Active)
        {
            x = Math.Max(0, Selection.MinX);
            y = Math.Max(0, Selection.MinY);
            return;
        }

        CursorTile(out x, out y);
    }

    public static void CursorTile(out int x, out int y)
    {
        x = (int)(Main.MouseWorld.X / 16f);
        y = (int)(Main.MouseWorld.Y / 16f);
    }

    public static void VisibleTiles(out int x, out int y, out int w, out int h)
    {
        x = Math.Max(0, (int)(Main.screenPosition.X / 16f) - 1);
        y = Math.Max(0, (int)(Main.screenPosition.Y / 16f) - 1);
        int x2 = Math.Min(Main.maxTilesX - 1, (int)((Main.screenPosition.X + Main.screenWidth / Main.GameZoomTarget) / 16f) + 2);
        int y2 = Math.Min(Main.maxTilesY - 1, (int)((Main.screenPosition.Y + Main.screenHeight / Main.GameZoomTarget) / 16f) + 2);
        w = Math.Max(0, x2 - x + 1);
        h = Math.Max(0, y2 - y + 1);
    }

    public static bool ReplaceDomain(out int x, out int y, out int w, out int h, out BrushShape shape)
    {
        if (Selection.Active)
        {
            x = Selection.MinX;
            y = Selection.MinY;
            w = Selection.Width;
            h = Selection.Height;
            shape = Selection.Shape;
            return w > 0 && h > 0;
        }

        VisibleTiles(out x, out y, out w, out h);
        shape = BrushShape.Square;
        return w > 0 && h > 0;
    }

    private static void PaintAt(int x, int y)
    {
        ToolEngine.Paint(WorldTiles.Instance, x, y, BrushRadius, BrushShape, CurrentStamp(), Layers, History);
        WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
    }

    private static void FillAt(int x, int y)
    {
        if (ToolEngine.FloodFill(WorldTiles.Instance, x, y, CurrentStamp(), Layers, History) > 0 &&
            History.TryGetLastBounds(out int fx, out int fy, out int fw, out int fh))
            WorldTiles.Refresh(fx, fy, fw, fh);
    }

    private static void EraseAt(int x, int y)
    {
        ToolEngine.Erase(WorldTiles.Instance, x, y, BrushRadius, BrushShape, Layers, History);
        WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
    }

    private static void ReplaceAt()
    {
        if (!HasMatch)
        {
            CursorTile(out int x, out int y);
            PickMatch(x, y);
            return;
        }

        if (!ReplaceDomain(out int dx, out int dy, out int dw, out int dh, out var shape))
            return;
        int n = ToolEngine.Replace(WorldTiles.Instance, dx, dy, dw, dh, Match, CurrentStamp(), Layers, History, shape);
        if (n <= 0)
            return;
        WorldTiles.Refresh(dx, dy, dw, dh);
        Notices.Post($"已替换 {n} 格");
    }

    private static TileDataBlock FromHeld()
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
