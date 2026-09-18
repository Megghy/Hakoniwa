using System;
using System.Numerics;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Tools;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;

namespace Hakoniwa.UI.Windows;

public sealed class StudioWindow
{
    public string Title => "箱庭工坊 (Hakoniwa Studio)###HakoniwaMainStudio";
    public bool IsOpen { get; set; }

    private int _currentTab;
    private readonly SchematicLibrary _schems = new();
    private static readonly string[] Tabs = ["建造选区", "世界规则", "物品库", "角色装备", "蓝图方案", "设置"];
    private static readonly string[] TabIcons = [Icons.Crop, Icons.Earth, Icons.Box, Icons.Human, Icons.Script, Icons.Settings];

    public void LoadLibrary() => _schems.Refresh();
    public void SaveFromSelection() => _schems.SaveFromSelection();

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
                    WorldTab.Draw();
                    break;
                case 2:
                    HakoniwaUi.ItemPicker.DrawContent(embedded: true);
                    break;
                case 3:
                    CharacterTab.Draw();
                    break;
                case 4:
                    _schems.Draw();
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
        if (ImGui.Button("粘贴 (Ctrl+V)", new Vector2(btnW, btnH))) EditorSession.BeginPaste();
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
        ImGui.BeginChild("brush-settings-card", new Vector2(0, 210f), ImGuiChildFlags.Borders);
        var bMin = ImGui.GetWindowPos();
        var bMax = bMin + ImGui.GetWindowSize();
        Ui.DrawPixelPanel(dl, bMin, bMax);

        // 工具切换 Chips
        int tool = (int)EditorSession.Tool;
        Ui.Chips("tools-chips", Ui.ToolNames.Length, ref tool, i => Ui.ToolNames[i], 26f);
        EditorSession.SetTool((EditorTool)tool);

        ImGui.Spacing();

        if (EditorSession.Tool == EditorTool.Shape)
        {
            int draw = (int)EditorSession.DrawKind;
            Ui.Chips("draw-chips", Ui.DrawNames.Length, ref draw, i => Ui.DrawNames[i], 24f);
            EditorSession.DrawKind = (DrawKind)draw;
            ImGui.Spacing();
        }

        int shape = (int)EditorSession.BrushShape;
        Ui.Chips("shape-chips", Ui.ShapeNames.Length, ref shape, i => $"笔刷: {Ui.ShapeNames[i]}", 24f);
        EditorSession.BrushShape = (BrushShape)shape;

        ImGui.Spacing();

        // 半径调节滑块
        ImGui.SliderInt("笔刷半径", ref EditorSession.BrushRadius, 0, 50, $"{EditorSession.BrushRadius} (直径 {EditorSession.BrushRadius * 2 + 1} 砖)");
        DrawLayerToggles();
        var stamp = EditorSession.CurrentStamp();
        ImGui.TextDisabled(EditorSession.HasStamp
            ? $"图章 物块 {stamp.TileType} 墙 {stamp.WallType} 帧 {stamp.TileFrameX},{stamp.TileFrameY}"
            : "图章来自手持物品（吸管可锁定世界格子）");
        if (EditorSession.HasMatch)
            ImGui.TextDisabled($"替换源 物块 {EditorSession.Match.TileType} 墙 {EditorSession.Match.WallType}");
        ImGui.TextDisabled("Ctrl+滚轮调半径。替换：右键取样源，左键替换选区或可见范围。");

        ImGui.EndChild();

        ImGui.EndChild();
    }

    private static void DrawLayerToggles()
    {
        var layers = EditorSession.Layers;
        for (int i = 0; i < Ui.LayerNames.Length; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            var flag = (TileLayer)(1 << i);
            bool on = (layers & flag) != 0;
            if (ImGui.Checkbox(Ui.LayerNames[i], ref on))
            {
                var next = on ? layers | flag : layers & ~flag;
                if (next != TileLayer.None)
                    layers = next;
            }
        }

        EditorSession.Layers = layers;
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

        Ui.Heading(Icons.Script, "文本输入");
        if (ImGui.RadioButton("原版输入增强（光标 / 选区 / 撤销）", !CheatState.ImGuiInput))
            CheatState.ImGuiInput = false;
        if (ImGui.RadioButton("ImGui 接管聊天与标牌", CheatState.ImGuiInput))
            CheatState.ImGuiInput = true;
        ImGui.TextDisabled(CheatState.ImGuiInput
            ? "聊天与标牌走工坊输入框，带补全与消息列表。"
            : "沿用原版输入条，补上光标、选区、撤销与历史上翻。");

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
        ImGui.TextColored(Ui.Gold, "Enter"); ImGui.NextColumn(); ImGui.TextUnformatted(CheatState.ImGuiInput ? "呼出工坊聊天输入框" : "打开原版聊天（增强光标/选区）"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Ctrl + 滚轮"); ImGui.NextColumn(); ImGui.TextUnformatted("调节笔刷/橡皮擦半径 (带范围预览与HUD)"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Ctrl + C / X / V"); ImGui.NextColumn(); ImGui.TextUnformatted("选区复制 / 剪切 / 粘贴"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "Ctrl + Z / Y"); ImGui.NextColumn(); ImGui.TextUnformatted("撤销 / 重做 上一步瓦片修改"); ImGui.NextColumn();
        ImGui.TextColored(Ui.Gold, "大地图右键 / 中键"); ImGui.NextColumn(); ImGui.TextUnformatted("快速瞬间传送 (需开启快捷传送)"); ImGui.NextColumn();

        ImGui.Columns(1);
        ImGui.EndChild();

        ImGui.EndChild();
    }
}
