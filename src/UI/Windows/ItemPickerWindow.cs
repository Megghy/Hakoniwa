using System;
using System.Collections.Generic;
using System.Numerics;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Hakoniwa.UI.Windows;

public sealed class ItemPickerWindow
{
    public string Title => "物品选择器 (Item Catalog)###HakoniwaItemPickerWindow";
    public bool IsOpen { get; set; }

    private const float Cell = 42f;
    private const uint HeartOn = 0xFF5B40F4; // Terraria Red-Pink Heart
    private const uint HeartIdle = 0x808A8794;

    private string _search = string.Empty;
    private int _category = 1; // 默认全部
    private int _sub;
    private readonly List<int> _hits = [];
    private string _last = "\0";

    // 代表性分类图标 (ItemID)
    private static readonly int[] CategoryIcons =
    [
        0,                        // 0: 收藏 (使用 Heart 图标)
        ItemID.Chest,             // 1: 全部 (宝箱)
        ItemID.IronPickaxe,       // 2: 工具 (铁镐)
        ItemID.HallowedPlateMail, // 3: 盔甲 (神圣胸甲)
        ItemID.FamiliarShirt,     // 4: 时装 (假面衬衫)
        ItemID.RedDye,            // 5: 染料 (红染料)
        ItemID.GoldBrick,         // 6: 物块 (金砖)
    ];

    // 二级分类代表性图标
    private static readonly int[][] SubCategoryIcons =
    [
        [], // 收藏
        [], // 全部
        [ItemID.IronPickaxe, ItemID.GoldPickaxe, ItemID.GoldAxe, ItemID.TheBreaker, ItemID.GoldenFishingRod], // 工具 (全部/镐/斧/锤/钓竿)
        [ItemID.HallowedPlateMail, ItemID.IronHelmet, ItemID.IronChainmail, ItemID.IronGreaves],              // 盔甲 (全部/头/胸/腿)
        [ItemID.FamiliarShirt, ItemID.FamiliarWig, ItemID.FamiliarShirt, ItemID.FamiliarPants, ItemID.HermesBoots], // 时装 (全部/头/衣/裤/饰)
        [ItemID.RedDye, ItemID.RedDye, ItemID.DepthHairDye],                                                 // 染料 (全部/染料/发染)
        [ItemID.GoldBrick, ItemID.DirtBlock, ItemID.WoodWall, ItemID.WoodenChair, ItemID.WoodPlatform],      // 物块 (全部/物块/墙/家具/平台)
    ];

