using System;

namespace Hakoniwa.Engine.Data;

public enum SchematicEntityKind : byte
{
    Chest = 0,
    Sign = 1,
    TileEntity = 2,
}

public readonly struct SchematicItem
{
    public readonly int Type;
    public readonly int Stack;
    public readonly byte Prefix;

    public SchematicItem(int type, int stack, byte prefix)
    {
        Type = type;
        Stack = stack;
        Prefix = prefix;
    }
}

public sealed class SchematicEntity
{
    public SchematicEntityKind Kind;
    public int X;
    public int Y;
    public string Name = string.Empty;
    public string Text = string.Empty;
    public SchematicItem[] Items = [];
    public byte TeType;
    public byte[] Extra = [];

    public SchematicEntity Clone() => Relocate(X, Y);

    public SchematicEntity Relocate(int x, int y) => new()
    {
        Kind = Kind,
        X = x,
        Y = y,
        Name = Name,
        Text = Text,
        Items = Items.Length == 0 ? [] : (SchematicItem[])Items.Clone(),
        TeType = TeType,
        Extra = Extra.Length == 0 ? [] : (byte[])Extra.Clone(),
    };
}
