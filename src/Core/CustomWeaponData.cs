using System;
using System.Globalization;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria;

namespace Hakoniwa.Core;

/// <summary>
/// 自定义物品/武器数据模型 (包含网络包 Packet 88 / ItemTweaker 同步的所有属性，兼容 CustomWeapon 插件)
/// </summary>
public sealed class CustomWeaponData
{
    public short ItemNetId { get; set; }
    public string Name { get; set; } = string.Empty;
    public byte? Prefix { get; set; }
    public Color? Color { get; set; }
    public short? Stack { get; set; }
    public ushort? Damage { get; set; }
    public float? Knockback { get; set; }
    public ushort? UseAnimation { get; set; }
    public ushort? UseTime { get; set; }
    public short? ShootProjectileId { get; set; }
    public float? ShootSpeed { get; set; }
    public float? Scale { get; set; }
    public short? AmmoIdentifier { get; set; }
    public short? UseAmmoIdentifier { get; set; }
    public bool? NotAmmo { get; set; }
    public short? DropAreaWidth { get; set; }
    public short? DropAreaHeight { get; set; }

    public CustomWeaponData() { }

    public CustomWeaponData(short netId)
    {
        ItemNetId = netId;
        LoadFromDefault(netId);
    }

    public void LoadFromDefault(short netId)
    {
        ItemNetId = netId;
        var probe = new Item();
        probe.SetDefaults(netId);

        Name = probe.Name;
        Prefix = probe.prefix > 0 ? probe.prefix : null;
        Color = null;
        Stack = (short)Math.Max(1, probe.maxStack);
        Damage = (ushort)probe.damage;
        Knockback = probe.knockBack;
        UseAnimation = (ushort)probe.useAnimation;
        UseTime = (ushort)probe.useTime;
        ShootProjectileId = (short)probe.shoot;
        ShootSpeed = probe.shootSpeed;
        Scale = probe.scale;
        AmmoIdentifier = probe.ammo > 0 ? (short)probe.ammo : null;
        UseAmmoIdentifier = probe.useAmmo > 0 ? (short)probe.useAmmo : null;
        NotAmmo = probe.notAmmo;
        DropAreaWidth = (short)probe.width;
        DropAreaHeight = (short)probe.height;
    }

    public static CustomWeaponData FromItem(Item item)
    {
        var data = new CustomWeaponData
        {
            ItemNetId = (short)item.type,
            Name = item.Name,
            Prefix = item.prefix > 0 ? item.prefix : null,
            Color = item.color != default ? item.color : null,
            Stack = (short)Math.Max(1, item.stack),
            Damage = (ushort)item.damage,
            Knockback = item.knockBack,
            UseAnimation = (ushort)item.useAnimation,
            UseTime = (ushort)item.useTime,
            ShootProjectileId = (short)item.shoot,
            ShootSpeed = item.shootSpeed,
            Scale = item.scale,
            AmmoIdentifier = item.ammo > 0 ? (short)item.ammo : null,
            UseAmmoIdentifier = item.useAmmo > 0 ? (short)item.useAmmo : null,
            NotAmmo = item.notAmmo,
            DropAreaWidth = (short)item.width,
            DropAreaHeight = (short)item.height
        };
        return data;
    }

    public void ApplyToItem(Item item)
    {
        if (item.type != ItemNetId)
            item.SetDefaults(ItemNetId);

        if (Prefix.HasValue && Prefix.Value > 0)
            item.Prefix(Prefix.Value);

        if (Color.HasValue) item.color = Color.Value;
        if (Stack.HasValue) item.stack = Math.Max(1, (int)Stack.Value);
        if (Damage.HasValue) item.damage = Damage.Value;
        if (Knockback.HasValue) item.knockBack = Knockback.Value;
        if (UseAnimation.HasValue) item.useAnimation = UseAnimation.Value;
        if (UseTime.HasValue) item.useTime = UseTime.Value;
        if (ShootProjectileId.HasValue) item.shoot = ShootProjectileId.Value;
        if (ShootSpeed.HasValue) item.shootSpeed = ShootSpeed.Value;
        if (Scale.HasValue) item.scale = Scale.Value;
        if (AmmoIdentifier.HasValue) item.ammo = AmmoIdentifier.Value;
        if (UseAmmoIdentifier.HasValue) item.useAmmo = UseAmmoIdentifier.Value;
        if (NotAmmo.HasValue) item.notAmmo = NotAmmo.Value;
        if (DropAreaWidth.HasValue) item.width = DropAreaWidth.Value;
        if (DropAreaHeight.HasValue) item.height = DropAreaHeight.Value;
    }

