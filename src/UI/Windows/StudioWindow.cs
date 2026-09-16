using System;
using System.Collections.Generic;
using System.Numerics;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.IO;
using Hakoniwa.Engine.Tools;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Hakoniwa.UI.Windows;

public sealed class StudioWindow : IWindow
{
    public string Title => "箱庭工坊 (Hakoniwa Studio)###HakoniwaMainStudio";
    public string Label => "工坊";
    public bool IsOpen { get; set; } = true;

    private int _currentTab;
    private static readonly string[] Tabs = ["建造选区", "世界规则", "物品库", "角色装备", "蓝图方案"];
    private static readonly int[] TabIcons = [ItemID.ArchitectGizmoPack, ItemID.Sundial, ItemID.Chest, ItemID.Dresser, ItemID.LaserRuler];

    // 建造选项
    private static readonly string[] Tools = ["笔刷 (Brush)", "油漆桶 (Fill)", "橡皮擦 (Eraser)", "矩形选区 (Select)"];
    private static readonly string[] Shapes = ["圆形 (Circle)", "方形 (Square)", "菱形 (Diamond)"];

    // 物品库搜索状态
    private string _itemSearch = string.Empty;
    private int _itemCategory;
    private readonly List<int> _itemHits = [];
    private string _lastItemQuery = "\0";
    private static readonly string[] ItemCategories = ["全部", "工具/武器", "防具", "时装", "染料", "全部物块"];

    // 蓝图库状态
    public List<Schematic> SchemLibrary { get; } = [];
    public string SchemSearch = string.Empty;
    public int SelectedSchemIndex = -1;
    public string SchemPathInput = string.Empty;

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(640f, 500f), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            DrawTabBar();
            ImGui.Separator();
            ImGui.Spacing();

