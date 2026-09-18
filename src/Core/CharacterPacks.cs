using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace Hakoniwa.Core;

public static class CharacterPacks
{
    public static int Count => _packs.Count;
    public static int Active { get; private set; } = -1;

    private static readonly List<Look> _packs = [];
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    private static Look? _committed;
    private static string? _bound;

    public static void Load()
    {
        _packs.Clear();
        Active = -1;
        string path = PathFor();
        if (!File.Exists(path))
            return;

        var data = JsonSerializer.Deserialize<FileData>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Invalid character packs file: {path}");
        if (data.Packs is null)
            return;
        for (int i = 0; i < data.Packs.Length; i++)
        {
            var pack = data.Packs[i] ?? new Look();
            if (string.IsNullOrWhiteSpace(pack.Name))
                pack.Name = $"外观 {i + 1}";
            _packs.Add(pack);
        }

        Active = _packs.Count == 0 ? -1 : (data.Active < 0 || data.Active >= _packs.Count ? 0 : data.Active);
    }

    public static void Tick()
    {
        if (Main.gameMenu || Main.myPlayer < 0 || !Main.LocalPlayer.active)
        {
            _bound = null;
            return;
        }

        string path = Main.ActivePlayerFileData?.Path ?? "";
        if (path.Length == 0 || path == _bound)
            return;
        _bound = path;
        _committed = From(Main.LocalPlayer);
    }

    public static string NameAt(int index) => _packs[index].Name;

    public static bool Dirty(Player player) => _committed is not null && !Same(From(player), _committed);

