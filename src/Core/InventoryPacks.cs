using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Terraria;
using Terraria.ID;

namespace Hakoniwa.Core;

public static class InventoryPacks
{
    public static int Count => _packs.Count;
    public static int Active { get; private set; }

    private static readonly List<Pack> _packs = [];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static void Load()
    {
        _packs.Clear();
        Active = -1;
        string path = PathFor();
        if (!File.Exists(path))
            return;

        var data = JsonSerializer.Deserialize<FileData>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Invalid packs file: {path}");
        if (data.Packs is null)
            return;
        for (int i = 0; i < data.Packs.Length; i++)
        {
            var pack = data.Packs[i] ?? new Pack();
            if (string.IsNullOrWhiteSpace(pack.Name))
                pack.Name = $"背包 {i + 1}";
            _packs.Add(pack);
        }

        Active = _packs.Count == 0 ? -1 : (data.Active < 0 || data.Active >= _packs.Count ? 0 : data.Active);
    }

    public static string NameAt(int index) => _packs[index].Name;

    public static bool Occupied(int index) => _packs[index].Items is { Length: > 0 };

    public static void SwitchTo(Player player, int index)
    {
        if ((uint)index >= (uint)_packs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index == Active)
            return;

        Capture(player);
        Active = index;
        Apply(player);
        Save();
        Recipe.UpdateRecipeList();
        Sync(player);
        Notices.Post($"已切换到 {_packs[index].Name}");
    }

    public static void Add(Player player)
    {
        Capture(player);
        _packs.Add(new Pack { Name = Unique("背包"), Items = [] });
        Active = _packs.Count - 1;
        Apply(player);
        Save();
        Recipe.UpdateRecipeList();
        Sync(player);
        Notices.Post($"已新建空背包 {_packs[Active].Name}");
    }

    public static void SaveActive(Player player)
    {
        if ((uint)Active >= (uint)_packs.Count)
            return;
        Capture(player);
        Notices.Post($"已保存 {_packs[Active].Name}");
    }

    public static void Remove(Player player, int index)
    {
        if ((uint)index >= (uint)_packs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        string name = _packs[index].Name;
        if (index == Active)
            Capture(player);
        _packs.RemoveAt(index);
        if (_packs.Count == 0)
        {
            Active = -1;
            Save();
            Notices.Post($"已删除 {name}");
            return;
        }

        if (Active == index)
        {
            Active = Math.Min(index, _packs.Count - 1);
            Apply(player);
            Recipe.UpdateRecipeList();
            Sync(player);
        }
        else if (Active > index)
        {
            Active--;
        }

        Save();
        Notices.Post($"已删除 {name}");
    }

    public static void Rename(int index, string name)
    {
        if ((uint)index >= (uint)_packs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        name = name.Trim();
        if (name.Length == 0)
            return;
        if (name == _packs[index].Name)
            return;
        _packs[index].Name = Unique(name, index);
        Save();
    }

    public static void Capture(Player player)
    {
        if ((uint)Active >= (uint)_packs.Count)
            return;

        var inv = player.inventory;
        int n = 0;
        for (int i = 0; i < inv.Length; i++)
        {
            if (!inv[i].IsAir)
                n++;
        }

        var items = n == 0 ? [] : new Slot[n];
        int w = 0;
        for (int i = 0; i < inv.Length; i++)
        {
            var item = inv[i];
            if (item.IsAir)
                continue;
            items[w++] = new Slot
            {
                I = i,
                T = item.type,
                S = item.stack,
                P = item.prefix,
                F = item.favorited,
            };
        }

        _packs[Active].Items = items;
        Save();
    }

    private static void Apply(Player player)
    {
        var inv = player.inventory;
        for (int i = 0; i < inv.Length; i++)
            inv[i].TurnToAir();

        var items = _packs[Active].Items;
        if (items is null)
            return;
        for (int i = 0; i < items.Length; i++)
        {
            var slot = items[i];
            if ((uint)slot.I >= (uint)inv.Length || slot.T <= 0)
                continue;
            var item = inv[slot.I];
            item.SetDefaults(slot.T);
            item.stack = Math.Max(1, slot.S);
            if (slot.P != 0)
                item.Prefix(slot.P);
            item.favorited = slot.F;
        }
    }

    private static void Sync(Player player)
    {
        if (Main.netMode != 1)
            return;
        int n = Math.Min(player.inventory.Length, 58);
        for (int i = 0; i < n; i++)
            NetMessage.SendData(5, -1, -1, null, player.whoAmI, PlayerItemSlotID.Inventory0 + i);
    }

    private static string Unique(string name, int skip = -1)
    {
        if (!Taken(name, skip))
            return name;
        int n = 2;
        while (Taken($"{name} {n}", skip))
            n++;
        return $"{name} {n}";
    }

    private static bool Taken(string name, int skip)
    {
        for (int i = 0; i < _packs.Count; i++)
        {
            if (i != skip && _packs[i].Name == name)
                return true;
        }

        return false;
    }

    private static void Save()
    {
        var data = new FileData { Active = Active, Packs = _packs.ToArray() };
        File.WriteAllText(PathFor(), JsonSerializer.Serialize(data, Json));
    }

    private static string PathFor()
    {
        string dir = Terraria.Program.SavePath;
        if (string.IsNullOrEmpty(dir))
            throw new InvalidOperationException("Terraria SavePath is not initialized.");
        return Path.Combine(dir, "hakoniwa-packs.json");
    }

    private sealed class FileData
    {
        public int Active { get; set; }
        public Pack[]? Packs { get; set; }
    }

    private sealed class Pack
    {
        public string Name { get; set; } = "";
        public Slot[]? Items { get; set; }
    }

    private sealed class Slot
    {
        public int I { get; set; }
        public int T { get; set; }
        public int S { get; set; }
        public byte P { get; set; }
        public bool F { get; set; }
    }
}
