using System;
using System.IO;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.IO;
using Hakoniwa.Engine.Tools;
using Xunit;

namespace Hakoniwa.Tests;

public sealed class SchematicTests
{
    [Fact]
    public void Constructor_RejectsNonPositiveSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Schematic(0, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Schematic(1, -2));
    }

    [Fact]
    public void Indexer_RoundTripsAndClones()
    {
        var source = CreateGrid();
        source[1, 2].TileFrameX = 540;
        var clone = source.Clone();
        clone[0, 0] = new TileDataBlock { TileType = 99, HasTile = true };

        Assert.Equal(1, source[0, 0].TileType);
        Assert.Equal(99, clone[0, 0].TileType);
        Assert.Equal(540, clone[1, 2].TileFrameX);
        Assert.Equal("grid", clone.Name);
        Assert.Equal(source.CreatedAt, clone.CreatedAt);
    }

    [Fact]
    public void Serializer_RoundTripsEntitiesAndSurvivesRotate()
    {
        var source = CreateGrid();
        source.Entities.Add(new SchematicEntity
        {
            Kind = SchematicEntityKind.Chest,
            X = 1,
            Y = 2,
            Name = "loot",
            Items = [new SchematicItem(2, 30, 0)],
        });
        source.Entities.Add(new SchematicEntity
        {
            Kind = SchematicEntityKind.Sign,
            X = 0,
            Y = 1,
            Text = "hello",
        });

        var loaded = SchematicSerializer.Deserialize(SchematicSerializer.Serialize(source));
        Assert.Equal(2, loaded.Entities.Count);
        Assert.Equal(SchematicEntityKind.Chest, loaded.Entities[0].Kind);
        Assert.Equal("loot", loaded.Entities[0].Name);
        Assert.Equal(2, loaded.Entities[0].Items[0].Type);
        Assert.Equal("hello", loaded.Entities[1].Text);

        var rotated = TransformEngine.Rotate90Clockwise(source);
        Assert.Equal((0, 1), (rotated.Entities[0].X, rotated.Entities[0].Y));
        var restored = TransformEngine.Rotate90Clockwise(
            TransformEngine.Rotate90Clockwise(
                TransformEngine.Rotate90Clockwise(rotated)));
        Assert.Equal(source.Entities[0].X, restored.Entities[0].X);
        Assert.Equal(source.Entities[0].Y, restored.Entities[0].Y);
    }

    [Fact]
    public void Serializer_RoundTripsBytesAndFile()
    {
        var source = CreateGrid();
        source.Author = "me";
        source.Tags = ["castle", "sky"];
        source.AnchorX = 1;
        source.AnchorY = 2;
        source[1, 1].TileFrameX = 360;
        source[1, 1].RedWire = true;

        byte[] bytes = SchematicSerializer.Serialize(source);
        var loaded = SchematicSerializer.Deserialize(bytes);
        AssertEqualGrid(source, loaded);

        string path = Path.Combine(Path.GetTempPath(), $"hakoniwa-{Guid.NewGuid():N}.schem");
        try
        {
            SchematicSerializer.Save(source, path);
            AssertEqualGrid(source, SchematicSerializer.Load(path));
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public void Serializer_RejectsBadMagic()
    {
        byte[] data = SchematicSerializer.Serialize(CreateGrid());
        data[0] = 0x00;
        Assert.Throws<InvalidDataException>(() => SchematicSerializer.Deserialize(data));
    }

    [Fact]
    public void Rotate90_FourTimes_RestoresOriginal()
    {
        var source = CreateGrid();
        var rotated = TransformEngine.Rotate90Clockwise(source);
        Assert.Equal(3, rotated.Width);
        Assert.Equal(2, rotated.Height);
        Assert.Equal(source[0, 0].TileType, rotated[2, 0].TileType);
        Assert.Equal(source[1, 2].TileType, rotated[0, 1].TileType);

        var full = TransformEngine.Rotate90Clockwise(
            TransformEngine.Rotate90Clockwise(
                TransformEngine.Rotate90Clockwise(rotated)));
        AssertEqualGrid(source, full);
        AssertEqualGrid(source, TransformEngine.Rotate180(TransformEngine.Rotate180(source)));
        AssertEqualGrid(source, TransformEngine.Rotate270Clockwise(rotated));
    }

    [Fact]
    public void FlipAndTranslate_MoveTilesAndAnchor()
    {
        var source = CreateGrid();
        var h = TransformEngine.FlipHorizontal(source);
        Assert.Equal(source[0, 1].TileType, h[1, 1].TileType);
        Assert.Equal(0, h.AnchorX);

        var v = TransformEngine.FlipVertical(source);
        Assert.Equal(source[1, 0].TileType, v[1, 2].TileType);

        var moved = TransformEngine.Translate(source, 1, 0);
        Assert.Equal(0, moved[0, 0].TileType);
        Assert.Equal(source[0, 0].TileType, moved[1, 0].TileType);
        Assert.Equal(2, moved.AnchorX);
    }

    private static Schematic CreateGrid()
    {
        var schematic = new Schematic(2, 3)
        {
            Name = "grid",
            AnchorX = 1,
            AnchorY = 0
        };
        for (int y = 0; y < 3; y++)
        {
            for (int x = 0; x < 2; x++)
            {
                schematic[x, y] = new TileDataBlock
                {
                    TileType = (ushort)(y * 2 + x + 1),
                    HasTile = true,
                    Slope = (byte)((x + y) % 4 + 1)
                };
            }
        }

        return schematic;
    }

    private static void AssertEqualGrid(Schematic expected, Schematic actual)
    {
        Assert.Equal(expected.Width, actual.Width);
        Assert.Equal(expected.Height, actual.Height);
        Assert.Equal(expected.AnchorX, actual.AnchorX);
        Assert.Equal(expected.AnchorY, actual.AnchorY);
        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Author, actual.Author);
        Assert.Equal(expected.Tags, actual.Tags);
        for (int y = 0; y < expected.Height; y++)
        {
            for (int x = 0; x < expected.Width; x++)
                Assert.Equal(expected[x, y], actual[x, y]);
        }
    }
}
