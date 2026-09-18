using System;
using Hakoniwa.Core;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace Hakoniwa.Engine;

public enum EditorTool
{
    None = -1,
    Marquee = 0,
    Brush = 1,
    Eraser = 2,
    Eyedropper = 3,
    Shape = 4,
}

public enum DrawKind
{
    Line = 0,
    Rect = 1,
    Circle = 2,
    RoundRect = 3,
}

public static class EditorSession
{
    public static readonly Selection Selection = new();
    public static readonly HistoryStack History = new();
    public static Schematic? Clipboard;
    public static bool Pasting;
    public static EditorTool Tool = EditorTool.None;

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
    public static int ShapeRadius = 4;
    public static TileLayer Layers = TileLayer.All;
    public static TileDataBlock Stamp;
    public static bool HasStamp;
    public static bool ReplaceMode;
    public static bool PasteSkipAir;
    public static bool Stroking;
    public static int StrokeX0, StrokeY0, StrokeX1, StrokeY1;

    private static KeyboardState _keys;

    public static TileDataBlock CurrentStamp() => HasStamp ? Stamp : FromHeld();

    public static TileLayer ActiveLayers()
    {
        if (HasStamp)
            return Layers;
        var item = Held();
        TileLayer held = 0;
        if (item.createTile >= 0) held |= TileLayer.Tile;
        if (item.createWall > 0) held |= TileLayer.Wall;
        if (item.paint > 0) held |= TileLayer.Paint;
        return held == 0 ? Layers : Layers & held;
    }

    public static void SyncKeys(KeyboardState kb) => _keys = kb;

    public static void HandleKeys(KeyboardState kb, bool hotkeys)
    {
        if (Pressed(kb, Keys.Escape) && Pasting && !Main.playerInventory && !Main.ingameOptionsWindow)
        {
            CancelPaste();
            Main.blockKey = Keys.Escape.ToString();
        }

        if (hotkeys && (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl)))
        {
            if (Pressed(kb, Keys.C)) Copy();
            if (Pressed(kb, Keys.X)) Cut();
            if (Pressed(kb, Keys.V)) BeginPaste();
            if (Pressed(kb, Keys.Z)) Undo();
            if (Pressed(kb, Keys.Y)) Redo();
        }

