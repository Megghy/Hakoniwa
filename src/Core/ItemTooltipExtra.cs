using System;
using Microsoft.Xna.Framework;
using Terraria;

namespace Hakoniwa.Core;

internal static class ItemTooltipExtra
{
    private static readonly Color Tint = new(150, 210, 255);

    public static void Install(HookManager hooks)
    {
        var method = typeof(Main).GetMethod(nameof(Main.MouseText_DrawItemTooltip_GetLinesInfo));
        if (method is null)
            throw new MissingMethodException(typeof(Main).FullName, nameof(Main.MouseText_DrawItemTooltip_GetLinesInfo));
        hooks.RegisterDetour(method, AfterLines);
    }

    private static void AfterLines(
        Orig orig,
        Item item,
        ref int yoyoLogo,
        float oldKb,
        ref int numLines,
        string[] lines,
        Color[] colors)
    {
        orig(item, ref yoyoLogo, oldKb, ref numLines, lines, colors);
        Add(ref numLines, lines, colors, $"物品 #{item.type}");
        if (item.shoot > 0)
            Add(ref numLines, lines, colors, $"弹幕: {Label(Lang.GetProjectileName(item.shoot).Value, item.shoot)}");
        if (item.useTime > 0 && (item.damage > 0 || item.shoot > 0 || item.mana > 0 || item.reuseDelay > 0))
        {
            Add(ref numLines, lines, colors, $"使用间隔: {item.useTime} 帧 ({60f / item.useTime:0.##}/秒)");
            if (item.useAnimation != item.useTime)
                Add(ref numLines, lines, colors, $"使用动画: {item.useAnimation} 帧");
        }

        if (item.reuseDelay > 0)
            Add(ref numLines, lines, colors, $"冷却: {item.reuseDelay} 帧");
        if (item.shootSpeed > 0f)
            Add(ref numLines, lines, colors, $"弹幕速度: {item.shootSpeed:0.##}");
        if (item.useAmmo > 0)
            Add(ref numLines, lines, colors, $"弹药: {Label(Lang.GetItemNameValue(item.useAmmo), item.useAmmo)}");
        if (item.autoReuse)
            Add(ref numLines, lines, colors, "自动挥舞");
        if (item.channel)
            Add(ref numLines, lines, colors, "持续施法");
        if (item.buffType > 0)
            Add(ref numLines, lines, colors, $"Buff: {Label(Lang.GetBuffName(item.buffType), item.buffType)}");
        if (item.createTile >= 0)
            Add(ref numLines, lines, colors, $"物块 #{item.createTile}");
        if (item.createWall > 0)
            Add(ref numLines, lines, colors, $"墙壁 #{item.createWall}");
        if (item.armorPenetration > 0)
            Add(ref numLines, lines, colors, $"穿甲: {item.armorPenetration}");
    }

    private static void Add(ref int n, string[] lines, Color[] colors, string text)
    {
        if ((uint)n >= (uint)lines.Length)
            return;
        lines[n] = text;
        colors[n] = Tint;
        n++;
    }

    private static string Label(string name, int id)
        => string.IsNullOrEmpty(name) ? $"#{id}" : $"{name} ({id})";

    private delegate void Orig(Item item, ref int yoyoLogo, float oldKb, ref int numLines, string[] lines, Color[] colors);
}
