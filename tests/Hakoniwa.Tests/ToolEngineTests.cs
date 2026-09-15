using System;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Xunit;

namespace Hakoniwa.Tests;

public sealed class ToolEngineTests
{
    [Fact]
    public void BrushShapes_PaintExpectedCounts()
    {
        var world = new TileAccessor(16, 16);
        var stamp = Stone(1);
        Assert.Equal(1, ToolEngine.Paint(world, 8, 8, 0, BrushShape.Square, stamp));
        world = new TileAccessor(16, 16);
        Assert.Equal(9, ToolEngine.Paint(world, 8, 8, 1, BrushShape.Square, stamp));
        world = new TileAccessor(16, 16);
        Assert.Equal(5, ToolEngine.Paint(world, 8, 8, 1, BrushShape.Circle, stamp));
        world = new TileAccessor(16, 16);
        Assert.Equal(5, ToolEngine.Paint(world, 8, 8, 1, BrushShape.Diamond, stamp));
        world = new TileAccessor(16, 16);
        Assert.Equal(25, ToolEngine.Paint(world, 8, 8, 2, BrushShape.Square, stamp));
        world = new TileAccessor(16, 16);
        Assert.Equal(13, ToolEngine.Paint(world, 8, 8, 2, BrushShape.Circle, stamp));
        world = new TileAccessor(16, 16);
        Assert.Equal(13, ToolEngine.Paint(world, 8, 8, 2, BrushShape.Diamond, stamp));
    }

    [Fact]
    public void Scatter_SkipsEveryCellWhenOne()
    {
        var world = new TileAccessor(8, 8);
        int painted = ToolEngine.Paint(world, 4, 4, 2, BrushShape.Square, Stone(1), scatter: 1, random: new Random(1));
        Assert.Equal(0, painted);
    }

    [Fact]
    public void Erase_ClearsOnlySelectedLayer()
    {
        var world = new TileAccessor(4, 4);
        var stamp = Stone(7);
        stamp.WallType = 3;
        stamp.Color = 5;
        ToolEngine.Paint(world, 1, 1, 0, BrushShape.Square, stamp);
        ToolEngine.Erase(world, 1, 1, 0, BrushShape.Square, TileLayer.Tile);
        var tile = world.Get(1, 1);
        Assert.False(tile.HasTile);
        Assert.Equal(3, tile.WallType);
        Assert.Equal(5, tile.Color);
    }

    [Fact]
    public void FloodFill_IsFourConnected()
    {
        var world = new TileAccessor(5, 5);
        var fill = Stone(9);
        Assert.Equal(25, ToolEngine.FloodFill(world, 2, 2, fill));

        world = new TileAccessor(3, 3);
        world.Set(1, 1, Stone(1));
        world.Set(0, 0, Stone(1));
        Assert.Equal(1, ToolEngine.FloodFill(world, 1, 1, Stone(2), TileLayer.Tile));
        Assert.Equal(2, world.Get(1, 1).TileType);
        Assert.Equal(1, world.Get(0, 0).TileType);
    }

    [Fact]
    public void ExtractPaste_HonorsAnchorAndClips()
    {
        var world = new TileAccessor(6, 6);
        ToolEngine.Paint(world, 1, 1, 0, BrushShape.Square, Stone(4));
        var schematic = ToolEngine.Extract(world, 1, 1, 2, 2);
        schematic.AnchorX = 0;
        schematic.AnchorY = 0;
        int pasted = ToolEngine.Paste(world, schematic, 5, 5);
        Assert.Equal(1, pasted);
        Assert.Equal(4, world.Get(5, 5).TileType);
    }

    [Fact]
    public void History_UndoRedoAndCapacityDrop()
    {
        var world = new TileAccessor(8, 8);
        var history = new HistoryStack(2);
        ToolEngine.Paint(world, 2, 2, 0, BrushShape.Square, Stone(1), history: history);
        ToolEngine.Paint(world, 3, 3, 0, BrushShape.Square, Stone(2), history: history);
        ToolEngine.Paint(world, 4, 4, 0, BrushShape.Square, Stone(3), history: history);

        Assert.True(history.Undo(world));
        Assert.Equal(0, world.Get(4, 4).TileType);
        Assert.True(history.Redo(world));
        Assert.Equal(3, world.Get(4, 4).TileType);
        Assert.True(history.Undo(world));
        Assert.True(history.Undo(world));
        Assert.False(history.Undo(world));
        Assert.Equal(1, world.Get(2, 2).TileType);
        Assert.Equal(0, world.Get(3, 3).TileType);
        Assert.Equal(0, world.Get(4, 4).TileType);
    }

    [Fact]
    public void TileAccessor_RejectsOutOfBounds()
    {
        var world = new TileAccessor(2, 2);
        Assert.Throws<ArgumentOutOfRangeException>(() => world.Get(2, 0));
        Assert.True(world.InBounds(1, 1));
    }

    [Fact]
    public void Selection_TracksRectangle()
    {
        var selection = new Selection();
        Assert.Equal(0, selection.Width);
        selection.Begin(4, 1);
        selection.DragTo(1, 5);
        Assert.Equal(1, selection.MinX);
        Assert.Equal(4, selection.Width);
        Assert.Equal(5, selection.Height);
        selection.Clear();
        Assert.False(selection.Active);
    }

    private static TileDataBlock Stone(ushort type)
    {
        var tile = new TileDataBlock { TileType = type };
        tile.HasTile = true;
        return tile;
    }
}
