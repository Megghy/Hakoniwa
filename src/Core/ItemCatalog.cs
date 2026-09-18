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
    private static ItemGroup[]? _groupByType;

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
        var probe = new Item();
        var subs = Subs(category);
        var filter = subs.Length == 0 ? Sub.All : subs[sub];
        for (int id = 1; id < ItemID.Count; id++)
        {
            if (category == ItemCategory.Favorites && !favorites.Contains(id))
                continue;
            string name = Lang.GetItemNameValue(id);
            if (name.Length == 0)
                continue;
            if (query.Length > 0 && name.IndexOf(query, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (category != ItemCategory.Favorites)
            {
                probe.SetDefaults(id);
                if (!Matches(probe, category, filter))
                    continue;
            }

            hits.Add(id);
        }
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

    public static bool Matches(Item item, ItemCategory category, Sub sub)
    {
        var group = GroupOf(item);
        if (!InCategory(item, category, group))
            return false;
        if (sub.Groups.Length > 0 && !Contains(sub.Groups, group))
            return false;
        if (!sub.PlatformsOnly && !sub.ExcludePlatforms)
            return true;
        bool platform = IsPlatform(item);
        if (sub.PlatformsOnly)
            return platform;
        return !platform;
    }

    private static bool InCategory(Item item, ItemCategory category, ItemGroup group) => category switch
    {
        ItemCategory.Favorites or ItemCategory.All => true,
        ItemCategory.Tools => group is ItemGroup.Pickaxe or ItemGroup.Axe or ItemGroup.Hammer or ItemGroup.FishingRods,
        ItemCategory.Armor => !item.vanity && group is ItemGroup.Headgear or ItemGroup.Torso or ItemGroup.Pants,
        ItemCategory.Vanity => item.vanity && group is ItemGroup.Headgear or ItemGroup.Torso or ItemGroup.Pants or ItemGroup.Accessories,
        ItemCategory.Dyes => group is ItemGroup.Dye or ItemGroup.HairDye,
        ItemCategory.Blocks => item.createTile >= 0 || item.createWall > 0,
        _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown item category."),
    };

    private static ItemGroup GroupOf(Item item)
    {
        _groupByType ??= new ItemGroup[ItemID.Count];
        ref var cached = ref _groupByType[item.type];
        if (cached == 0)
            cached = ContentSamples.CreativeHelper.GetItemGroup(item, out _);
        return cached;
    }

    private static bool IsPlatform(Item item) =>
        item.createTile >= 0 && TileID.Sets.Platforms[item.createTile];

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
