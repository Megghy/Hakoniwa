using System;
using System.Collections.Generic;
using Hakoniwa.Core;
using Hakoniwa.Engine.Data;

namespace Hakoniwa.Engine.Tools;

public enum BrushShape
{
    Circle = 0,
    Square = 1,
    Diamond = 2,
}

[Flags]
public enum TileLayer
{
    None = 0,
    Tile = 1 << 0,
    Wall = 1 << 1,
    Paint = 1 << 2,
    Wire = 1 << 3,
    Liquid = 1 << 4,
    All = Tile | Wall | Paint | Wire | Liquid,
}

public static class ToolEngine
{
    public static bool InBrush(int dx, int dy, int radius, BrushShape shape) => shape switch
    {
        BrushShape.Square => Math.Max(Math.Abs(dx), Math.Abs(dy)) <= radius,
        BrushShape.Diamond => Math.Abs(dx) + Math.Abs(dy) <= radius,
        BrushShape.Circle => (long)dx * dx + (long)dy * dy <= (long)radius * radius,
        _ => throw new ArgumentOutOfRangeException(nameof(shape), shape, "Unknown brush shape."),
    };

    public static int Paint(
        ITileGrid world,
        int cx,
        int cy,
        int radius,
        BrushShape shape,
        in TileDataBlock stamp,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null,
        double scatter = 0,
        Random? random = null)
    {
        EnsureStroke(world, radius, layers, scatter, random);
        var changes = new List<TileChange>();
        int painted = 0;

        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (!world.InBounds(x, y) || !InBrush(x - cx, y - cy, radius, shape))
                    continue;
                if (scatter > 0 && random!.NextDouble() < scatter)
                    continue;
                painted += Apply(world, x, y, ApplyLayers(world.Get(x, y), stamp, layers), changes);
            }
        }

        history?.Push(changes);
        return painted;
    }

    public static int Erase(
        ITileGrid world,
        int cx,
        int cy,
        int radius,
        BrushShape shape,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null)
    {
        EnsureStroke(world, radius, layers, 0, null);
        var changes = new List<TileChange>();
        int erased = 0;

        for (int y = cy - radius; y <= cy + radius; y++)
        {
            for (int x = cx - radius; x <= cx + radius; x++)
            {
                if (!world.InBounds(x, y) || !InBrush(x - cx, y - cy, radius, shape))
                    continue;
                erased += Apply(world, x, y, ClearLayers(world.Get(x, y), layers), changes);
            }
        }

        history?.Push(changes);
        return erased;
    }

    public static int FloodFill(
        ITileGrid world,
        int x,
        int y,
        in TileDataBlock stamp,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (layers == TileLayer.None)
            throw new ArgumentException("Layer mask must select at least one layer.", nameof(layers));
        if (!world.InBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside the world.");

        var target = world.Get(x, y);
        var changes = new List<TileChange>();
        var visited = new HashSet<int>();
        var queue = new Queue<(int X, int Y)>();
        queue.Enqueue((x, y));
        int filled = 0;

        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            int key = cx * world.Height + cy;
            if (!visited.Add(key) || !world.InBounds(cx, cy))
                continue;
            if (!Matches(world.Get(cx, cy), target, layers))
                continue;

            filled += Apply(world, cx, cy, ApplyLayers(world.Get(cx, cy), stamp, layers), changes);
            queue.Enqueue((cx + 1, cy));
            queue.Enqueue((cx - 1, cy));
            queue.Enqueue((cx, cy + 1));
            queue.Enqueue((cx, cy - 1));
        }

        history?.Push(changes);
        return filled;
    }

    public static Schematic Extract(ITileGrid world, int x, int y, int width, int height)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (!world.InBounds(x, y) || !world.InBounds(x + width - 1, y + height - 1))
            throw new ArgumentOutOfRangeException(nameof(x), "Extract rectangle must lie fully inside the world.");

        var schematic = new Schematic(width, height);
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
                schematic[ix, iy] = world.Get(x + ix, y + iy);
        }

        return schematic;
    }

    public static int Paste(
        ITileGrid world,
        Schematic schematic,
        int originX,
        int originY,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (schematic is null)
            throw new ArgumentNullException(nameof(schematic));
        if (layers == TileLayer.None)
            throw new ArgumentException("Layer mask must select at least one layer.", nameof(layers));

        var changes = new List<TileChange>();
        int pasted = 0;
        for (int iy = 0; iy < schematic.Height; iy++)
        {
            for (int ix = 0; ix < schematic.Width; ix++)
            {
                int wx = originX + ix - schematic.AnchorX;
                int wy = originY + iy - schematic.AnchorY;
                if (!world.InBounds(wx, wy))
                    continue;
                pasted += Apply(world, wx, wy, ApplyLayers(world.Get(wx, wy), schematic[ix, iy], layers), changes);
            }
        }

        history?.Push(changes);
        return pasted;
    }

    public static int ClearRect(
        ITileGrid world,
        int x,
        int y,
        int width,
        int height,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (layers == TileLayer.None)
            throw new ArgumentException("Layer mask must select at least one layer.", nameof(layers));

        var changes = new List<TileChange>();
        int cleared = 0;
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int wx = x + ix;
                int wy = y + iy;
                if (!world.InBounds(wx, wy))
                    continue;
                cleared += Apply(world, wx, wy, ClearLayers(world.Get(wx, wy), layers), changes);
            }
        }

        history?.Push(changes);
        return cleared;
    }

    public static TileDataBlock ApplyLayers(in TileDataBlock dest, in TileDataBlock stamp, TileLayer layers)
    {
        var result = dest;
        if ((layers & TileLayer.Tile) != 0)
        {
            result.TileType = stamp.TileType;
            result.TileFrameX = stamp.TileFrameX;
            result.TileFrameY = stamp.TileFrameY;
            result.HasTile = stamp.HasTile;
            result.IsHalfBlock = stamp.IsHalfBlock;
            result.HasActuator = stamp.HasActuator;
            result.IsActuated = stamp.IsActuated;
            result.Slope = stamp.Slope;
        }

        if ((layers & TileLayer.Wall) != 0)
            result.WallType = stamp.WallType;
        if ((layers & TileLayer.Paint) != 0)
        {
            result.Color = stamp.Color;
            result.WallColor = stamp.WallColor;
        }

        if ((layers & TileLayer.Wire) != 0)
            result.WireFlags = stamp.WireFlags;
        if ((layers & TileLayer.Liquid) != 0)
        {
            result.Liquid = stamp.Liquid;
            result.LiquidType = stamp.LiquidType;
        }

        return result;
    }

    public static TileDataBlock ClearLayers(in TileDataBlock dest, TileLayer layers)
    {
        var result = dest;
        if ((layers & TileLayer.Tile) != 0)
        {
            result.TileType = 0;
            result.TileFrameX = 0;
            result.TileFrameY = 0;
            result.Flags = 0;
        }

        if ((layers & TileLayer.Wall) != 0)
            result.WallType = 0;
        if ((layers & TileLayer.Paint) != 0)
        {
            result.Color = 0;
            result.WallColor = 0;
        }

        if ((layers & TileLayer.Wire) != 0)
            result.WireFlags = 0;
        if ((layers & TileLayer.Liquid) != 0)
        {
            result.Liquid = 0;
            result.LiquidType = 0;
        }

        return result;
    }

    public static bool Matches(in TileDataBlock a, in TileDataBlock b, TileLayer layers)
    {
        if ((layers & TileLayer.Tile) != 0 && (a.HasTile != b.HasTile || a.TileType != b.TileType))
            return false;
        if ((layers & TileLayer.Wall) != 0 && a.WallType != b.WallType)
            return false;
        if ((layers & TileLayer.Paint) != 0 && (a.Color != b.Color || a.WallColor != b.WallColor))
            return false;
        if ((layers & TileLayer.Wire) != 0 && a.WireFlags != b.WireFlags)
            return false;
        if ((layers & TileLayer.Liquid) != 0 && (a.Liquid != b.Liquid || a.LiquidType != b.LiquidType))
            return false;
        return layers != TileLayer.None;
    }

    private static void EnsureStroke(ITileGrid world, int radius, TileLayer layers, double scatter, Random? random)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "Radius must be >= 0.");
        if (layers == TileLayer.None)
            throw new ArgumentException("Layer mask must select at least one layer.", nameof(layers));
        if (scatter is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(scatter), scatter, "Scatter must be in [0, 1].");
        if (scatter > 0 && random is null)
            throw new ArgumentNullException(nameof(random), "A Random instance is required when scatter > 0.");
    }

    private static int Apply(ITileGrid world, int x, int y, in TileDataBlock next, List<TileChange> changes)
    {
        var before = world.Get(x, y);
        if (before == next)
            return 0;
        world.Set(x, y, next);
        changes.Add(new TileChange(x, y, before, next));
        return 1;
    }
}
