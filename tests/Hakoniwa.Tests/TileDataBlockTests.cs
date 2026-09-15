using System.Runtime.InteropServices;
using Hakoniwa.Engine.Data;
using Xunit;

namespace Hakoniwa.Tests;

public sealed class TileDataBlockTests
{
    [Fact]
    public void ByteSize_MatchesRuntimeLayout()
    {
        Assert.Equal(14, TileDataBlock.ByteSize);
        Assert.Equal(TileDataBlock.ByteSize, Marshal.SizeOf<TileDataBlock>());
    }

    [Fact]
    public void Flags_RoundTrip()
    {
        var tile = new TileDataBlock { TileType = 1, TileFrameX = 400, Slope = 3 };
        tile.HasTile = true;
        tile.IsHalfBlock = true;
        tile.RedWire = true;
        tile.YellowWire = true;

        Assert.True(tile.HasTile);
        Assert.True(tile.IsHalfBlock);
        Assert.Equal(3, tile.Slope);
        Assert.Equal(400, tile.TileFrameX);
        Assert.True(tile.RedWire);
        Assert.False(tile.BlueWire);
        Assert.True(tile.YellowWire);
    }

    [Fact]
    public void Equality_UsesAllFields()
    {
        var a = new TileDataBlock { TileType = 6, WallType = 2, Color = 4, HasTile = true };
        var b = a;
        var c = a;
        c.WallColor = 1;
        Assert.Equal(a, b);
        Assert.NotEqual(a, c);
    }
}
