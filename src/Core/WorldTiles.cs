using System;
using Hakoniwa.Engine.Data;
using Terraria;

namespace Hakoniwa.Core;

public sealed class WorldTiles : ITileGrid
{
    public static readonly WorldTiles Instance = new();

    public int Width => Main.maxTilesX;
    public int Height => Main.maxTilesY;

    public bool InBounds(int x, int y) => Main.tile is not null && WorldGen.InWorld(x, y);

    public TileDataBlock Get(int x, int y)
    {
        if (!InBounds(x, y))
            throw new System.ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside the world.");

        var tile = Main.tile[x, y];
        if (tile is null)
            return default;

        var block = new TileDataBlock
        {
            TileType = tile.type,
            WallType = tile.wall,
            TileFrameX = tile.frameX,
            TileFrameY = tile.frameY,
            Liquid = tile.liquid,
            LiquidType = tile.liquidType(),
            Color = tile.color(),
            WallColor = tile.wallColor(),
        };
        block.HasTile = tile.active();
        block.IsHalfBlock = tile.halfBrick();
        block.HasActuator = tile.actuator();
        block.IsActuated = tile.inActive();
        block.Slope = tile.slope();
        block.RedWire = tile.wire();
        block.BlueWire = tile.wire2();
        block.GreenWire = tile.wire3();
        block.YellowWire = tile.wire4();
        return block;
    }

    public void Set(int x, int y, in TileDataBlock block)
    {
        if (!InBounds(x, y))
            throw new System.ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside the world.");

        var tile = Main.tile[x, y] ?? (Main.tile[x, y] = new Tile());
        if (block.HasTile)
        {
            tile.type = block.TileType;
            tile.active(true);
            tile.frameX = block.TileFrameX;
            tile.frameY = block.TileFrameY;
            tile.halfBrick(block.IsHalfBlock);
            tile.actuator(block.HasActuator);
            tile.inActive(block.IsActuated);
            tile.slope(block.Slope);
        }
        else
        {
            tile.active(false);
            tile.type = 0;
            tile.frameX = 0;
            tile.frameY = 0;
            tile.halfBrick(false);
            tile.actuator(false);
            tile.inActive(false);
            tile.slope(0);
        }

        tile.wall = block.WallType;
        tile.liquid = block.Liquid;
        tile.liquidType(block.LiquidType);
        tile.color(block.Color);
        tile.wallColor(block.WallColor);
        tile.wire(block.RedWire);
        tile.wire2(block.BlueWire);
        tile.wire3(block.GreenWire);
        tile.wire4(block.YellowWire);
    }

    public static void Refresh(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0 || Main.tile is null)
            return;

        int x1 = Math.Max(0, x);
        int y1 = Math.Max(0, y);
        int x2 = Math.Min(Main.maxTilesX, x + width);
        int y2 = Math.Min(Main.maxTilesY, y + height);
        if (x1 >= x2 || y1 >= y2)
            return;

        int fx1 = Math.Max(1, x1);
        int fy1 = Math.Max(1, y1);
        int fx2 = Math.Min(Main.maxTilesX - 1, x2);
        int fy2 = Math.Min(Main.maxTilesY - 1, y2);
        for (int i = fx1; i < fx2; i++)
        {
            for (int j = fy1; j < fy2; j++)
                WorldGen.Reframe(i, j);
        }

        if (Main.netMode == 0)
            return;

        const int chunk = 32;
        for (int cx = x1; cx < x2; cx += chunk)
        {
            int cw = Math.Min(chunk, x2 - cx);
            for (int cy = y1; cy < y2; cy += chunk)
            {
                int ch = Math.Min(chunk, y2 - cy);
                NetMessage.SendTileSquare(-1, cx, cy, cw, ch);
            }
        }
    }
}
