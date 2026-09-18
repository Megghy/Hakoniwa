using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using ItemGroup = Terraria.ID.ContentSamples.CreativeHelper.ItemGroup;

namespace Hakoniwa.Core;

public enum ItemCategory
{
    Favorites = 0,
    All = 1,
    Tools = 2,
    Armor = 3,
    Vanity = 4,
    Dyes = 5,
    Blocks = 6,
}

public static class ItemCatalog
{
    public readonly struct Sub
    {
        public string Name { get; init; }
        public ItemGroup[] Groups { get; init; }
        public bool PlatformsOnly { get; init; }
        public bool ExcludePlatforms { get; init; }

        public static Sub All => new() { Name = "全部", Groups = [] };

        public static Sub Of(string name, params ItemGroup[] groups) =>
            new() { Name = name, Groups = groups };
    }

    public static readonly string[] Names = ["收藏", "全部", "工具", "盔甲", "时装", "染料", "物块"];

    private static readonly Sub[] ToolSubs =
    [
        Sub.All,
        Sub.Of("镐", ItemGroup.Pickaxe),
        Sub.Of("斧", ItemGroup.Axe),
        Sub.Of("锤", ItemGroup.Hammer),
        Sub.Of("钓竿", ItemGroup.FishingRods),
    ];

    private static readonly Sub[] ArmorSubs =
    [
        Sub.All,
        Sub.Of("头盔", ItemGroup.Headgear),
        Sub.Of("胸甲", ItemGroup.Torso),
        Sub.Of("腿甲", ItemGroup.Pants),
    ];

    private static readonly Sub[] VanitySubs =
    [
        Sub.All,
        Sub.Of("头饰", ItemGroup.Headgear),
        Sub.Of("上衣", ItemGroup.Torso),
        Sub.Of("裤装", ItemGroup.Pants),
        Sub.Of("配饰", ItemGroup.Accessories),
    ];

    private static readonly Sub[] DyeSubs =
    [
        Sub.All,
        Sub.Of("染料", ItemGroup.Dye),
        Sub.Of("发染", ItemGroup.HairDye),
    ];

    private static readonly Sub[] BlockSubs =
    [
        Sub.All,
        Sub.Of("物块", ItemGroup.Blocks, ItemGroup.Wood),
        Sub.Of("墙壁", ItemGroup.Walls),
        new() { Name = "家具", Groups = [ItemGroup.PlaceableObjects], ExcludePlatforms = true },
        new() { Name = "平台", Groups = [ItemGroup.PlaceableObjects], PlatformsOnly = true },
    ];

    private static readonly Sub[] NoSubs = [];

    private readonly struct Record(int id, string name, ItemGroup group, bool vanity, bool tile, bool wall, bool platform)
    {
        public int Id { get; } = id;
        public string Name { get; } = name;
        public ItemGroup Group { get; } = group;
        public bool Vanity { get; } = vanity;
        public bool Tile { get; } = tile;
        public bool Wall { get; } = wall;
        public bool Platform { get; } = platform;
    }

    private static Record[]? _records;

    public static Sub[] Subs(ItemCategory category) => category switch
    {
        ItemCategory.Tools => ToolSubs,
        ItemCategory.Armor => ArmorSubs,
        ItemCategory.Vanity => VanitySubs,
        ItemCategory.Dyes => DyeSubs,
        ItemCategory.Blocks => BlockSubs,
        _ => NoSubs,
    };

    public static void Search(string query, ItemCategory category, int sub, ISet<int> favorites, List<int> hits)
    {
        hits.Clear();
        var subs = Subs(category);
        var filter = subs.Length == 0 ? Sub.All : subs[sub];
        foreach (var rec in Records())
        {
            if (category == ItemCategory.Favorites && !favorites.Contains(rec.Id))
                continue;
            if (query.Length > 0 && rec.Name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (category != ItemCategory.Favorites && !Matches(rec, category, filter))
                continue;
            hits.Add(rec.Id);
        }
    }

    private static Record[] Records()
    {
        if (_records is not null)
            return _records;
        var list = new List<Record>(ItemID.Count);
        var probe = new Item();
        for (int id = 1; id < ItemID.Count; id++)
        {
            string name = Lang.GetItemNameValue(id);
            if (name.Length == 0)
                continue;
            probe.SetDefaults(id);
            list.Add(new Record(
                id,
                name,
                ContentSamples.CreativeHelper.GetItemGroup(probe, out _),
                probe.vanity,
                probe.createTile >= 0,
                probe.createWall > 0,
                probe.createTile >= 0 && TileID.Sets.Platforms[probe.createTile]));
        }

        _records = list.ToArray();
        return _records;
    }

    public static bool GiveToCursor(int type)
    {
        if (Main.gameMenu || !Main.LocalPlayer.active)
            return false;

        Main.mouseItem.SetDefaults(type);
        Main.mouseItem.stack = Math.Max(1, Main.mouseItem.maxStack);
        Notices.Post($"已拿到 {Lang.GetItemNameValue(type)} x{Main.mouseItem.stack}");
        return true;
    }

    public static bool Give(int type, bool fullStack)
    {
        if (Main.gameMenu || !Main.LocalPlayer.active)
            return false;

        var item = new Item();
        item.SetDefaults(type);
        int max = Math.Max(1, item.maxStack);
        item.stack = fullStack ? max : 1;
        int given = item.stack;
        var leftover = Main.LocalPlayer.GetItem(item, GetItemSettings.ItemCreatedFromItemUsage);
        bool leftovered = leftover.type != 0 && leftover.stack > 0;
        int accepted = leftovered ? given - leftover.stack : given;
        if (accepted <= 0)
        {
            Notices.Post("背包已满");
            return false;
        }

        Notices.Post($"已放入 {Lang.GetItemNameValue(type)} x{accepted}");
        return true;
    }

    private static bool Matches(Record rec, ItemCategory category, Sub sub)
    {
        if (!InCategory(rec, category))
            return false;
        if (sub.Groups.Length > 0 && !Contains(sub.Groups, rec.Group))
            return false;
        if (!sub.PlatformsOnly && !sub.ExcludePlatforms)
            return true;
        return sub.PlatformsOnly ? rec.Platform : !rec.Platform;
    }

    private static bool InCategory(Record rec, ItemCategory category) => category switch
    {
        ItemCategory.Favorites or ItemCategory.All => true,
        ItemCategory.Tools => rec.Group is ItemGroup.Pickaxe or ItemGroup.Axe or ItemGroup.Hammer or ItemGroup.FishingRods,
        ItemCategory.Armor => !rec.Vanity && rec.Group is ItemGroup.Headgear or ItemGroup.Torso or ItemGroup.Pants,
        ItemCategory.Vanity => rec.Vanity && rec.Group is ItemGroup.Headgear or ItemGroup.Torso or ItemGroup.Pants or ItemGroup.Accessories,
        ItemCategory.Dyes => rec.Group is ItemGroup.Dye or ItemGroup.HairDye,
        ItemCategory.Blocks => rec.Tile || rec.Wall,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown item category."),
    };

    private static bool Contains(ItemGroup[] groups, ItemGroup group)
    {
        foreach (var g in groups)
        {
            if (g == group)
                return true;
        }

        return false;
    }
}
