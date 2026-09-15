using System;
using Hakoniwa.Engine.Data;

namespace Hakoniwa.Engine.Tools;

public static class TransformEngine
{
    public static Schematic Rotate90Clockwise(Schematic source)
    {
        int w = source.Width;
        int h = source.Height;
        var rotated = CreateLike(source, h, w, h - 1 - source.AnchorY, source.AnchorX);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var tile = source[x, y];
                tile.Slope = RotateSlope90(tile.Slope);
                rotated[h - 1 - y, x] = tile;
            }
        }

        return rotated;
    }

    public static Schematic Rotate180(Schematic source)
    {
        int w = source.Width;
        int h = source.Height;
        var rotated = CreateLike(source, w, h, w - 1 - source.AnchorX, h - 1 - source.AnchorY);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var tile = source[x, y];
                tile.Slope = RotateSlope180(tile.Slope);
                rotated[w - 1 - x, h - 1 - y] = tile;
            }
        }

        return rotated;
    }

    public static Schematic Rotate270Clockwise(Schematic source)
    {
        int w = source.Width;
        int h = source.Height;
        var rotated = CreateLike(source, h, w, source.AnchorY, w - 1 - source.AnchorX);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var tile = source[x, y];
                tile.Slope = RotateSlope270(tile.Slope);
                rotated[y, w - 1 - x] = tile;
            }
        }

        return rotated;
    }

    public static Schematic FlipHorizontal(Schematic source)
    {
        int w = source.Width;
        int h = source.Height;
        var flipped = CreateLike(source, w, h, w - 1 - source.AnchorX, source.AnchorY);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var tile = source[x, y];
                tile.Slope = FlipSlopeHorizontal(tile.Slope);
                flipped[w - 1 - x, y] = tile;
            }
        }

        return flipped;
    }

    public static Schematic FlipVertical(Schematic source)
    {
        int w = source.Width;
        int h = source.Height;
        var flipped = CreateLike(source, w, h, source.AnchorX, h - 1 - source.AnchorY);

        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var tile = source[x, y];
                tile.Slope = FlipSlopeVertical(tile.Slope);
                flipped[x, h - 1 - y] = tile;
            }
        }

        return flipped;
    }

    public static Schematic Translate(Schematic source, int dx, int dy)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        var moved = CreateLike(source, source.Width, source.Height, source.AnchorX + dx, source.AnchorY + dy);
        for (int x = 0; x < source.Width; x++)
        {
            for (int y = 0; y < source.Height; y++)
            {
                int nx = x + dx;
                int ny = y + dy;
                if ((uint)nx >= (uint)source.Width || (uint)ny >= (uint)source.Height)
                    continue;
                moved[nx, ny] = source[x, y];
            }
        }

        return moved;
    }

    private static Schematic CreateLike(Schematic source, int width, int height, int anchorX, int anchorY)
    {
        if (source is null)
            throw new ArgumentNullException(nameof(source));

        return new Schematic(width, height)
        {
            Name = source.Name,
            Author = source.Author,
            Description = source.Description,
            Tags = (string[])source.Tags.Clone(),
            CreatedAt = source.CreatedAt,
            AnchorX = anchorX,
            AnchorY = anchorY
        };
    }

    private static byte RotateSlope90(byte slope) => slope switch
    {
        1 => 3,
        2 => 1,
        3 => 4,
        4 => 2,
        _ => slope
    };

    private static byte RotateSlope180(byte slope) => slope switch
    {
        1 => 4,
        2 => 3,
        3 => 2,
        4 => 1,
        _ => slope
    };

    private static byte RotateSlope270(byte slope) => slope switch
    {
        1 => 2,
        2 => 4,
        3 => 1,
        4 => 3,
        _ => slope
    };

    private static byte FlipSlopeHorizontal(byte slope) => slope switch
    {
        1 => 2,
        2 => 1,
        3 => 4,
        4 => 3,
        _ => slope
    };

    private static byte FlipSlopeVertical(byte slope) => slope switch
    {
        1 => 3,
        3 => 1,
        2 => 4,
        4 => 2,
        _ => slope
    };
}
