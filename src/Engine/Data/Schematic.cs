using System;
using System.Collections.Generic;

namespace Hakoniwa.Engine.Data;

public sealed class Schematic
{
    public string Name { get; set; } = "Untitled";
    public string Author { get; set; } = "Anonymous";
    public string Description { get; set; } = string.Empty;
    public string[] Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int Width { get; }
    public int Height { get; }
    public int AnchorX { get; set; }
    public int AnchorY { get; set; }

    public TileDataBlock[] Tiles { get; }
    public List<SchematicEntity> Entities { get; } = [];

    public Schematic(int width, int height)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width), width, "Width must be positive.");
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height), height, "Height must be positive.");
        if ((long)width * height > int.MaxValue / TileDataBlock.ByteSize)
            throw new ArgumentOutOfRangeException(nameof(width), "Schematic dimensions exceed the serializable tile budget.");

        Width = width;
        Height = height;
        Tiles = new TileDataBlock[width * height];
    }

    public ref TileDataBlock this[int x, int y]
    {
        get
        {
            if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
                throw new ArgumentOutOfRangeException(nameof(x), $"({x},{y}) is outside {Width}x{Height}.");
            return ref Tiles[x + y * Width];
        }
    }

    public Schematic Clone()
    {
        var copy = new Schematic(Width, Height)
        {
            Name = Name,
            Author = Author,
            Description = Description,
            Tags = (string[])Tags.Clone(),
            CreatedAt = CreatedAt,
            AnchorX = AnchorX,
            AnchorY = AnchorY
        };
        Array.Copy(Tiles, copy.Tiles, Tiles.Length);
        for (int i = 0; i < Entities.Count; i++)
            copy.Entities.Add(Entities[i].Clone());
        return copy;
    }
}
