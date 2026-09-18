using System;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Num = System.Numerics;

namespace Hakoniwa.UI;

internal static class SchematicDrawer
{
    public static void DrawWorld()
    {
        var schem = EditorSession.Clipboard;
        if (!EditorSession.Pasting || schem is null || Main.gameMenu || Main.mapFullscreen || !FocusHelper.AllowInputProcessing)
            return;

        EditorSession.CursorTile(out int ox, out int oy);
        EditorSession.VisibleTiles(out int vx, out int vy, out int vw, out int vh);
        var list = Ui.WorldList;
        var layers = EditorSession.Layers;
        for (int iy = 0; iy < schem.Height; iy++)
        {
            int wy = oy + iy - schem.AnchorY;
            if (wy < vy || wy >= vy + vh)
                continue;
            for (int ix = 0; ix < schem.Width; ix++)
            {
                int wx = ox + ix - schem.AnchorX;
                if (wx < vx || wx >= vx + vw)
                    continue;
                WorldTile(wx, wy, out var min, out var max);
                DrawCell(list, schem[ix, iy], min, max, 0.55f, layers);
            }
        }
    }

    public static void DrawFit(ImDrawListPtr list, Schematic schem, Num.Vector2 pos, Num.Vector2 size, float alpha = 1f)
    {
        if (schem.Width <= 0 || schem.Height <= 0 || size.X < 2f || size.Y < 2f)
            return;
        float cell = Math.Min(size.X / schem.Width, size.Y / schem.Height);
        var origin = pos + (size - new Num.Vector2(schem.Width, schem.Height) * cell) * 0.5f;
        int step = Math.Max(1, Math.Max(schem.Width, schem.Height) / 48);
        var tileSize = new Num.Vector2(cell * step, cell * step);
        for (int iy = 0; iy < schem.Height; iy += step)
        {
            for (int ix = 0; ix < schem.Width; ix += step)
            {
                var min = origin + new Num.Vector2(ix * cell, iy * cell);
                DrawCell(list, schem[ix, iy], min, min + tileSize, alpha, TileLayer.All);
            }
        }
    }

    private static void DrawCell(ImDrawListPtr list, in TileDataBlock tile, Num.Vector2 min, Num.Vector2 max, float alpha, TileLayer layers)
    {
        if (tile.Skip)
            return;
        uint tint = ((uint)(alpha * 255) << 24) | 0x00FFFFFFu;
        if ((layers & TileLayer.Wall) != 0 && WallSprite(tile, layers, out var wallId, out var w0, out var w1))
        {
            var pad = (max - min) * 0.5f;
            list.AddImage(ImGuiBackend.TexRef(wallId), min - pad, max + pad, w0, w1, tint);
        }

        if ((layers & TileLayer.Tile) != 0 && TileSprite(tile, layers, out var tileId, out var t0, out var t1))
        {
            if (tile.IsHalfBlock)
                min.Y += (max.Y - min.Y) * 0.5f;
            list.AddImage(ImGuiBackend.TexRef(tileId), min, max, t0, t1, tint);
        }
    }

    private static bool TileSprite(in TileDataBlock tile, TileLayer layers, out IntPtr id, out Num.Vector2 uv0, out Num.Vector2 uv1)
    {
        id = IntPtr.Zero;
        uv0 = uv1 = default;
        if (!tile.HasTile)
            return false;
        ushort type = tile.TileType;
        if (type >= TextureAssets.Tile.Length || TextureAssets.Tile[type] is null)
            return false;
        Main.instance.LoadTiles(type);
        int paint = (layers & TileLayer.Paint) != 0 ? tile.Color : 0;
        var tex = Main.instance.TilesRenderer.GetTileDrawTexture(type, paint);
        int sx = Math.Max(0, (int)tile.TileFrameX);
        int sy = Math.Max(0, (int)tile.TileFrameY);
        int sw = 16, sh = 16;
        if (tile.IsHalfBlock)
        {
            sy += 8;
            sh = 8;
        }

        return Sprite(tex, sx, sy, sw, sh, out id, out uv0, out uv1);
    }

    private static bool WallSprite(in TileDataBlock tile, TileLayer layers, out IntPtr id, out Num.Vector2 uv0, out Num.Vector2 uv1)
    {
        id = IntPtr.Zero;
        uv0 = uv1 = default;
        ushort type = tile.WallType;
        if (type == 0 || type >= TextureAssets.Wall.Length || TextureAssets.Wall[type] is null)
            return false;
        Main.instance.LoadWall(type);
        int paint = (layers & TileLayer.Paint) != 0 ? tile.WallColor : 0;
        var tex = Main.instance.WallsRenderer.GetWallDrawTexture(type, paint);
        return Sprite(tex, 0, 0, 32, 32, out id, out uv0, out uv1);
    }

    private static bool Sprite(Texture2D? tex, int x, int y, int w, int h, out IntPtr id, out Num.Vector2 uv0, out Num.Vector2 uv1)
    {
        id = IntPtr.Zero;
        uv0 = uv1 = default;
        if (tex is null || tex.IsDisposed)
            return false;
        w = Math.Min(w, tex.Width - x);
        h = Math.Min(h, tex.Height - y);
        if (w <= 0 || h <= 0)
            return false;
        id = ImGuiBackend.GetTextureId(tex);
        if (id == IntPtr.Zero)
            return false;
        uv0 = new Num.Vector2(x / (float)tex.Width, y / (float)tex.Height);
        uv1 = new Num.Vector2((x + w) / (float)tex.Width, (y + h) / (float)tex.Height);
        return true;
    }

    private static void WorldTile(int x, int y, out Num.Vector2 min, out Num.Vector2 max)
    {
        min = ToScreen(x * 16, y * 16);
        max = ToScreen((x + 1) * 16, (y + 1) * 16);
        if (max.X < min.X) (min.X, max.X) = (max.X, min.X);
        if (max.Y < min.Y) (min.Y, max.Y) = (max.Y, min.Y);
    }

    private static Num.Vector2 ToScreen(float wx, float wy)
    {
        var screen = Microsoft.Xna.Framework.Vector2.Transform(
            new Microsoft.Xna.Framework.Vector2(wx, wy) - Main.screenPosition,
            Main.GameViewMatrix.ZoomMatrix);
        return new Num.Vector2(screen.X, screen.Y);
    }
}
