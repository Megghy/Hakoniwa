using System;
using System.Collections.Generic;
using System.Numerics;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.IO;
using Hakoniwa.Engine.Tools;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Hakoniwa.UI.Windows;

public sealed class StudioWindow
{
    public string Title => "箱庭工坊 (Hakoniwa Studio)###HakoniwaMainStudio";
    public bool IsOpen { get; set; }

    private int _currentTab;
    private static readonly string[] Tabs = ["建造选区", "世界规则", "物品库", "角色装备", "蓝图方案", "设置"];
    private static readonly string[] TabIcons = [Icons.Crop, Icons.Earth, Icons.Box, Icons.Human, Icons.Script, Icons.Settings];

    // 蓝图库状态
    public List<Schematic> SchemLibrary { get; } = [];
    public string SchemSearch = string.Empty;
    public int SelectedSchemIndex = -1;
    public string SchemPathInput = string.Empty;

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(720f, 560f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(560f, 420f), new Vector2(1920f, 1080f));
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            DrawTabBar();
            ImGui.Spacing();
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
                    HakoniwaUi.ItemPicker.DrawContent(embedded: true);
                    break;
                case 3:
                    DrawCharacterTab();
                    break;
                case 4:
                    DrawSchematicsTab();
                    break;
                case 5:
                    DrawSettingsTab();
                    break;
            }
        }

        IsOpen = open;
        ImGui.End();
    }

    private void DrawTabBar()
    {
        int count = Tabs.Length;
        float avail = ImGui.GetContentRegionAvail().X;
        float gap = 4f;
        float width = (avail - gap * (count - 1)) / count;
        float height = 34f;

        var dl = ImGui.GetWindowDrawList();

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                ImGui.SameLine(0f, gap);

            bool selected = _currentTab == i;
            var pos = ImGui.GetCursorScreenPos();
            var size = new Vector2(width, height);

            if (ImGui.InvisibleButton($"##studioTab{i}", size))
            {
                if (_currentTab != i)
                {
                    _currentTab = i;
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
                dl.AddRectFilled(min, new Vector2(max.X, min.Y + 2f), Ui.GoldBorder);
            }

            Icons.DrawDirect(dl, min + new Vector2(14f, height * 0.5f), TabIcons[i], 255, selected ? Ui.ChipOn : 0xFF8A8794, 18f);

            string text = Tabs[i];
            var textSize = ImGui.CalcTextSize(text);
            var textPos = min + new Vector2(24f + (width - 24f - textSize.X) * 0.5f, (height - textSize.Y) * 0.5f);
            dl.AddText(textPos, selected ? 0xFFFFFFFF : 0xFFB4C2D6, text);
        }
    }

    private static void DrawBuildTab()
    {
        Ui.BeginScroll("build-scroll");

        // 1. 选区状态卡片
        Ui.Heading(Icons.Crop, "选区状态与框选快捷键");
        var sel = EditorSession.Selection;

        ImGui.BeginChild("sel-status-card", new Vector2(0, 54f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        var cMin = ImGui.GetWindowPos();
        var cMax = cMin + ImGui.GetWindowSize();
        Ui.DrawPixelPanel(dl, cMin, cMax);

        if (sel.Active)
        {
            ImGui.TextColored(Ui.Gold, $"活动选区: 起点 ({sel.MinX}, {sel.MinY})  尺寸: {sel.Width} x {sel.Height} (共 {sel.Width * sel.Height} 格)");
        }
        else
        {
            ImGui.TextDisabled($"当前无选区 (按住 {CheatState.SelectModifier} 键并在世界中左键拖拽可快速创建)");
        }

        string bind = CheatState.WaitingSelectKey ? "请按下新按键..." : CheatState.SelectModifier.ToString();
        if (ImGui.Button($"框选修饰键: {bind}##selKey", new Vector2(160f, 22f)))
            CheatState.WaitingSelectKey = true;

        ImGui.EndChild();

        ImGui.Spacing();

        // 2. 动作工具网格 Bento Grid
        Ui.Heading(Icons.SectionCopy, "几何与编辑动作 (Actions)");
        float availW = ImGui.GetContentRegionAvail().X;
        float btnW = (availW - 12f) / 4f;
        float btnH = 28f;

        if (ImGui.Button("复制 (Ctrl+C)", new Vector2(btnW, btnH))) EditorSession.Copy();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("剪切 (Ctrl+X)", new Vector2(btnW, btnH))) EditorSession.Cut();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("粘贴 (Ctrl+V)", new Vector2(btnW, btnH))) EditorSession.Paste();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("删除选区 (Del)", new Vector2(btnW, btnH))) EditorSession.Delete();

        if (ImGui.Button("水平翻转", new Vector2(btnW, btnH))) EditorSession.FlipHorizontal();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("垂直翻转", new Vector2(btnW, btnH))) EditorSession.FlipVertical();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("旋转 90°", new Vector2(btnW, btnH))) EditorSession.Rotate90();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("清除选区", new Vector2(btnW, btnH))) sel.Clear();

        if (ImGui.Button("撤销 (Ctrl+Z)", new Vector2(btnW, btnH))) EditorSession.Undo();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("重做 (Ctrl+Y)", new Vector2(btnW, btnH))) EditorSession.Redo();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 3. 笔刷配置与形态卡片
        Ui.Heading(Icons.Brush, "笔刷工具与形态配置 (Brush & Shape)");
        ImGui.BeginChild("brush-settings-card", new Vector2(0, 140f), ImGuiChildFlags.Borders);
        var bMin = ImGui.GetWindowPos();
        var bMax = bMin + ImGui.GetWindowSize();
        Ui.DrawPixelPanel(dl, bMin, bMax);

        // 工具切换 Chips
        int tool = (int)EditorSession.Tool;
        Ui.Chips("tools-chips", Ui.ToolNames.Length, ref tool, i => Ui.ToolNames[i], 26f);
        EditorSession.Tool = (EditorTool)tool;

        ImGui.Spacing();

        // 形状选择 Chips
        int shape = (int)EditorSession.BrushShape;
        Ui.Chips("shape-chips", Ui.ShapeNames.Length, ref shape, i => $"形状: {Ui.ShapeNames[i]}", 24f);
        EditorSession.BrushShape = (BrushShape)shape;

        ImGui.Spacing();

        // 半径调节滑块
        ImGui.SliderInt("笔刷半径", ref EditorSession.BrushRadius, 0, 50, $"{EditorSession.BrushRadius} (直径 {EditorSession.BrushRadius * 2 + 1} 砖)");
        ImGui.TextColored(Ui.Accent, "💡 提示: 在世界中按住 Ctrl + 滚动鼠标滚轮 可实时调整笔刷大小并显示范围预览与提示窗。");

        ImGui.EndChild();

        ImGui.EndChild();
    }

    private static void DrawWorldTab()
    {
        Ui.BeginScroll("world-scroll");

        float availW = ImGui.GetContentRegionAvail().X;
        float halfW = (availW - 8f) * 0.5f;

        // 左列卡片: 创造特权
        ImGui.BeginChild("rules-card-left", new Vector2(halfW, 186f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        Ui.Heading(Icons.Shield, "创造特权 (Privileges)");
        ImGui.Checkbox("上帝模式 (God Mode)", ref CheatState.GodMode);
        ImGui.Checkbox("全图照明 (Full Bright)", ref CheatState.FullBright);
        ImGui.Checkbox("穿墙模式 (No Clip)", ref CheatState.NoClip);
        ImGui.Checkbox("自由悬空放置 (Free Placement)", ref CheatState.FreePlacement);
        ImGui.Checkbox("快捷传送 (Click Teleport)", ref CheatState.ClickTeleport);

        ImGui.EndChild();

        ImGui.SameLine(0f, 8f);

        // 右列卡片: 建筑辅助
        ImGui.BeginChild("rules-card-right", new Vector2(halfW, 186f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        Ui.Heading(Icons.Infinity, "建筑辅助 (Assists)");
        ImGui.Checkbox("无限放置范围 (Infinite Reach)", ref CheatState.InfiniteReach);
        ImGui.Checkbox("无限物块消耗 (Infinite Items)", ref CheatState.InfiniteItems);
        ImGui.Checkbox("锁定世界时间 (Freeze Time)", ref CheatState.FreezeTime);

        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 时间与昼夜卡片
        Ui.Heading(Icons.Clock, "世界时间控制 (World Time & Phase)");
        ImGui.BeginChild("time-card", new Vector2(0, 110f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        float time = GetTimeFraction();
        if (ImGui.SliderFloat("时间进度 (0:00 - 24:00)", ref time, 0f, 1f, GetTimeString(time)))
            SetTimeFraction(time);

        ImGui.Spacing();

        float btnW = (ImGui.GetContentRegionAvail().X - 12f) / 4f;
        if (ImGui.Button("🌅 清晨 04:30", new Vector2(btnW, 26f))) SetTimeFraction(0f);
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("☀️ 正午 12:00", new Vector2(btnW, 26f))) SetTimeFraction(27000f / 86400f);
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("🌇 黄昏 19:30", new Vector2(btnW, 26f))) SetTimeFraction(54000f / 86400f);
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("🌙 午夜 00:00", new Vector2(btnW, 26f))) SetTimeFraction(70200f / 86400f);

        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextDisabled("提示: 开启快捷传送后，在大地图右键 或 在世界中按下鼠标中键 可瞬间传送到达对应位置。");

        ImGui.EndChild();
    }

    private static void DrawCharacterTab()
    {
        Ui.BeginScroll("char-scroll");

        if (Main.gameMenu || !Main.LocalPlayer.active)
        {
            Ui.Heading(Icons.Human, "角色装备");
            ImGui.TextUnformatted("请先进入世界以编辑角色属性与手持武器。");
            ImGui.EndChild();
            return;
        }

        var player = Main.LocalPlayer;

        // 1. 玩家基础属性
        Ui.Heading(Icons.Human, "玩家基础属性 (Player Stats)");
        ImGui.BeginChild("stats-card", new Vector2(0, 90f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

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
        if (ImGui.SliderInt("发型款式", ref hair, 0, Main.maxHairStyles - 1))
            player.hair = hair;

        ImGui.EndChild();

        ImGui.Spacing();

        // 2. 手持装备属性修改器
        Ui.Heading(Icons.Pencil, "当前手持/抓取物品属性修改 (Held Item Editor)");
        ImGui.BeginChild("held-item-card", new Vector2(0, 160f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        var held = !Main.mouseItem.IsAir ? Main.mouseItem : player.HeldItem;
        if (held.IsAir)
        {
            ImGui.TextDisabled("当前手持或鼠标光标未抓取任何物品 (请在背包中拾取或选中一件物品)。");
        }
        else
        {
            // 物品图标与名称
            UiIcons.DrawItemDirect(dl, ImGui.GetCursorScreenPos() + new Vector2(16f, 16f), held.type, 28f);
            ImGui.Dummy(new Vector2(36f, 32f));
            ImGui.SameLine();
            ImGui.TextColored(Ui.Gold, $"{held.Name} (ID: {held.type})");

            float halfW = (ImGui.GetContentRegionAvail().X - 8f) * 0.5f;

            // 左列
            ImGui.BeginGroup();
            int damage = held.damage;
            ImGui.SetNextItemWidth(halfW - 80f);
            if (ImGui.InputInt("基础伤害", ref damage)) held.damage = damage;

            int useTime = held.useTime;
            ImGui.SetNextItemWidth(halfW - 80f);
            if (ImGui.InputInt("使用间隔", ref useTime)) held.useTime = useTime;

            float shoot = held.shootSpeed;
            ImGui.SetNextItemWidth(halfW - 80f);
            if (ImGui.InputFloat("弹幕射速", ref shoot)) held.shootSpeed = shoot;
            ImGui.EndGroup();

            ImGui.SameLine(0f, 8f);

            // 右列
            ImGui.BeginGroup();
            int crit = held.crit;
            ImGui.SetNextItemWidth(halfW - 80f);
            if (ImGui.InputInt("暴击加成", ref crit)) held.crit = crit;

            int useAnim = held.useAnimation;
            ImGui.SetNextItemWidth(halfW - 80f);
            if (ImGui.InputInt("动画帧长", ref useAnim)) held.useAnimation = useAnim;

            ImGui.Checkbox("自动连发 (Auto Reuse)", ref held.autoReuse);
            ImGui.EndGroup();
        }

        ImGui.EndChild();

        ImGui.Spacing();

        // 3. 外观配色 (双列紧凑排版)
        Ui.Heading(Icons.Colors, "外观配色 (Colors)");
        ImGui.BeginChild("colors-card", new Vector2(0, 110f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        float colW = (ImGui.GetContentRegionAvail().X - 12f) / 3f;

        ImGui.BeginGroup();
        ColorEdit("发色", ref player.hairColor, colW);
        ColorEdit("肤色", ref player.skinColor, colW);
        ImGui.EndGroup();

        ImGui.SameLine(0f, 6f);

        ImGui.BeginGroup();
        ColorEdit("眼睛", ref player.eyeColor, colW);
        ColorEdit("上衣", ref player.shirtColor, colW);
        ColorEdit("内衬", ref player.underShirtColor, colW);
        ImGui.EndGroup();

        ImGui.SameLine(0f, 6f);

        ImGui.BeginGroup();
        ColorEdit("长裤", ref player.pantsColor, colW);
        ColorEdit("鞋子", ref player.shoeColor, colW);
        ImGui.EndGroup();

        ImGui.EndChild();

        ImGui.EndChild();
    }

    private static void ColorEdit(string label, ref Microsoft.Xna.Framework.Color color, float width)
    {
        var rgb = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        ImGui.SetNextItemWidth(width - 50f);
        if (ImGui.ColorEdit3(label, ref rgb, ImGuiColorEditFlags.NoInputs))
            color = new Microsoft.Xna.Framework.Color(rgb.X, rgb.Y, rgb.Z);
    }

    private void DrawSchematicsTab()
    {
        Ui.BeginScroll("schem-scroll");

        Ui.Heading(Icons.Script, "蓝图文件导入与导出 (Schematics IO)");
        ImGui.InputText("蓝图路径", ref SchemPathInput, (UIntPtr)512);

        float btnW = 140f;
        if (ImGui.Button("导入文件 (Import)", new Vector2(btnW, 26f)) && SchemPathInput.Length > 0)
        {
            try
            {
                SchemLibrary.Add(SchematicSerializer.Load(SchemPathInput));
                SelectedSchemIndex = SchemLibrary.Count - 1;
                Notices.Post("蓝图已成功导入");
            }
            catch (Exception ex)
            {
                Notices.Post($"导入失败: {ex.Message}");
            }
        }

        ImGui.SameLine(0f, 6f);
        if (ImGui.Button("导出当前蓝图 (Export)", new Vector2(btnW, 26f)) && SchemPathInput.Length > 0 && SelectedSchemIndex >= 0)
        {
            try
            {
                SchematicSerializer.Save(SchemLibrary[SelectedSchemIndex], SchemPathInput);
                Notices.Post("蓝图已成功导出");
            }
            catch (Exception ex)
            {
                Notices.Post($"导出失败: {ex.Message}");
            }
        }

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        Ui.Heading(Icons.SectionCopy, "蓝图库列表 (Library)");
        ImGui.InputTextWithHint("##schemFilter", "过滤蓝图...", ref SchemSearch, (UIntPtr)128);

        ImGui.BeginChild("schem-list-child", new Vector2(0, 160f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        if (SchemLibrary.Count == 0)
        {
            ImGui.TextDisabled("暂无加载的蓝图。可在选区中复制后保存为蓝图，或从文件导入。");
        }
        else
        {
            for (int i = 0; i < SchemLibrary.Count; i++)
            {
                var schematic = SchemLibrary[i];
                if (SchemSearch.Length > 0 && schematic.Name.IndexOf(SchemSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (ImGui.Selectable($"{schematic.Name} ({schematic.Width} x {schematic.Height})", SelectedSchemIndex == i))
                    SelectedSchemIndex = i;
            }
        }

        ImGui.EndChild();

        ImGui.EndChild();
    }

    private static void DrawSettingsTab()
    {
        Ui.BeginScroll("settings-scroll");

        // 1. 显示与性能
        Ui.Heading(Icons.Sliders, "显示与刷新率");
        ImGui.Checkbox("帧率跟随屏幕刷新率 (Unlock FPS)", ref CheatState.UnlockFps);

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 2. 通知弹窗配置
        Ui.Heading(Icons.Settings, "通知与提示 (Notifications)");
        var corner = CheatState.NotifyAnchor;
        if (Ui.CornerCombo("弹出位置", ref corner))
            CheatState.NotifyAnchor = corner;

        ImGui.SliderFloat("通知缩放", ref CheatState.NotifyScale, 0.7f, 1.6f, "%.2f");
        if (ImGui.Button("发送测试通知", new Vector2(120f, 24f)))
            Notices.Post("箱庭工坊通知系统正常");

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 3. 快捷键一览速查卡片
        Ui.Heading(Icons.Script, "快捷键速查表 (Hotkeys)");
        ImGui.BeginChild("hotkeys-cheatsheet", new Vector2(0, 150f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        ImGui.Columns(2, "hotkey-cols", false);
        ImGui.SetColumnWidth(0, 220f);

        ImGui.TextColored(Ui.Gold, "Insert"); ImGui.NextColumn(); ImGui.TextUnformatted("显示 / 隐藏 箱庭所有浮层界面"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Enter"); ImGui.NextColumn(); ImGui.TextUnformatted("呼出箱庭 ImGui 聊天输入框"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Ctrl + 滚轮"); ImGui.NextColumn(); ImGui.TextUnformatted("调节笔刷/橡皮擦半径 (带范围预览与HUD)"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Ctrl + C / X / V"); ImGui.NextColumn(); ImGui.TextUnformatted("选区复制 / 剪切 / 粘贴"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Ctrl + Z / Y"); ImGui.NextColumn(); ImGui.TextUnformatted("撤销 / 重做 上一步瓦片修改"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "大地图右键 / 中键"); ImGui.NextColumn(); ImGui.TextUnformatted("快速瞬间传送 (需开启快捷传送)"); ImGui.NextColumn();

        ImGui.Columns(1);
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
