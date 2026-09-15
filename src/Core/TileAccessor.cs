using System;
using Hakoniwa.Engine.Data;

namespace Hakoniwa.Core;

public sealed class TileAccessor : ITileGrid
{
    private readonly TileDataBlock[] _tiles;

    public int Width { get; }
    public int Height { get; }

    public TileAccessor(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");

        Width = width;
        Height = height;
        _tiles = new TileDataBlock[checked(width * height)];
    }

    public bool InBounds(int x, int y) => (uint)x < (uint)Width && (uint)y < (uint)Height;

    public TileDataBlock Get(int x, int y)
    {
        EnsureInBounds(x, y);
        return _tiles[x + y * Width];
    }

    public void Set(int x, int y, in TileDataBlock tile)
    {
        EnsureInBounds(x, y);
        _tiles[x + y * Width] = tile;
    }

    private void EnsureInBounds(int x, int y)
    {
        if (!InBounds(x, y))
            throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside {Width}x{Height}.");
    }
}
