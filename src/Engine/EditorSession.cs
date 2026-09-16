using Hakoniwa.Core;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Terraria;

namespace Hakoniwa.Engine;

public static class EditorSession
{
    public static readonly Selection Selection = new();
    public static readonly HistoryStack History = new();
    public static Schematic? Clipboard;
    public static int SelectedTool;
    public static int BrushRadius = 3;
    public static BrushShape BrushShape = BrushShape.Circle;

    public static void Delete()
    {
        if (!Selection.Active)
            return;
        ToolEngine.ClearRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, TileLayer.All, History);
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
        ToolEngine.ClearRect(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height, TileLayer.All, History);
        WorldTiles.Refresh(Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        Notices.Post($"已剪切 {Selection.Width}x{Selection.Height}");
    }

    public static void Paste()
    {
        if (Clipboard is null || !Main.LocalPlayer.active)
            return;
        int x = (int)(Main.MouseWorld.X / 16f);
        int y = (int)(Main.MouseWorld.Y / 16f);
        if (Selection.Active)
        {
            x = Selection.MinX;
            y = Selection.MinY;
        }

        ToolEngine.Paste(WorldTiles.Instance, Clipboard, x, y, TileLayer.All, History);
        WorldTiles.Refresh(x, y, Clipboard.Width, Clipboard.Height);
        Notices.Post($"已粘贴 {Clipboard.Width}x{Clipboard.Height}");
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

    private static bool TryCopy()
    {
        if (!Selection.Active)
            return false;
        Clipboard = ToolEngine.Extract(WorldTiles.Instance, Selection.MinX, Selection.MinY, Selection.Width, Selection.Height);
        return true;
    }

    public static void ApplyToolAtCursor()
    {
        if (Main.gameMenu || !Main.LocalPlayer.active)
            return;
        int x = (int)(Main.MouseWorld.X / 16f);
        int y = (int)(Main.MouseWorld.Y / 16f);
        switch (SelectedTool)
        {
            case 0:
                ToolEngine.Paint(WorldTiles.Instance, x, y, BrushRadius, BrushShape, StampFromHeld(), TileLayer.All, History);
                WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
                break;
            case 1:
                if (ToolEngine.FloodFill(WorldTiles.Instance, x, y, StampFromHeld(), TileLayer.All, History) > 0 &&
                    History.TryGetLastBounds(out int fx, out int fy, out int fw, out int fh))
                    WorldTiles.Refresh(fx, fy, fw, fh);
                break;
            case 2:
                ToolEngine.Erase(WorldTiles.Instance, x, y, BrushRadius, BrushShape, TileLayer.All, History);
                WorldTiles.Refresh(x - BrushRadius, y - BrushRadius, BrushRadius * 2 + 1, BrushRadius * 2 + 1);
                break;
        }
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
