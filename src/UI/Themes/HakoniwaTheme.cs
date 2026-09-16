using System.Numerics;
using Hexa.NET.ImGui;

namespace Hakoniwa.UI.Themes;

/// <summary>
/// Hakoniwa 像素工坊主题 (Terraria 16-bit 像素硬朗风格 / 深黑曜石暗蓝 + 黄金/天蓝高光)
/// </summary>
public static class HakoniwaTheme
{
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        var colors = style.Colors;

        // 纯正像素风格：0 钝化圆角，硬朗直角像素切边
        style.WindowRounding = 0f;
        style.ChildRounding = 0f;
        style.FrameRounding = 0f;
        style.PopupRounding = 0f;
        style.ScrollbarRounding = 0f;
        style.GrabRounding = 0f;
        style.TabRounding = 0f;

        style.WindowBorderSize = 1f;
        style.ChildBorderSize = 1f;
        style.PopupBorderSize = 1f;
        style.FrameBorderSize = 1f;

        style.WindowPadding = new Vector2(10f, 10f);
        style.FramePadding = new Vector2(6f, 4f);
        style.ItemSpacing = new Vector2(6f, 5f);
        style.ItemInnerSpacing = new Vector2(5f, 4f);
        style.ScrollbarSize = 10f;
        style.GrabMinSize = 8f;

        // 文本 (高对比度像素白与灰白)
        colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.97f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.52f, 0.58f, 0.68f, 1.00f);

        // 窗口背景与卡片 (深黑曜石/星空蓝灰底色)
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.06f, 0.07f, 0.11f, 0.96f);
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0.09f, 0.11f, 0.16f, 0.85f);
        colors[(int)ImGuiCol.PopupBg] = new Vector4(0.07f, 0.08f, 0.12f, 0.98f);
        colors[(int)ImGuiCol.Border] = new Vector4(0.24f, 0.30f, 0.44f, 0.85f);
        colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        // 控件基础色 (像素内凹槽底色与边框)
        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.12f, 0.14f, 0.20f, 0.92f);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.18f, 0.22f, 0.32f, 0.95f);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.25f, 0.30f, 0.44f, 1.00f);

        // 标题栏 (黑曜石深蓝与像素边)
        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.06f, 0.07f, 0.10f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.12f, 0.16f, 0.26f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.04f, 0.05f, 0.07f, 0.80f);

        // 按钮 (Terraria 深晶蓝 / 悬停金蓝 / 激活高亮)
        colors[(int)ImGuiCol.Button] = new Vector4(0.18f, 0.22f, 0.34f, 0.90f);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.28f, 0.35f, 0.52f, 0.98f);
        colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.38f, 0.48f, 0.70f, 1.00f);

        // 表头与选中项
        colors[(int)ImGuiCol.Header] = new Vector4(0.20f, 0.26f, 0.38f, 0.80f);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.30f, 0.38f, 0.56f, 0.90f);
        colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.38f, 0.48f, 0.70f, 1.00f);

        // 滑块与复选框 (Terraria 天蓝与金黄色)
        colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.35f, 0.78f, 0.98f, 1.00f);
        colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.98f, 0.80f, 0.30f, 1.00f);
        colors[(int)ImGuiCol.CheckMark] = new Vector4(0.35f, 0.78f, 0.98f, 1.00f);

        // 分隔线与高亮
        colors[(int)ImGuiCol.Separator] = new Vector4(0.20f, 0.25f, 0.36f, 0.80f);
        colors[(int)ImGuiCol.SeparatorHovered] = new Vector4(0.35f, 0.78f, 0.98f, 0.90f);
        colors[(int)ImGuiCol.SeparatorActive] = new Vector4(0.98f, 0.80f, 0.30f, 1.00f);

        // 页签 (像素卡片式)
        colors[(int)ImGuiCol.Tab] = new Vector4(0.12f, 0.14f, 0.20f, 0.85f);
        colors[(int)ImGuiCol.TabHovered] = new Vector4(0.22f, 0.28f, 0.42f, 0.95f);
        colors[(int)ImGuiCol.TabSelected] = new Vector4(0.28f, 0.36f, 0.54f, 1.00f);
        colors[(int)ImGuiCol.TabDimmed] = new Vector4(0.08f, 0.10f, 0.14f, 0.80f);
        colors[(int)ImGuiCol.TabDimmedSelected] = new Vector4(0.18f, 0.24f, 0.36f, 1.00f);

        // 滚动条 (像素条)
        colors[(int)ImGuiCol.ScrollbarBg] = new Vector4(0.06f, 0.07f, 0.09f, 0.60f);
        colors[(int)ImGuiCol.ScrollbarGrab] = new Vector4(0.20f, 0.25f, 0.36f, 0.80f);
        colors[(int)ImGuiCol.ScrollbarGrabHovered] = new Vector4(0.30f, 0.38f, 0.54f, 0.90f);
        colors[(int)ImGuiCol.ScrollbarGrabActive] = new Vector4(0.40f, 0.50f, 0.70f, 1.00f);
    }
}