    public static void SwitchTo(Player player, int index)
    {
        if ((uint)index >= (uint)_packs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (index == Active)
            return;
        Active = index;
        Apply(_packs[index], player);
        Save();
        Sync(player);
        Notices.Post($"已切换到 {_packs[index].Name}（未写入角色文件）");
    }

    public static void Add(Player player)
    {
        var look = From(player);
        look.Name = Unique("外观");
        _packs.Add(look);
        Active = _packs.Count - 1;
        Save();
        Notices.Post($"已新建 {_packs[Active].Name}");
    }

    public static void Remove(Player player, int index)
    {
        if ((uint)index >= (uint)_packs.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        string name = _packs[index].Name;
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
            Apply(_packs[Active], player);
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
        if (name.Length == 0 || name == _packs[index].Name)
            return;
        _packs[index].Name = Unique(name, index);
        Save();
    }

    public static void Commit(Player player)
    {
        _committed = From(player);
        if ((uint)Active < (uint)_packs.Count)
        {
            var pack = From(player);
            pack.Name = _packs[Active].Name;
            _packs[Active] = pack;
        }

        Save();
        if (Main.ActivePlayerFileData is { } file)
            Player.SavePlayer(file);
        Notices.Post("已保存到角色存档");
    }

    public static void WriteThrough(Player player, Action save)
    {
        if (player is null)
            throw new ArgumentNullException(nameof(player));
        if (save is null)
            throw new ArgumentNullException(nameof(save));
        if (_committed is null)
        {
            save();
            return;
        }

        var live = From(player);
        Apply(_committed, player);
        try
        {
            save();
        }
        finally
        {
            Apply(live, player);
        }
    }

    private static void Sync(Player player)
    {
        ContentSamples.FixItemsUsingPlayerColours();
        if (Main.netMode != 1)
            return;
        NetMessage.SendData(4, -1, -1, null, player.whoAmI);
        NetMessage.SendData(16, -1, -1, null, player.whoAmI);
        NetMessage.SendData(42, -1, -1, null, player.whoAmI);
    }

    private static Look From(Player player) => new()
    {
        Male = player.Male,
        Hair = player.hair,
        SkinVariant = player.skinVariant,
        Skin = player.skinColor.PackedValue,
        Eyes = player.eyeColor.PackedValue,
        HairColor = player.hairColor.PackedValue,
        Shirt = player.shirtColor.PackedValue,
        Undershirt = player.underShirtColor.PackedValue,
        Pants = player.pantsColor.PackedValue,
        Shoes = player.shoeColor.PackedValue,
        Life = player.statLife,
        LifeMax = player.statLifeMax,
        Mana = player.statMana,
        ManaMax = player.statManaMax,
        Voice = player.voiceVariant,
        Difficulty = player.difficulty,
        ExtraAccessory = player.extraAccessory,
        AegisCrystal = player.usedAegisCrystal,
        AegisFruit = player.usedAegisFruit,
        ArcaneCrystal = player.usedArcaneCrystal,
        GalaxyPearl = player.usedGalaxyPearl,
        GummyWorm = player.usedGummyWorm,
        Ambrosia = player.usedAmbrosia,
    };

    private static void Apply(Look look, Player player)
    {
        player.Male = look.Male;
        player.hair = look.Hair;
        player.skinVariant = look.SkinVariant;
        player.skinColor = Unpack(look.Skin);
        player.eyeColor = Unpack(look.Eyes);
        player.hairColor = Unpack(look.HairColor);
        player.shirtColor = Unpack(look.Shirt);
        player.underShirtColor = Unpack(look.Undershirt);
        player.pantsColor = Unpack(look.Pants);
        player.shoeColor = Unpack(look.Shoes);
        player.statLifeMax = look.LifeMax;
        player.statLife = look.Life;
        player.statManaMax = look.ManaMax;
        player.statMana = look.Mana;
        if (look.Voice != 0)
            player.voiceVariant = look.Voice;
        player.difficulty = look.Difficulty;
        player.extraAccessory = look.ExtraAccessory;
        player.usedAegisCrystal = look.AegisCrystal;
        player.usedAegisFruit = look.AegisFruit;
        player.usedArcaneCrystal = look.ArcaneCrystal;
        player.usedGalaxyPearl = look.GalaxyPearl;
        player.usedGummyWorm = look.GummyWorm;
        player.usedAmbrosia = look.Ambrosia;
    }

    private static bool Same(Look a, Look b) =>
        a.Male == b.Male && a.Hair == b.Hair && a.SkinVariant == b.SkinVariant
        && a.Skin == b.Skin && a.Eyes == b.Eyes && a.HairColor == b.HairColor
        && a.Shirt == b.Shirt && a.Undershirt == b.Undershirt && a.Pants == b.Pants && a.Shoes == b.Shoes
        && a.Life == b.Life && a.LifeMax == b.LifeMax && a.Mana == b.Mana && a.ManaMax == b.ManaMax
        && a.Voice == b.Voice && a.Difficulty == b.Difficulty && a.ExtraAccessory == b.ExtraAccessory
        && a.AegisCrystal == b.AegisCrystal && a.AegisFruit == b.AegisFruit
        && a.ArcaneCrystal == b.ArcaneCrystal && a.GalaxyPearl == b.GalaxyPearl
        && a.GummyWorm == b.GummyWorm && a.Ambrosia == b.Ambrosia;

    private static Color Unpack(uint packed) => new() { PackedValue = packed };

    private static string Unique(string name, int skip = -1)
    {
        if (!Taken(name, skip))
            return name;
        for (int n = 2; ; n++)
        {
            string next = $"{name} {n}";
            if (!Taken(next, skip))
                return next;
        }
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
        return Path.Combine(dir, "hakoniwa-characters.json");
    }

    private sealed class FileData
    {
        public int Active { get; set; }
        public Look[]? Packs { get; set; }
    }

    private sealed class Look
    {
        public string Name { get; set; } = "";
        public bool Male { get; set; } = true;
        public int Hair { get; set; }
        public int SkinVariant { get; set; }
        public uint Skin { get; set; }
        public uint Eyes { get; set; }
        public uint HairColor { get; set; }
        public uint Shirt { get; set; }
        public uint Undershirt { get; set; }
        public uint Pants { get; set; }
        public uint Shoes { get; set; }
        public int Life { get; set; } = 100;
        public int LifeMax { get; set; } = 100;
        public int Mana { get; set; }
        public int ManaMax { get; set; }
        public int Voice { get; set; }
        public byte Difficulty { get; set; }
        public bool ExtraAccessory { get; set; }
        public bool AegisCrystal { get; set; }
        public bool AegisFruit { get; set; }
        public bool ArcaneCrystal { get; set; }
        public bool GalaxyPearl { get; set; }
        public bool GummyWorm { get; set; }
        public bool Ambrosia { get; set; }
    }
}
