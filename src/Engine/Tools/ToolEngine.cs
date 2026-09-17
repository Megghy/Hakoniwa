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

    public static bool InRectShape(int x, int y, int minX, int minY, int maxX, int maxY, BrushShape shape)
    {
        if (x < minX || x > maxX || y < minY || y > maxY)
            return false;
        if (shape == BrushShape.Square)
            return true;
        float cx = (minX + maxX) * 0.5f;
        float cy = (minY + maxY) * 0.5f;
        float rx = (maxX - minX + 1) * 0.5f;
        float ry = (maxY - minY + 1) * 0.5f;
        float dx = (x - cx) / rx;
        float dy = (y - cy) / ry;
        return shape == BrushShape.Diamond
            ? Math.Abs(dx) + Math.Abs(dy) <= 1.001f
            : dx * dx + dy * dy <= 1.001f;
    }

    public static int CountBrush(int radius, BrushShape shape)
    {
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        int n = 0;
        for (int dy = -radius; dy <= radius; dy++)
        {
            for (int dx = -radius; dx <= radius; dx++)
            {
                if (InBrush(dx, dy, radius, shape))
                    n++;
            }
        }

        return n;
    }

    public static int CountRectShape(int x0, int y0, int x1, int y1, BrushShape shape)
    {
        int minX = Math.Min(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxX = Math.Max(x0, x1);
        int maxY = Math.Max(y0, y1);
        int n = 0;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (InRectShape(x, y, minX, minY, maxX, maxY, shape))
                    n++;
            }
        }

        return n;
    }

    public static int CountLine(int x0, int y0, int x1, int y1, int radius, BrushShape shape)
    {
        if (radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius));
        var seen = new HashSet<long>();
        ForEachLineCell(x0, y0, x1, y1, (cx, cy) =>
        {
            for (int dy = -radius; dy <= radius; dy++)
            {
                for (int dx = -radius; dx <= radius; dx++)
                {
                    if (InBrush(dx, dy, radius, shape))
                        seen.Add(((long)(cx + dx) << 32) ^ (uint)(cy + dy));
                }
            }
        });
        return seen.Count;
    }

    public static void ForEachLineCell(int x0, int y0, int x1, int y1, Action<int, int> visit)
    {
        int x = x0;
        int y = y0;
        int dx = Math.Abs(x1 - x0);
        int sx = x0 < x1 ? 1 : -1;
        int dy = -Math.Abs(y1 - y0);
        int sy = y0 < y1 ? 1 : -1;
        int err = dx + dy;
        while (true)
        {
            visit(x, y);
            if (x == x1 && y == y1)
                break;
            int e2 = 2 * err;
            if (e2 >= dy)
            {
                err += dy;
                x += sx;
            }

            if (e2 <= dx)
            {
                err += dx;
                y += sy;
            }
        }
    }

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
        int painted = StampAround(world, cx, cy, radius, shape, stamp, layers, changes, scatter, random);
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
        int x1 = Math.Max(0, x);
        int y1 = Math.Max(0, y);
        int x2 = Math.Min(world.Width - 1, x + width - 1);
        int y2 = Math.Min(world.Height - 1, y + height - 1);
        if (x1 > x2 || y1 > y2)
            throw new ArgumentOutOfRangeException(nameof(x), "Extract rectangle does not overlap the world.");
        x = x1;
        y = y1;
        width = x2 - x1 + 1;
        height = y2 - y1 + 1;

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
                var stamp = schematic[ix, iy];
                if (stamp.Skip)
                    continue;
                pasted += Apply(world, wx, wy, ApplyLayers(world.Get(wx, wy), stamp, layers), changes);
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
        HistoryStack? history = null,
        BrushShape shape = BrushShape.Square)
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
                if (!world.InBounds(wx, wy) || !InRectShape(wx, wy, x, y, x + width - 1, y + height - 1, shape))
                    continue;
                cleared += Apply(world, wx, wy, ClearLayers(world.Get(wx, wy), layers), changes);
            }
        }

        history?.Push(changes);
        return cleared;
    }

    public static int Relocate(
        ITileGrid world,
        Schematic schematic,
        int srcX,
        int srcY,
        int dstX,
        int dstY,
        bool cut,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (schematic is null)
            throw new ArgumentNullException(nameof(schematic));

        var changes = new List<TileChange>();
        int n = 0;
        if (cut)
        {
            for (int iy = 0; iy < schematic.Height; iy++)
            {
                for (int ix = 0; ix < schematic.Width; ix++)
                {
                    if (schematic[ix, iy].Skip)
                        continue;
                    int wx = srcX + ix;
                    int wy = srcY + iy;
                    if (!world.InBounds(wx, wy))
                        continue;
                    n += Apply(world, wx, wy, ClearLayers(world.Get(wx, wy), layers), changes);
                }
            }
        }

        for (int iy = 0; iy < schematic.Height; iy++)
        {
            for (int ix = 0; ix < schematic.Width; ix++)
            {
                var stamp = schematic[ix, iy];
                if (stamp.Skip)
                    continue;
                int wx = dstX + ix - schematic.AnchorX;
                int wy = dstY + iy - schematic.AnchorY;
                if (!world.InBounds(wx, wy))
                    continue;
                n += Apply(world, wx, wy, ApplyLayers(world.Get(wx, wy), stamp, layers), changes);
            }
        }

        history?.Push(changes);
        return n;
    }

    public static int PaintLine(
        ITileGrid world,
        int x0,
        int y0,
        int x1,
        int y1,
        int radius,
        BrushShape shape,
        in TileDataBlock stamp,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null)
    {
        EnsureStroke(world, radius, layers, 0, null);
        var changes = new List<TileChange>();
        var local = stamp;
        int painted = 0;
        ForEachLineCell(x0, y0, x1, y1, (x, y) =>
            painted += StampAround(world, x, y, radius, shape, local, layers, changes));
        history?.Push(changes);
        return painted;
    }

    public static int PaintRect(
        ITileGrid world,
        int x0,
        int y0,
        int x1,
        int y1,
        in TileDataBlock stamp,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null,
        BrushShape shape = BrushShape.Square)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (layers == TileLayer.None)
            throw new ArgumentException("Layer mask must select at least one layer.", nameof(layers));

        int minX = Math.Min(x0, x1);
        int minY = Math.Min(y0, y1);
        int maxX = Math.Max(x0, x1);
        int maxY = Math.Max(y0, y1);
        var changes = new List<TileChange>();
        int painted = 0;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!world.InBounds(x, y) || !InRectShape(x, y, minX, minY, maxX, maxY, shape))
                    continue;
                painted += Apply(world, x, y, ApplyLayers(world.Get(x, y), stamp, layers), changes);
            }
        }

        history?.Push(changes);
        return painted;
    }

    public static int Replace(
        ITileGrid world,
        int x,
        int y,
        int width,
        int height,
        in TileDataBlock match,
        in TileDataBlock stamp,
        TileLayer layers = TileLayer.All,
        HistoryStack? history = null,
        BrushShape shape = BrushShape.Square)
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
        int replaced = 0;
        int maxX = x + width - 1;
        int maxY = y + height - 1;
        for (int iy = 0; iy < height; iy++)
        {
            for (int ix = 0; ix < width; ix++)
            {
                int wx = x + ix;
                int wy = y + iy;
                if (!world.InBounds(wx, wy) || !InRectShape(wx, wy, x, y, maxX, maxY, shape))
                    continue;
                var current = world.Get(wx, wy);
                if (!Matches(current, match, layers))
                    continue;
                replaced += Apply(world, wx, wy, ApplyLayers(current, stamp, layers), changes);
            }
        }

        history?.Push(changes);
        return replaced;
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

    private static int StampAround(
        ITileGrid world,
        int cx,
        int cy,
        int radius,
        BrushShape shape,
        in TileDataBlock stamp,
        TileLayer layers,
        List<TileChange> changes,
        double scatter = 0,
        Random? random = null)
    {
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

        return painted;
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
