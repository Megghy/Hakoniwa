using System.Numerics;
using ImGuiNET;

namespace Hakoniwa.UI.Themes;

/// <summary>
/// Hakoniwa 二次元质感暗色主题 (Cyber Lavender / Deep Charcoal)
/// </summary>
public static class HakoniwaTheme
{
    public static void Apply()
    {
        var style = ImGui.GetStyle();
        var colors = style.Colors;

        style.WindowRounding = 8f;
        style.ChildRounding = 6f;
        style.FrameRounding = 5f;
        style.PopupRounding = 6f;
        style.ScrollbarRounding = 6f;
        style.GrabRounding = 5f;
        style.TabRounding = 6f;

        style.WindowBorderSize = 1f;
        style.FrameBorderSize = 0f;
        style.PopupBorderSize = 1f;

        style.WindowPadding = new Vector2(10, 10);
        style.FramePadding = new Vector2(8, 4);
        style.ItemSpacing = new Vector2(8, 6);

        colors[(int)ImGuiCol.Text] = new Vector4(0.95f, 0.95f, 0.98f, 1.00f);
        colors[(int)ImGuiCol.TextDisabled] = new Vector4(0.55f, 0.55f, 0.65f, 1.00f);
        colors[(int)ImGuiCol.WindowBg] = new Vector4(0.10f, 0.10f, 0.14f, 0.92f);
        colors[(int)ImGuiCol.ChildBg] = new Vector4(0.12f, 0.12f, 0.18f, 0.60f);
        colors[(int)ImGuiCol.PopupBg] = new Vector4(0.12f, 0.12f, 0.16f, 0.96f);
        colors[(int)ImGuiCol.Border] = new Vector4(0.35f, 0.28f, 0.48f, 0.50f);
        colors[(int)ImGuiCol.BorderShadow] = new Vector4(0.00f, 0.00f, 0.00f, 0.00f);

        colors[(int)ImGuiCol.FrameBg] = new Vector4(0.18f, 0.18f, 0.25f, 0.60f);
        colors[(int)ImGuiCol.FrameBgHovered] = new Vector4(0.28f, 0.24f, 0.40f, 0.80f);
        colors[(int)ImGuiCol.FrameBgActive] = new Vector4(0.38f, 0.30f, 0.55f, 0.90f);

        colors[(int)ImGuiCol.TitleBg] = new Vector4(0.10f, 0.08f, 0.15f, 1.00f);
        colors[(int)ImGuiCol.TitleBgActive] = new Vector4(0.22f, 0.16f, 0.35f, 1.00f);
        colors[(int)ImGuiCol.TitleBgCollapsed] = new Vector4(0.08f, 0.08f, 0.10f, 0.75f);

        colors[(int)ImGuiCol.Button] = new Vector4(0.32f, 0.22f, 0.50f, 0.80f);
        colors[(int)ImGuiCol.ButtonHovered] = new Vector4(0.48f, 0.32f, 0.75f, 0.95f);
        colors[(int)ImGuiCol.ButtonActive] = new Vector4(0.60f, 0.40f, 0.90f, 1.00f);

        colors[(int)ImGuiCol.Header] = new Vector4(0.30f, 0.22f, 0.45f, 0.70f);
        colors[(int)ImGuiCol.HeaderHovered] = new Vector4(0.45f, 0.32f, 0.68f, 0.85f);
        colors[(int)ImGuiCol.HeaderActive] = new Vector4(0.55f, 0.38f, 0.82f, 1.00f);

        colors[(int)ImGuiCol.SliderGrab] = new Vector4(0.70f, 0.45f, 0.95f, 1.00f);
        colors[(int)ImGuiCol.SliderGrabActive] = new Vector4(0.85f, 0.60f, 1.00f, 1.00f);
        colors[(int)ImGuiCol.CheckMark] = new Vector4(0.85f, 0.55f, 1.00f, 1.00f);
    }
}
