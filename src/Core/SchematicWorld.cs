using System;
using System.IO;
using Hakoniwa.Engine.Data;
using Terraria;
using Terraria.DataStructures;

namespace Hakoniwa.Core;

internal static class SchematicWorld
{
    public static void Capture(Schematic schematic, int originX, int originY)
    {
        schematic.Entities.Clear();
        if (Main.chest is not null)
        {
            for (int i = 0; i < Main.chest.Length; i++)
            {
                var chest = Main.chest[i];
                if (chest is null || !Inside(schematic, originX, originY, chest.x, chest.y))
                    continue;
                schematic.Entities.Add(new SchematicEntity
                {
                    Kind = SchematicEntityKind.Chest,
                    X = chest.x - originX,
                    Y = chest.y - originY,
                    Name = chest.name ?? string.Empty,
                    Items = SnapshotItems(chest.item),
                });
            }
        }

        if (Main.sign is not null)
        {
            for (int i = 0; i < Main.sign.Length; i++)
            {
                var sign = Main.sign[i];
                if (sign is null || !Inside(schematic, originX, originY, sign.x, sign.y))
                    continue;
                schematic.Entities.Add(new SchematicEntity
                {
                    Kind = SchematicEntityKind.Sign,
                    X = sign.x - originX,
                    Y = sign.y - originY,
                    Text = sign.text ?? string.Empty,
                });
            }
        }

        if (TileEntity.ByPosition is null)
            return;
        foreach (var pair in TileEntity.ByPosition)
        {
            int x = pair.Key.X;
            int y = pair.Key.Y;
            if (!Inside(schematic, originX, originY, x, y))
                continue;
            schematic.Entities.Add(new SchematicEntity
            {
                Kind = SchematicEntityKind.TileEntity,
                X = x - originX,
                Y = y - originY,
                TeType = pair.Value.type,
                Extra = WriteExtra(pair.Value),
            });
        }
    }

    public static void Paste(Schematic schematic, int originX, int originY)
    {
        for (int i = 0; i < schematic.Entities.Count; i++)
        {
            var entity = schematic.Entities[i];
            int x = originX + entity.X - schematic.AnchorX;
            int y = originY + entity.Y - schematic.AnchorY;
            if (!WorldGen.InWorld(x, y))
                continue;
            switch (entity.Kind)
            {
                case SchematicEntityKind.Chest:
                    PasteChest(entity, x, y);
                    break;
                case SchematicEntityKind.Sign:
                    PasteSign(entity, x, y);
                    break;
                case SchematicEntityKind.TileEntity:
                    PasteTileEntity(entity, x, y);
                    break;
            }
        }
    }

    public static void RemoveAt(Schematic schematic, int originX, int originY)
    {
        for (int i = 0; i < schematic.Entities.Count; i++)
        {
            var entity = schematic.Entities[i];
            RemoveOne(originX + entity.X, originY + entity.Y, entity.Kind);
        }
    }

    public static void RemoveRect(int x, int y, int width, int height)
    {
        int x2 = x + width;
        int y2 = y + height;
        if (Main.chest is not null)
        {
            for (int i = 0; i < Main.chest.Length; i++)
            {
                var chest = Main.chest[i];
                if (chest is not null && chest.x >= x && chest.x < x2 && chest.y >= y && chest.y < y2)
                    Chest.RemoveChest(i);
            }
        }

        if (Main.sign is not null)
        {
            for (int i = 0; i < Main.sign.Length; i++)
            {
                var sign = Main.sign[i];
                if (sign is not null && sign.x >= x && sign.x < x2 && sign.y >= y && sign.y < y2)
                    Main.sign[i] = null;
            }
        }

        if (TileEntity.ByPosition is null)
            return;
        var remove = new System.Collections.Generic.List<TileEntity>();
        foreach (var pair in TileEntity.ByPosition)
        {
            if (pair.Key.X >= x && pair.Key.X < x2 && pair.Key.Y >= y && pair.Key.Y < y2)
                remove.Add(pair.Value);
        }

        for (int i = 0; i < remove.Count; i++)
            TileEntity.Remove(remove[i]);
    }

