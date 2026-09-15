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
        tile.type = block.TileType;
        tile.wall = block.WallType;
        tile.frameX = block.TileFrameX;
        tile.frameY = block.TileFrameY;
        tile.liquid = block.Liquid;
        tile.liquidType(block.LiquidType);
        tile.color(block.Color);
        tile.wallColor(block.WallColor);
        tile.active(block.HasTile);
        tile.halfBrick(block.IsHalfBlock);
        tile.actuator(block.HasActuator);
        tile.inActive(block.IsActuated);
        tile.slope(block.Slope);
        tile.wire(block.RedWire);
        tile.wire2(block.BlueWire);
        tile.wire3(block.GreenWire);
        tile.wire4(block.YellowWire);
    }

    public static void Refresh(int x, int y, int width, int height)
    {
        if (width <= 0 || height <= 0)
            return;
        WorldGen.RangeFrame(x, y, x + width, y + height);
    }
}
