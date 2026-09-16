using System.Numerics;
using Hexa.NET.ImGui;

namespace Hakoniwa.UI.Themes;

/// <summary>
/// Hakoniwa 像素工坊暗色主题 (Dark Slate / Violet Accent)
/// </summary>
public static class HakoniwaTheme
{
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        var colors = style.Colors;

        style.WindowRounding = 4f;
        style.ChildRounding = 4f;
        style.FrameRounding = 3f;
        style.PopupRounding = 4f;
        style.ScrollbarRounding = 3f;
        style.GrabRounding = 3f;
        style.TabRounding = 4f;

        style.WindowBorderSize = 1f;
        style.ChildBorderSize = 1f;
        style.PopupBorderSize = 1f;
        style.FrameBorderSize = 0f;

        style.WindowPadding = new Vector2(12f, 12f);
        style.FramePadding = new Vector2(8f, 5f);
        style.ItemSpacing = new Vector2(8f, 6f);
        style.ItemInnerSpacing = new Vector2(6f, 4f);
        style.ScrollbarSize = 10f;
        style.GrabMinSize = 8f;

        // 文本
        colors[(int)ImGuiCol.Text] = new Vector4(0.94f, 0.96f, 0.98f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.55f, 0.58f, 0.64f, 1.00f);

        // 窗口背景与卡片
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.08f, 0.09f, 0.12f, 0.94f);
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0.12f, 0.13f, 0.17f, 0.70f);
        colors[(int)ImGuiCol.PopupBg] = new Vector4(0.10f, 0.11f, 0.15f, 0.98f);
        colors[(int)ImGuiCol.Border] = new Vector4(0.24f, 0.26f, 0.34f, 0.75f);
        colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        // 控件基础色
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.16f, 0.18f, 0.24f, 0.85f);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.24f, 0.26f, 0.35f, 0.90f);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.32f, 0.34f, 0.45f, 1.00f);

        // 标题栏
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.08f, 0.09f, 0.12f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.16f, 0.14f, 0.24f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.06f, 0.07f, 0.09f, 0.80f);

        // 按钮 (Violet 强调色)
        colors[(int)ImGuiCol.Button] = new Vector4(0.35f, 0.24f, 0.58f, 0.85f);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.48f, 0.34f, 0.76f, 0.95f);
        colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.58f, 0.42f, 0.88f, 1.00f);

        // 表头与选中项
        colors[(int)ImGuiCol.Header] = new Vector4(0.26f, 0.22f, 0.42f, 0.75f);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.38f, 0.30f, 0.60f, 0.85f);
        colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.48f, 0.38f, 0.72f, 1.00f);

        // 滑块与复选框
        colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.65f, 0.48f, 0.95f, 1.00f);
        colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.80f, 0.65f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.CheckMark] = new Vector4(0.35f, 0.78f, 0.98f, 1.00f);

        // 分隔线与高亮
        colors[(int)ImGuiCol.Separator] = new Vector4(0.22f, 0.24f, 0.32f, 0.80f);
        colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.45f, 0.35f, 0.70f, 0.90f);
        colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.60f, 0.45f, 0.90f, 1.00f);

        // 页签
        colors[(int)ImGuiCol.Tab] = new Vector4(0.14f, 0.15f, 0.20f, 0.80f);
        colors[(int)ImGuiCol.TabHovered] = new Vector4(0.32f, 0.25f, 0.50f, 0.90f);
        colors[(int)ImGuiCol.TabSelected] = new Vector4(0.42f, 0.28f, 0.68f, 1.00f);
        colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.10f, 0.11f, 0.15f, 0.80f);
        colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.25f, 0.18f, 0.40f, 1.00f);

        // 滚动条
        colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.06f, 0.07f, 0.09f, 0.50f);
        colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.22f, 0.24f, 0.32f, 0.70f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.35f, 0.37f, 0.48f, 0.85f);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.45f, 0.48f, 0.62f, 1.00f);
    }
}