        _keys = kb;
    }

    private static bool Pressed(KeyboardState kb, Keys key) => kb.IsKeyDown(key) && !_keys.IsKeyDown(key);

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

    public static void BeginPaste()
    {
        if (Clipboard is null || !Main.LocalPlayer.active)
            return;
        CursorTile(out int x, out int y);
        int ox = x - Clipboard.AnchorX;
        int oy = y - Clipboard.AnchorY;
        Selection.Set(ox, oy, ox + Clipboard.Width - 1, oy + Clipboard.Height - 1);
        ReplaceMode = false;
        if (!Pasting)
            Notices.Post("预览粘贴：可移动、翻转，确认后写入");
        Pasting = true;
    }

    public static void CancelPaste() => Pasting = false;

    public static void CommitPaste()
    {
        if (!Pasting || Clipboard is null || !Main.LocalPlayer.active || !Selection.Active)
            return;
        int x = Selection.MinX + Clipboard.AnchorX;
        int y = Selection.MinY + Clipboard.AnchorY;
        ToolEngine.Paste(WorldTiles.Instance, Clipboard, x, y, Layers, History, PasteSkipAir);
        SchematicWorld.Paste(Clipboard, x, y);
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Clipboard.Width, Clipboard.Height);
        Pasting = false;
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
        FitPasteSelection();
        Notices.Post("已水平翻转");
    }

    public static void FlipVertical()
    {
        if (Clipboard is null)
            return;
        Clipboard = TransformEngine.FlipVertical(Clipboard);
        FitPasteSelection();
        Notices.Post("已垂直翻转");
    }

    public static void Rotate90()
    {
        if (Clipboard is null)
            return;
        Clipboard = TransformEngine.Rotate90Clockwise(Clipboard);
        FitPasteSelection();
        Notices.Post("已旋转 90°");
    }

    private static void FitPasteSelection()
    {
        if (!Pasting || Clipboard is null || !Selection.Active)
            return;
        int x = Selection.MinX;
        int y = Selection.MinY;
        Selection.Set(x, y, x + Clipboard.Width - 1, y + Clipboard.Height - 1);
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

    public static Schematic? CaptureSelection()
    {
        if (!Selection.Active)
            return null;
        var world = WorldTiles.Instance;
        int x1 = Math.Max(0, Selection.MinX);
        int y1 = Math.Max(0, Selection.MinY);
        int x2 = Math.Min(world.Width - 1, Selection.MaxX);
        int y2 = Math.Min(world.Height - 1, Selection.MaxY);
        if (x1 > x2 || y1 > y2)
            return null;
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
        return schem;
    }

    public static bool TryCopy()
    {
        var schem = CaptureSelection();
        if (schem is null)
            return false;
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
            case EditorTool.Eraser:
                EraseAt(x, y);
                break;
            case EditorTool.Eyedropper:
                PickStamp(x, y);
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

    public static void BeginStroke(int x, int y)
    {
        Stroking = true;
        StrokeX0 = StrokeX1 = x;
        StrokeY0 = StrokeY1 = y;
    }

    public static void DragStroke(int x, int y)
    {
        if (SquareStroke())
            SnapSquare(x, y);
        else
        {
            StrokeX1 = x;
            StrokeY1 = y;
        }
    }

    private static bool SquareStroke()
    {
        if (DrawKind is not (DrawKind.Rect or DrawKind.Circle or DrawKind.RoundRect))
            return false;
        var kb = Keyboard.GetState();
        return kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
    }

    private static void SnapSquare(int x, int y)
    {
        int dx = x - StrokeX0;
        int dy = y - StrokeY0;
        int side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        StrokeX1 = StrokeX0 + (dx < 0 ? -side : side);
        StrokeY1 = StrokeY0 + (dy < 0 ? -side : side);
    }

    public static void EndStroke()
    {
        if (!Stroking)
            return;
        if (SquareStroke())
            SnapSquare(StrokeX1, StrokeY1);
        Stroking = false;
        var stamp = CurrentStamp();
        var layers = ActiveLayers();
        if (Tool != EditorTool.Shape || layers == TileLayer.None)
            return;
        int n = DrawKind switch
        {
            DrawKind.Line => ToolEngine.PaintLine(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, BrushRadius, BrushShape, stamp, layers, History),
            DrawKind.Rect => ToolEngine.PaintRect(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, stamp, layers, History),
            DrawKind.Circle => ToolEngine.PaintRect(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, stamp, layers, History, BrushShape.Circle),
            DrawKind.RoundRect => ToolEngine.PaintRect(WorldTiles.Instance, StrokeX0, StrokeY0, StrokeX1, StrokeY1, stamp, layers, History, BrushShape.Square, ShapeRadius),
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

    public static void FillSelection()
    {
        if (!Selection.Active || !TryHandStamp(out var stamp))
            return;
        var layers = ActiveLayers();
        if (layers == TileLayer.None)
            return;
        int n = ToolEngine.PaintRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.MaxX, Selection.MaxY, stamp, layers, History, Selection.Shape);
        if (n <= 0)
            return;
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        Notices.Post($"已填充 {n} 格");
    }

    public static void ReplaceSelectionAt(int x, int y)
    {
        if (!ReplaceMode || !Selection.Contains(x, y) || !TryHandStamp(out var stamp))
            return;
        var layers = ActiveLayers();
        if (layers == TileLayer.None)
            return;
        var match = WorldTiles.Instance.Get(x, y);
        int n = ToolEngine.Replace(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, match, stamp, layers, History, Selection.Shape);
        if (n <= 0)
            return;
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        Notices.Post($"已替换 {n} 格");
    }

    private static void PaintAt(int x, int y)
    {
        var layers = ActiveLayers();
        if (layers == TileLayer.None)
            return;
        ToolEngine.Paint(WorldTiles.Instance, x, y, BrushRadius, BrushShape, CurrentStamp(), layers, History);
        WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
    }

    private static void EraseAt(int x, int y)
    {
        ToolEngine.Erase(WorldTiles.Instance, x, y, BrushRadius, BrushShape, Layers, History);
        WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
    }

    private static TileDataBlock FromHeld()
    {
        TryHandStamp(out var stamp, warn: false);
        return stamp;
    }

    private static Item Held() => !Main.mouseItem.IsAir ? Main.mouseItem : Main.LocalPlayer.HeldItem;

    private static bool TryHandStamp(out TileDataBlock stamp, bool warn = true)
    {
        stamp = default;
        var item = Held();
        if (item.createTile < 0 && item.createWall <= 0 && item.paint == 0)
        {
            if (warn)
                Notices.Post("请先拿着要填充或替换的物块 / 墙壁 / 油漆");
            return false;
        }

        if (item.createTile >= 0)
        {
            stamp.HasTile = true;
            stamp.TileType = (ushort)item.createTile;
        }

        if (item.createWall > 0)
            stamp.WallType = (ushort)item.createWall;
        if (item.paint > 0)
        {
            stamp.Color = item.paint;
            stamp.WallColor = item.paint;
        }
        return true;
    }
}