    public void Open() => IsOpen = true;
    public void Close() => IsOpen = false;
    public void Toggle()
    {
        IsOpen = !IsOpen;
        if (IsOpen)
            SoundEngine.PlaySound(SoundID.MenuOpen);
        else
            SoundEngine.PlaySound(SoundID.MenuClose);
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(680f, 540f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(480f, 360f), new Vector2(1920f, 1080f));
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            DrawContent(embedded: false);
        }
        IsOpen = open;
        ImGui.End();
    }

    public void DrawContent(bool embedded)
    {
        if (Main.gameMenu || !Main.LocalPlayer.active)
        {
            Ui.Heading(Icons.Box, "箱庭物品库");
            ImGui.TextUnformatted("请先进入世界以使用物品选择器。");
            return;
        }

        // 顶部搜索与控制栏
        DrawTopBar(embedded);

        ImGui.Spacing();

        // 一级分类 Tab (带游戏代表性物品图标)
        DrawCategoryTabs();

        // 二级分类 Tab (若存在)
        DrawSubCategoryTabs();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 刷新搜索结果
        Refresh();

        // 物品网格
        DrawGrid();
    }

    private void DrawTopBar(bool embedded)
    {
        float avail = ImGui.GetContentRegionAvail().X;
        float rightControlsWidth = embedded ? 140f : 90f;
        float searchWidth = Math.Max(180f, avail - rightControlsWidth - 10f);

        ImGui.SetNextItemWidth(searchWidth);
        ImGui.InputTextWithHint("##itemSearch", "搜索物品名称 / 拼音...", ref _search, (UIntPtr)128);

        if (_search.Length > 0)
        {
            ImGui.SameLine(0f, 4f);
            if (ImGui.Button(Icons.Close, new Vector2(22f, 22f)))
                _search = string.Empty;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("清空搜索");
        }

        ImGui.SameLine();
        ImGui.TextColored(Ui.Accent, $"{_hits.Count} 件");

        if (embedded)
        {
            ImGui.SameLine(avail - 96f);
            if (ImGui.Button("↗ 独立窗口", new Vector2(96f, 22f)))
            {
                IsOpen = true;
                SoundEngine.PlaySound(SoundID.MenuOpen);
            }
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("将物品库弹出为独立窗口，可在游戏或背包中快捷移动/查看");
        }
    }

    private void DrawCategoryTabs()
    {
        int count = ItemCatalog.Names.Length;
        float avail = ImGui.GetContentRegionAvail().X;
        float gap = 4f;
        float width = (avail - gap * (count - 1)) / count;
        float height = 32f;

        var dl = ImGui.GetWindowDrawList();

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, gap);

            bool selected = _category == i;
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(width, height);

            if (ImGui.InvisibleButton($"##catTab{i}", size))
            {
                if (_category != i)
                {
                    _category = i;
                    _sub = 0;
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }

            bool hover = ImGui.IsItemHovered();
            var min = pos;
            var max = pos + size;

            // 像素页签底板
            uint bg = selected ? 0xF81C263C : (hover ? 0xF8141C2A : 0xF00D111A);
            uint border = selected ? Ui.GoldBorder : (hover ? Ui.ChipOn : Ui.ChipLine);
            dl.AddRectFilled(min, max, bg);
            dl.AddRect(min, max, border, 0f, ImDrawFlags.None, 1f);

            if (selected)
            {
                // 顶部高亮金条
                dl.AddRectFilled(min, new Vector2(max.X, min.Y + 2f), Ui.GoldBorder);
            }

            // 图标渲染
            float textOffset = 24f;
            if (i == 0) // 收藏
            {
                Icons.DrawDirect(dl, min + new Vector2(14f, height * 0.5f), Icons.Heart, 255, selected ? HeartOn : 0xFF8A8794, 16f);
            }
            else
            {
                int iconId = CategoryIcons[i];
                if (iconId > 0)
                {
                    UiIcons.DrawItemDirect(dl, min + new Vector2(14f, height * 0.5f), iconId, 22f);
                }
            }

            // 文本渲染
            string text = ItemCatalog.Names[i];
            var textSize = ImGui.CalcTextSize(text);
            var textPos = min + new Vector2(textOffset + (width - textOffset - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f);
            dl.AddText(textPos, selected ? 0xFFFFFFFF : 0xFFB4C2D6, text);
        }
    }

    private void DrawSubCategoryTabs()
    {
        var subs = ItemCatalog.Subs((ItemCategory)_category);
        if (subs.Length <= 0)
            return;

        ImGui.Spacing();
        int count = subs.Length;
        float avail = ImGui.GetContentRegionAvail().X;
        float gap = 4f;
        float width = Math.Max(58f, (avail - gap * (count - 1)) / count);
        float height = 24f;

        var dl = ImGui.GetWindowDrawList();
        var iconList = _category < SubCategoryIcons.Length ? SubCategoryIcons[_category] : [];

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, gap);

            bool selected = _sub == i;
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(width, height);

            if (ImGui.InvisibleButton($"##subTab{i}", size))
            {
                _sub = i;
                SoundEngine.PlaySound(SoundID.MenuTick);
            }

            bool hover = ImGui.IsItemHovered();
            var min = pos;
            var max = pos + size;

            uint bg = selected ? 0xF8182236 : (hover ? 0xF8121824 : 0xF00A0D14);
            uint border = selected ? Ui.ChipOn : (hover ? 0xFF4B6082 : Ui.ChipLine);
            dl.AddRectFilled(min, max, bg);
            dl.AddRect(min, max, border, 0f, ImDrawFlags.None, 1f);

            // 小图标 (如果有)
            float textOffset = 8f;
            if (i < iconList.Length && iconList[i] > 0)
            {
                UiIcons.DrawItemDirect(dl, min + new Vector2(10f, height * 0.5f), iconList[i], 16f);
                textOffset = 20f;
            }

            string text = subs[i].Name;
            var textSize = ImGui.CalcTextSize(text);
            var textPos = min + new Vector2(textOffset + (width - textOffset - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f);
            dl.AddText(textPos, selected ? 0xFF38BDF8 : 0xFF94A3B8, text);
        }
    }

    private void Refresh()
    {
        string key = $"{_category}\n{_sub}\n{_search}\n{CheatState.FavRev}";
        if (key == _last)
            return;
        _last = key;
        ItemCatalog.Search(_search, (ItemCategory)_category, _sub, CheatState.Favorites, _hits);
    }

    private void DrawGrid()
    {
        ImGui.BeginChild("item-grid-viewport", Vector2.Zero, ImGuiChildFlags.None);
        float avail = ImGui.GetContentRegionAvail().X;
        int cols = Math.Max(1, (int)(avail / Cell));
        int rows = (_hits.Count + cols - 1) / cols;
        float height = ImGui.GetWindowHeight();
        int first = Math.Max(0, (int)(ImGui.GetScrollY() / Cell));
        int last = Math.Min(rows, first + (int)(height / Cell) + 2);
        if (first > 0)
            ImGui.Dummy(new Vector2(1f, first * Cell));

        var dl = ImGui.GetWindowDrawList();
        for (int row = first; row < last; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int i = row * cols + col;
                if (i >= _hits.Count)
                    break;
                if (col > 0)
                    ImGui.SameLine(0f, 0f);
                DrawCell(dl, _hits[i]);
            }
        }

        if (last < rows)
            ImGui.Dummy(new Vector2(1f, (rows - last) * Cell));
        ImGui.EndChild();
    }

    private static void DrawCell(ImDrawListPtr dl, int id)
    {
        ImGui.PushID(id);
        ImGui.InvisibleButton("##itemCell", new Vector2(Cell, Cell));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        bool hover = ImGui.IsItemHovered();
        bool fav = CheatState.Favorites.Contains(id);

        var cellMin = min + new Vector2(1f, 1f);
        var cellMax = max - new Vector2(1f, 1f);

        // 绘制像素凹槽
        Ui.DrawPixelSlot(dl, cellMin, cellMax, hover, fav);

        // 绘制物品纹理
        UiIcons.DrawItemDirect(dl, (min + max) * 0.5f, id, 30f);

        // 收藏爱心
        var heartCenter = new Vector2(cellMax.X - 7f, cellMin.Y + 7f);
        bool overHeart = hover && Vector2.Distance(ImGui.GetIO().MousePos, heartCenter) < 8f;
        if (fav || hover)
        {
            Icons.DrawDirect(dl, heartCenter, Icons.Heart, (byte)(fav || overHeart ? 255 : 150), fav ? HeartOn : HeartIdle, 13f);
        }

        if (hover)
        {
            DrawItemTooltip(id);
        }

        var io = ImGui.GetIO();
        bool shift = io.KeyShift;
        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle) || (ImGui.IsItemClicked(ImGuiMouseButton.Left) && overHeart))
        {
            CheatState.ToggleFavorite(id);
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            if (shift)
            {
                ImGui.SetClipboardText(new CustomWeaponData((short)id).ToCwCommand());
                Notices.Post("已复制 /cwadd 命令到剪贴板");
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else
                HakoniwaUi.ItemEditor.OpenWith((short)id);
        }
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
        {
            if (shift)
                HakoniwaUi.ItemEditor.OpenWith((short)id);
            else
            {
                ItemCatalog.GiveToCursor(id);
                SoundEngine.PlaySound(SoundID.Grab);
            }
        }

        ImGui.PopID();
    }

    private static void DrawItemTooltip(int id)
    {
        var item = new Item();
        item.SetDefaults(id);

        ImGui.BeginTooltip();
        var dl = ImGui.GetWindowDrawList();

        // 稀有度颜色
        var rareCol = GetRarityColor(item.rare);
        uint titleColor = ImGui.ColorConvertFloat4ToU32(rareCol);

        // 物品名称
        ImGui.TextColored(rareCol, item.Name);
        ImGui.SameLine();
        ImGui.TextDisabled($"#{id}");

        ImGui.Separator();

        // 属性标签
        if (item.damage > 0)
        {
            string dmgType = item.melee ? "近战" : (item.ranged ? "远程" : (item.magic ? "魔法" : (item.summon ? "召唤" : "")));
            string dmgLabel = string.IsNullOrEmpty(dmgType) ? $"{item.damage} 点基础伤害" : $"{item.damage} 点{dmgType}伤害";
            ImGui.TextUnformatted(dmgLabel);
            if (item.crit > 0)
            {
                ImGui.SameLine();
                ImGui.TextColored(Ui.Gold, $"+{item.crit}% 暴击率");
            }
        }

        if (item.defense > 0)
        {
            ImGui.TextUnformatted($"{item.defense} 点防御力");
        }

        if (item.knockBack > 0f)
        {
            ImGui.TextDisabled($"击退力: {item.knockBack:F1}");
        }

        if (item.shootSpeed > 0f)
        {
            ImGui.TextDisabled($"射速: {item.shootSpeed:F1}");
        }

        if (item.createTile >= 0)
        {
            ImGui.TextColored(Ui.Accent, "可放置物块");
        }
        else if (item.createWall > 0)
        {
            ImGui.TextColored(Ui.Accent, "可放置墙壁");
        }

        if (item.maxStack > 1)
        {
            ImGui.TextDisabled($"最大堆叠: {item.maxStack}");
        }

        ImGui.Separator();
        ImGui.TextColored(new Vector4(0.55f, 0.65f, 0.80f, 1f), "[左键] 拿取整组  |  [右键] 操作菜单  |  [中键] 收藏");
        ImGui.TextColored(Ui.Accent, "[Shift + 左键] 高级属性编辑  |  [Shift + 右键] 复制 /cw 命令");

        ImGui.EndTooltip();
    }

    private static Vector4 GetRarityColor(int rare) => rare switch
    {
        -1 => new Vector4(0.55f, 0.55f, 0.55f, 1f), // 灰色
        0 => new Vector4(0.95f, 0.95f, 0.95f, 1f),  // 白色
        1 => new Vector4(0.58f, 0.68f, 1.00f, 1f),  // 蓝色
        2 => new Vector4(0.58f, 1.00f, 0.58f, 1f),  // 绿色
        3 => new Vector4(1.00f, 0.78f, 0.45f, 1f),  // 橙色
        4 => new Vector4(1.00f, 0.55f, 0.55f, 1f),  // 浅红
        5 => new Vector4(1.00f, 0.55f, 1.00f, 1f),  // 粉色
        6 => new Vector4(0.82f, 0.65f, 1.00f, 1f),  // 浅紫
        7 => new Vector4(0.85f, 1.00f, 0.35f, 1f),  // 黄绿
        8 => new Vector4(1.00f, 1.00f, 0.40f, 1f),  // 黄色
        9 => new Vector4(0.35f, 0.90f, 1.00f, 1f),  // 青色
        10 => new Vector4(0.98f, 0.35f, 0.35f, 1f), // 红色
        11 => new Vector4(0.75f, 0.35f, 0.98f, 1f), // 紫色
        -11 => new Vector4(1.00f, 0.70f, 0.00f, 1f),// 琥珀
        -12 => new Vector4(1.00f, 0.45f, 0.00f, 1f),// 大师 (火橙)
        _ => new Vector4(0.95f, 0.95f, 0.95f, 1f),
    };
}