    public Item CreateItem()
    {
        var item = new Item();
        item.SetDefaults(ItemNetId);
        ApplyToItem(item);
        return item;
    }

    public bool GiveToLocalPlayer(bool fullStack = true)
    {
        if (Main.gameMenu || !Main.LocalPlayer.active)
            return false;

        var item = CreateItem();
        if (fullStack && Stack.HasValue)
            item.stack = Stack.Value;
        else if (fullStack)
            item.stack = item.maxStack;

        var leftover = Main.LocalPlayer.GetItem(item, GetItemSettings.ItemCreatedFromItemUsage);
        bool accepted = leftover.stack < item.stack || leftover.type == 0;
        if (!accepted)
        {
            Notices.Post("背包已满，无法放入物品");
            return false;
        }

        string title = string.IsNullOrWhiteSpace(Name) ? item.Name : Name;
        Notices.Post($"已获取自定义物品: {title}");
        return true;
    }

    /// <summary>
    /// 序列化为 BossProject CustomWeapon 插件支持的 /cwadd 命令格式
    /// </summary>
    public string ToCwCommand(bool includeDefaults = false)
    {
        var sb = new StringBuilder();
        string nameParam = string.IsNullOrWhiteSpace(Name) ? Lang.GetItemNameValue(ItemNetId) : Name;
        if (nameParam.IndexOf(' ') >= 0)
            nameParam = $"\"{nameParam}\"";

        sb.Append($"cwadd -name {nameParam} -id {ItemNetId}");

        if (Prefix.HasValue && Prefix.Value > 0)
            sb.Append($" -pre {Prefix.Value}");

        if (Color.HasValue)
        {
            var c = Color.Value;
            sb.Append($" -color {c.R:X2}{c.G:X2}{c.B:X2}");
        }

        if (Stack.HasValue && (includeDefaults || Stack.Value > 1))
            sb.Append($" -stack {Stack.Value}");

        if (Damage.HasValue && (includeDefaults || Damage.Value > 0))
            sb.Append($" -d {Damage.Value}");

        if (Knockback.HasValue && (includeDefaults || Knockback.Value > 0f))
            sb.Append($" -k {Knockback.Value.ToString("0.##", CultureInfo.InvariantCulture)}");

        if (UseAnimation.HasValue && (includeDefaults || UseAnimation.Value > 0))
            sb.Append($" -anim {UseAnimation.Value}");

        if (UseTime.HasValue && (includeDefaults || UseTime.Value > 0))
            sb.Append($" -time {UseTime.Value}");

        if (ShootProjectileId.HasValue && (includeDefaults || ShootProjectileId.Value > 0))
            sb.Append($" -proj {ShootProjectileId.Value}");

        if (ShootSpeed.HasValue && (includeDefaults || ShootSpeed.Value > 0f))
            sb.Append($" -speed {ShootSpeed.Value.ToString("0.##", CultureInfo.InvariantCulture)}");

        if (Scale.HasValue && (includeDefaults || Math.Abs(Scale.Value - 1f) > 0.01f))
            sb.Append($" -scale {Scale.Value.ToString("0.##", CultureInfo.InvariantCulture)}");

        if (AmmoIdentifier.HasValue && AmmoIdentifier.Value > 0)
            sb.Append($" -ammo {AmmoIdentifier.Value}");

        if (UseAmmoIdentifier.HasValue && UseAmmoIdentifier.Value > 0)
            sb.Append($" -useammo {UseAmmoIdentifier.Value}");

        if (NotAmmo.HasValue && NotAmmo.Value)
            sb.Append($" -notammo true");

        return sb.ToString();
    }

    /// <summary>
    /// 从 /cwadd 命令或参数字符串中反序列化
    /// </summary>
    public static bool TryParseCwCommand(string input, out CustomWeaponData data, out string? error)
    {
        data = new CustomWeaponData();
        error = null;

        if (string.IsNullOrWhiteSpace(input))
        {
            error = "输入为空";
            return false;
        }

        var tokens = Tokenize(input);
        if (tokens.Count == 0)
        {
            error = "没有解析到参数";
            return false;
        }

        int start = 0;
        if (tokens[0].Equals("cwadd", StringComparison.OrdinalIgnoreCase) ||
            tokens[0].Equals("/cwadd", StringComparison.OrdinalIgnoreCase) ||
            tokens[0].Equals("cw", StringComparison.OrdinalIgnoreCase) ||
            tokens[0].Equals("/cw", StringComparison.OrdinalIgnoreCase))
        {
            start = 1;
            if (tokens.Count > 1 && tokens[1].Equals("add", StringComparison.OrdinalIgnoreCase))
                start = 2;
        }

        int remaining = tokens.Count - start;
        if (remaining % 2 != 0)
        {
            error = "参数键值对数量不匹配";
            return false;
        }

        for (int i = start; i < tokens.Count; i += 2)
        {
            string key = tokens[i].ToLowerInvariant();
            string val = tokens[i + 1];

            switch (key)
            {
                case "-name":
                    data.Name = val;
                    break;
                case "-id":
                case "-net":
                case "-netid":
                    if (short.TryParse(val, out var id))
                        data.ItemNetId = id;
                    break;
                case "-pre":
                case "-prefix":
                    if (byte.TryParse(val, out var pre))
                        data.Prefix = pre;
                    break;
                case "-color":
                    if (TryParseColor(val, out var color))
                        data.Color = color;
                    break;
                case "-stack":
                    if (short.TryParse(val, out var stack))
                        data.Stack = stack;
                    break;
                case "-d":
                case "-damage":
                    if (ushort.TryParse(val, out var damage))
                        data.Damage = damage;
                    break;
                case "-k":
                case "-knock":
                case "-knockback":
                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var knock))
                        data.Knockback = knock;
                    break;
                case "-anim":
                case "-animation":
                    if (ushort.TryParse(val, out var anim))
                        data.UseAnimation = anim;
                    break;
                case "-time":
                case "-usetime":
                    if (ushort.TryParse(val, out var time))
                        data.UseTime = time;
                    break;
                case "-proj":
                case "-shoot":
                case "-shootproj":
                    if (short.TryParse(val, out var proj))
                        data.ShootProjectileId = proj;
                    break;
                case "-speed":
                case "-shootspeed":
                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var speed))
                        data.ShootSpeed = speed;
                    break;
                case "-scale":
                case "-size":
                    if (float.TryParse(val, NumberStyles.Float, CultureInfo.InvariantCulture, out var scale))
                        data.Scale = scale;
                    break;
                case "-ammo":
                case "-ammoid":
                    if (short.TryParse(val, out var ammo))
                        data.AmmoIdentifier = ammo;
                    break;
                case "-useammo":
                case "-useammoid":
                    if (short.TryParse(val, out var useAmmo))
                        data.UseAmmoIdentifier = useAmmo;
                    break;
                case "-notammo":
                case "-nammo":
                    if (bool.TryParse(val, out var notAmmo))
                        data.NotAmmo = notAmmo;
                    break;
            }
        }

        if (data.ItemNetId <= 0)
        {
            error = "缺少基础物品 ID (-id)";
            return false;
        }

        return true;
    }

    private static bool TryParseColor(string hex, out Color color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(hex))
            return false;
        hex = hex.TrimStart('#');
        if (hex.Length != 6)
            return false;

        if (byte.TryParse(hex.Substring(0, 2), NumberStyles.HexNumber, null, out var r) &&
            byte.TryParse(hex.Substring(2, 2), NumberStyles.HexNumber, null, out var g) &&
            byte.TryParse(hex.Substring(4, 2), NumberStyles.HexNumber, null, out var b))
        {
            color = new Color(r, g, b);
            return true;
        }
        return false;
    }

    private static System.Collections.Generic.List<string> Tokenize(string text)
    {
        var list = new System.Collections.Generic.List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
            }
            else if (char.IsWhiteSpace(c) && !inQuotes)
            {
                if (sb.Length > 0)
                {
                    list.Add(sb.ToString());
                    sb.Clear();
                }
            }
            else
            {
                sb.Append(c);
            }
        }

        if (sb.Length > 0)
            list.Add(sb.ToString());

        return list;
    }
}
