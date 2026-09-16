using System;
using System.Runtime.InteropServices;

namespace Hakoniwa.Engine.Data;

/// <summary>
/// Compact tile snapshot. FrameXY stay as short because Terraria stores frameX/frameY as short;
/// packing them into byte would clip furniture and trees. Pack=1 yields 14 bytes per cell.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct TileDataBlock : IEquatable<TileDataBlock>
{
    public const int ByteSize = 14;

    public ushort TileType;
    public ushort WallType;
    public short TileFrameX;
    public short TileFrameY;
    public byte Liquid;
    public byte LiquidType;
    public byte Flags;
    public byte Color;
    public byte WallColor;
    public byte WireFlags;

    public bool HasTile
    {
        readonly get => (Flags & 0x01) != 0;
        set => Flags = (byte)(value ? (Flags | 0x01) : (Flags & ~0x01));
    }

    public bool IsHalfBlock
    {
        readonly get => (Flags & 0x02) != 0;
        set => Flags = (byte)(value ? (Flags | 0x02) : (Flags & ~0x02));
    }

    public bool HasActuator
    {
        readonly get => (Flags & 0x04) != 0;
        set => Flags = (byte)(value ? (Flags | 0x04) : (Flags & ~0x04));
    }

    public bool IsActuated
    {
        readonly get => (Flags & 0x08) != 0;
        set => Flags = (byte)(value ? (Flags | 0x08) : (Flags & ~0x08));
    }

    public byte Slope
    {
        readonly get => (byte)((Flags >> 4) & 0x07);
        set => Flags = (byte)((Flags & 0x8F) | ((value & 0x07) << 4));
    }

    public bool Skip
    {
        readonly get => (Flags & 0x80) != 0;
        set => Flags = (byte)(value ? (Flags | 0x80) : (Flags & ~0x80));
    }

    public bool RedWire
    {
        readonly get => (WireFlags & 0x01) != 0;
        set => WireFlags = (byte)(value ? (WireFlags | 0x01) : (WireFlags & ~0x01));
    }

    public bool BlueWire
    {
        readonly get => (WireFlags & 0x02) != 0;
        set => WireFlags = (byte)(value ? (WireFlags | 0x02) : (WireFlags & ~0x02));
    }

    public bool GreenWire
    {
        readonly get => (WireFlags & 0x04) != 0;
        set => WireFlags = (byte)(value ? (WireFlags | 0x04) : (WireFlags & ~0x04));
    }

    public bool YellowWire
    {
        readonly get => (WireFlags & 0x08) != 0;
        set => WireFlags = (byte)(value ? (WireFlags | 0x08) : (WireFlags & ~0x08));
    }

    public readonly bool Equals(TileDataBlock other) =>
        TileType == other.TileType
        && WallType == other.WallType
        && TileFrameX == other.TileFrameX
        && TileFrameY == other.TileFrameY
        && Liquid == other.Liquid
        && LiquidType == other.LiquidType
        && Flags == other.Flags
        && Color == other.Color
        && WallColor == other.WallColor
        && WireFlags == other.WireFlags;

    public override readonly bool Equals(object? obj) => obj is TileDataBlock other && Equals(other);

    public override readonly int GetHashCode()
    {
        unchecked
        {
            int hash = TileType;
            hash = (hash * 397) ^ WallType;
            hash = (hash * 397) ^ (ushort)TileFrameX;
            hash = (hash * 397) ^ (ushort)TileFrameY;
            hash = (hash * 397) ^ Liquid;
            hash = (hash * 397) ^ LiquidType;
            hash = (hash * 397) ^ Flags;
            hash = (hash * 397) ^ Color;
            hash = (hash * 397) ^ WallColor;
            hash = (hash * 397) ^ WireFlags;
            return hash;
        }
    }

    public static bool operator ==(TileDataBlock left, TileDataBlock right) => left.Equals(right);
    public static bool operator !=(TileDataBlock left, TileDataBlock right) => !left.Equals(right);
}
