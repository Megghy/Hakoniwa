using Terraria;

namespace Hakoniwa.Core;

public enum ItemCategory
{
    All = 0,
    Tools = 1,
    Armor = 2,
    Vanity = 3,
    Dyes = 4,
}

public static class ItemCatalog
{
    public static bool Matches(Item item, ItemCategory category) => category switch
    {
        ItemCategory.All => true,
        ItemCategory.Tools => item.pick > 0 || item.axe > 0 || item.hammer > 0 || item.fishingPole > 0,
        ItemCategory.Armor => !item.vanity && (item.headSlot >= 0 || item.bodySlot >= 0 || item.legSlot >= 0),
        ItemCategory.Vanity => item.vanity,
        ItemCategory.Dyes => item.dye > 0,
        _ => throw new System.ArgumentOutOfRangeException(nameof(category), category, "Unknown item category."),
    };
}