    private static void RemoveOne(int x, int y, SchematicEntityKind kind)
    {
        switch (kind)
        {
            case SchematicEntityKind.Chest:
                int chest = Chest.FindChest(x, y);
                if (chest >= 0)
                    Chest.RemoveChest(chest);
                break;
            case SchematicEntityKind.Sign:
                Sign.KillSign(x, y);
                break;
            case SchematicEntityKind.TileEntity:
                if (TileEntity.ByPosition is not null && TileEntity.ByPosition.TryGetValue(new Point16(x, y), out var te))
                    TileEntity.Remove(te);
                break;
        }
    }

    private static void PasteChest(SchematicEntity entity, int x, int y)
    {
        int id = Chest.FindChest(x, y);
        if (id < 0)
            id = Chest.FindEmptyChest(x, y);
        if (id < 0)
            return;
        if (Main.chest[id] is null)
            Chest.CreateWorldChest(id, x, y);
        var chest = Main.chest[id];
        chest.name = entity.Name;
        int slots = Math.Min(entity.Items.Length, chest.maxItems);
        for (int i = 0; i < chest.maxItems; i++)
        {
            if (chest.item[i] is null)
                chest.item[i] = new Item();
            else
                chest.item[i].TurnToAir();
            if (i >= slots || entity.Items[i].Type <= 0)
                continue;
            chest.item[i].SetDefaults(entity.Items[i].Type);
            chest.item[i].stack = entity.Items[i].Stack;
            if (entity.Items[i].Prefix != 0)
                chest.item[i].Prefix(entity.Items[i].Prefix);
        }

        if (Main.netMode == 0)
            return;
        for (int i = 0; i < chest.maxItems; i++)
            NetMessage.SendData(32, -1, -1, null, id, i);
    }

    private static void PasteSign(SchematicEntity entity, int x, int y)
    {
        int id = Sign.ReadSign(x, y);
        if (id < 0)
            return;
        Sign.TextSign(id, entity.Text);
        if (Main.netMode != 0)
            NetMessage.SendData(47, -1, -1, null, id, Main.myPlayer);
    }

    private static void PasteTileEntity(SchematicEntity entity, int x, int y)
    {
        if (TileEntity.manager is null)
            return;
        var pos = new Point16(x, y);
        if (TileEntity.ByPosition is not null && TileEntity.ByPosition.TryGetValue(pos, out var existing))
            TileEntity.Remove(existing);
        var te = TileEntity.manager.GenerateInstance(entity.TeType);
        if (te is null)
            return;
        te.type = entity.TeType;
        te.Position = pos;
        te.ID = TileEntity.AssignNewID();
        if (entity.Extra.Length > 0)
        {
            using var stream = new MemoryStream(entity.Extra);
            using var reader = new BinaryReader(stream);
            te.ReadExtraData(reader, Main.curRelease, true);
        }

        TileEntity.Add(te);
    }

    private static byte[] WriteExtra(TileEntity entity)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream))
            entity.WriteExtraData(writer, true);
        return stream.ToArray();
    }

    private static SchematicItem[] SnapshotItems(Item[] items)
    {
        if (items is null || items.Length == 0)
            return [];
        var snaps = new SchematicItem[items.Length];
        for (int i = 0; i < items.Length; i++)
        {
            var item = items[i];
            if (item is null || item.type <= 0 || item.stack <= 0)
                continue;
            snaps[i] = new SchematicItem(item.type, item.stack, item.prefix);
        }

        return snaps;
    }

    private static bool Inside(Schematic schematic, int originX, int originY, int x, int y)
        => x >= originX && y >= originY && x < originX + schematic.Width && y < originY + schematic.Height;
}