            switch (_currentTab)
            {
                case 0:
                    DrawBuildTab();
                    break;
                case 1:
                    DrawWorldTab();
                    break;
                case 2:
                    DrawItemsTab();
                    break;
                case 3:
                    DrawCharacterTab();
                    break;
                case 4:
                    DrawSchematicsTab();
                    break;
            }
        }

        IsOpen = open;
        ImGui.End();
    }

    private void DrawTabBar()
    {
        float avail = ImGui.GetContentRegionAvail().X;
        float spacing = ImGui.GetStyle().ItemSpacing.X;
        float width = (avail - spacing * (Tabs.Length - 1)) / Tabs.Length;

        for (int i = 0; i < Tabs.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine();

            bool active = _currentTab == i;
            if (active)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.42f, 0.28f, 0.68f, 1f));

            if (ImGui.Button($"    {Tabs[i]}##tab_{i}", new Vector2(width, 32f)))
                _currentTab = i;

            var min = ImGui.GetItemRectMin();
            UiIcons.DrawItemDirect(ImGui.GetWindowDrawList(), min + new Vector2(16f, 16f), TabIcons[i], 18f);

            if (active)
                ImGui.PopStyleColor();
        }
    }

    private static void DrawBuildTab()
    {
        ImGui.BeginChild("build-scroll", new Vector2(0, 0), ImGuiChildFlags.None);

        // 选区信息与操作组
        UiIcons.DrawItem(ItemID.LaserRuler, 20f);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "选区与几何变换 (Selection & Transform)");
        var sel = EditorSession.Selection;
        string selInfo = sel.Active
            ? $"当前选区: 起点 ({sel.MinX}, {sel.MinY})  尺寸: {sel.Width} x {sel.Height}"
            : "当前无活动选区 (按住左键拖拽框选)";
        ImGui.TextUnformatted(selInfo);

        if (ImGui.Button("复制 (Copy)", new Vector2(90, 26))) EditorSession.Copy();
        ImGui.SameLine();
        if (ImGui.Button("剪切 (Cut)", new Vector2(90, 26))) EditorSession.Cut();
        ImGui.SameLine();
        if (ImGui.Button("粘贴 (Paste)", new Vector2(90, 26))) EditorSession.Paste();
        ImGui.SameLine();
        if (ImGui.Button("清除选区", new Vector2(90, 26))) sel.Clear();

        if (ImGui.Button("水平翻转", new Vector2(90, 26))) EditorSession.FlipHorizontal();
        ImGui.SameLine();
        if (ImGui.Button("垂直翻转", new Vector2(90, 26))) EditorSession.FlipVertical();
        ImGui.SameLine();
        if (ImGui.Button("顺时针90°", new Vector2(90, 26))) EditorSession.Rotate90();
        ImGui.SameLine();
        if (ImGui.Button("撤销 (Undo)", new Vector2(90, 26))) EditorSession.Undo();
        ImGui.SameLine();
        if (ImGui.Button("重做 (Redo)", new Vector2(90, 26))) EditorSession.Redo();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 笔刷配置
        UiIcons.DrawItem(ItemID.Paintbrush, 20f);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "笔刷设置 (Brush & Tools)");
        ImGui.Combo("当前工具", ref EditorSession.SelectedTool, Tools, Tools.Length);

        int shape = (int)EditorSession.BrushShape;
        if (ImGui.Combo("笔刷形状", ref shape, Shapes, Shapes.Length))
            EditorSession.BrushShape = (BrushShape)shape;

        ImGui.SliderInt("笔刷半径", ref EditorSession.BrushRadius, 0, 50);

        ImGui.EndChild();
    }

    private static void DrawWorldTab()
    {
        ImGui.BeginChild("world-scroll", new Vector2(0, 0), ImGuiChildFlags.None);

        UiIcons.DrawItem(ItemID.Sundial, 20f);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "创造模式规则 (Rules)");
        ImGui.Checkbox("上帝模式 (God Mode)", ref CheatState.GodMode);
        ImGui.SameLine(220f);
        ImGui.Checkbox("全图照明 (Full Bright)", ref CheatState.FullBright);

        ImGui.Checkbox("无限放置范围 (Infinite Reach)", ref CheatState.InfiniteReach);
        ImGui.SameLine(220f);
        ImGui.Checkbox("无限物块 (Infinite Items)", ref CheatState.InfiniteItems);

        ImGui.Checkbox("自由悬空放置 (Free Placement)", ref CheatState.FreePlacement);
        ImGui.SameLine(220f);
        ImGui.Checkbox("快捷传送 (Click Teleport)", ref CheatState.ClickTeleport);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "世界时间与环境 (Time & Environment)");
        float time = GetTimeFraction();
        if (ImGui.SliderFloat("时间进度 (0:00 - 24:00)", ref time, 0f, 1f, GetTimeString(time)))
            SetTimeFraction(time);

        ImGui.Checkbox("锁定时间 (Freeze Time)", ref CheatState.FreezeTime);
        ImGui.SameLine();
        if (ImGui.Button("清晨 04:30")) SetTimeFraction(0f);
        ImGui.SameLine();
        if (ImGui.Button("正午 12:00")) SetTimeFraction(27000f / 86400f);
        ImGui.SameLine();
        if (ImGui.Button("黄昏 19:30")) SetTimeFraction(54000f / 86400f);
        ImGui.SameLine();
        if (ImGui.Button("午夜 00:00")) SetTimeFraction(70200f / 86400f);

        ImGui.Spacing();
        ImGui.TextDisabled("提示: 开启快捷传送后，大地图右键 或 世界中键 可直接瞬间传送。");

        ImGui.EndChild();
    }

    private void DrawItemsTab()
    {
        ImGui.InputText("搜索物品", ref _itemSearch, 128);
        ImGui.SameLine();
        ImGui.Combo("分类", ref _itemCategory, ItemCategories, ItemCategories.Length);

        RefreshItems();
        ImGui.TextDisabled($"共匹配 {_itemHits.Count} 个物品 (点击直接提取到鼠标)");

        ImGui.BeginChild("items-list-child", new Vector2(0, 0), ImGuiChildFlags.Border);
        const float rowH = 22f;
        int first = Math.Max(0, (int)(ImGui.GetScrollY() / rowH));
        int last = Math.Min(_itemHits.Count, first + (int)(ImGui.GetWindowHeight() / rowH) + 2);
        if (first > 0)
            ImGui.Dummy(new Vector2(1f, first * rowH));
        for (int i = first; i < last; i++)
        {
            int id = _itemHits[i];
            string name = Lang.GetItemNameValue(id);

            ImGui.PushID(id);
            UiIcons.DrawItem(id, 20f);
            ImGui.SameLine();

            if (ImGui.Selectable($"{name} (ID: {id})", false, ImGuiSelectableFlags.None, new Vector2(0, rowH)))
                GiveItem(id);

            ImGui.PopID();
        }
        if (last < _itemHits.Count)
            ImGui.Dummy(new Vector2(1f, (_itemHits.Count - last) * rowH));

        ImGui.EndChild();
    }

    private void RefreshItems()
    {
        string key = _itemCategory + "\n" + _itemSearch;
        if (key == _lastItemQuery)
            return;
        _lastItemQuery = key;
        _itemHits.Clear();
        if (Main.gameMenu)
            return;

        var category = (ItemCategory)_itemCategory;
        var probe = new Item();
        for (int id = 1; id < ItemID.Count; id++)
        {
            string name = Lang.GetItemNameValue(id);
            if (name.Length == 0)
                continue;
            if (_itemSearch.Length > 0 && name.IndexOf(_itemSearch, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            probe.SetDefaults(id);
            if (!ItemCatalog.Matches(probe, category))
                continue;
            _itemHits.Add(id);
        }
    }

    private static void GiveItem(int id)
    {
        var item = new Item();
        item.SetDefaults(id);
        item.stack = Math.Max(1, item.maxStack);
        Main.mouseItem = item;
    }

    private static void DrawCharacterTab()
    {
        ImGui.BeginChild("char-scroll", new Vector2(0, 0), ImGuiChildFlags.None);

        if (Main.gameMenu || !Main.LocalPlayer.active)
        {
            ImGui.TextUnformatted("请先进入世界以编辑角色属性。");
            ImGui.EndChild();
            return;
        }

        var player = Main.LocalPlayer;
        UiIcons.DrawItem(ItemID.Dresser, 20f);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "玩家属性 (Player Attributes)");
        int life = player.statLifeMax;
        if (ImGui.SliderInt("生命上限", ref life, 1, 5000))
        {
            player.statLifeMax = life;
            player.statLife = life;
        }

        int mana = player.statManaMax;
        if (ImGui.SliderInt("魔力上限", ref mana, 0, 400))
        {
            player.statManaMax = mana;
            player.statMana = mana;
        }

        int hair = player.hair;
        if (ImGui.SliderInt("发型", ref hair, 0, Main.maxHairStyles - 1))
            player.hair = hair;

        bool male = player.Male;
        if (ImGui.Checkbox("男性角色", ref male))
            player.Male = male;

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "当前手持装备属性 (Held Item Editor)");
        var held = !Main.mouseItem.IsAir ? Main.mouseItem : player.HeldItem;
        if (held.IsAir)
        {
            ImGui.TextDisabled("当前未手持或抓取任何物品。");
        }
        else
        {
            UiIcons.DrawItem(held.type, 20f);
            ImGui.SameLine();
            ImGui.TextUnformatted($"物品: {held.Name} (ID: {held.type})");
            int damage = held.damage;
            if (ImGui.InputInt("基础伤害", ref damage)) held.damage = damage;

            int useTime = held.useTime;
            if (ImGui.InputInt("使用间隔 (Use Time)", ref useTime)) held.useTime = useTime;

            int useAnim = held.useAnimation;
            if (ImGui.InputInt("动画帧长 (Use Animation)", ref useAnim)) held.useAnimation = useAnim;

            float shoot = held.shootSpeed;
            if (ImGui.InputFloat("弹幕射速", ref shoot)) held.shootSpeed = shoot;

            float knock = held.knockBack;
            if (ImGui.InputFloat("击退力", ref knock)) held.knockBack = knock;

            int crit = held.crit;
            if (ImGui.InputInt("暴击加成", ref crit)) held.crit = crit;

            ImGui.Checkbox("自动连发 (Auto Reuse)", ref held.autoReuse);
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "外观配色 (Colors)");
        ColorEdit("发色", ref player.hairColor);
        ColorEdit("肤色", ref player.skinColor);
        ColorEdit("眼睛", ref player.eyeColor);
        ColorEdit("上衣", ref player.shirtColor);
        ColorEdit("内衬", ref player.underShirtColor);
        ColorEdit("长裤", ref player.pantsColor);
        ColorEdit("鞋子", ref player.shoeColor);

        ImGui.EndChild();
    }

    private static void ColorEdit(string label, ref Microsoft.Xna.Framework.Color color)
    {
        var rgb = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        if (ImGui.ColorEdit3(label, ref rgb))
            color = new Microsoft.Xna.Framework.Color(rgb.X, rgb.Y, rgb.Z);
    }

    private void DrawSchematicsTab()
    {
        ImGui.BeginChild("schem-scroll", new Vector2(0, 0), ImGuiChildFlags.None);

        UiIcons.DrawItem(ItemID.LaserRuler, 20f);
        ImGui.SameLine();
        ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "蓝图导入与导出 (Schematics IO)");
        ImGui.InputText("蓝图文件路径", ref SchemPathInput, 512);

        if (ImGui.Button("导入文件 (Import)", new Vector2(120, 26)) && SchemPathInput.Length > 0)
        {
            try
            {
                SchemLibrary.Add(SchematicSerializer.Load(SchemPathInput));
                SelectedSchemIndex = SchemLibrary.Count - 1;
            }
            catch (Exception ex)
            {
                Main.NewText($"[Hakoniwa] 蓝图导入失败: {ex.Message}", 255, 100, 100);
            }
        }

        ImGui.SameLine();
        if (ImGui.Button("导出当前蓝图 (Export)", new Vector2(140, 26)) && SchemPathInput.Length > 0 && SelectedSchemIndex >= 0)
        {
            try
            {
                SchematicSerializer.Save(SchemLibrary[SelectedSchemIndex], SchemPathInput);
                Main.NewText("[Hakoniwa] 蓝图导出成功！", 100, 255, 100);
            }
            catch (Exception ex)
            {
                Main.NewText($"[Hakoniwa] 蓝图导出失败: {ex.Message}", 255, 100, 100);
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        ImGui.InputText("过滤蓝图", ref SchemSearch, 128);
        ImGui.BeginChild("schem-list-child", new Vector2(0, 0), ImGuiChildFlags.Border);
        for (int i = 0; i < SchemLibrary.Count; i++)
        {
            var schematic = SchemLibrary[i];
            if (SchemSearch.Length > 0 && schematic.Name.IndexOf(SchemSearch, StringComparison.OrdinalIgnoreCase) < 0)
                continue;

            if (ImGui.Selectable($"{schematic.Name} ({schematic.Width} x {schematic.Height})", SelectedSchemIndex == i))
                SelectedSchemIndex = i;
        }

        ImGui.EndChild();

        ImGui.EndChild();
    }

    private static float GetTimeFraction()
    {
        double cycle = Main.dayTime ? Main.time : 54000.0 + Main.time;
        return (float)(cycle / 86400.0);
    }

    private static void SetTimeFraction(float fraction)
    {
        fraction = Math.Max(0f, Math.Min(1f, fraction));
        double cycle = fraction * 86400.0;
        if (cycle < 54000.0)
            Main.SkipToTime((int)cycle, true);
        else
            Main.SkipToTime((int)(cycle - 54000.0), false);
        CheatState.FrozenTime = Main.time;
    }

    private static string GetTimeString(float fraction)
    {
        double totalSeconds = fraction * 86400.0;
        double clockSeconds = (totalSeconds + 16200.0) % 86400.0;
        int hours = (int)(clockSeconds / 3600.0);
        int minutes = (int)((clockSeconds % 3600.0) / 60.0);
        return $"{hours:D2}:{minutes:D2}";
    }
}
