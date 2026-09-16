using System;
using System.Runtime.InteropServices;
using System.Text;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using System.Numerics;

namespace Hakoniwa.UI;

public static class Ui
{
    public static readonly Vector4 Accent = new(0.35f, 0.78f, 0.98f, 1f); // Terraria Cyan
    public static readonly Vector4 Gold = new(0.98f, 0.80f, 0.30f, 1f);   // Terraria Gold
    public static readonly Vector4 White = new(0.96f, 0.98f, 1f, 1f);

    public const uint ChipIdle = 0xF0101420;
    public const uint ChipHover = 0xF81C2438;
    public const uint ChipLine = 0xFF2A344C;
    public const uint ChipOn = 0xFF38BDF8;
    public const uint SlotBg = 0xF00A0C12;
    public const uint SlotInner = 0xFF141A28;
    public const uint GoldBorder = 0xFFF8BD38;

    public static readonly string[] ToolNames = ["选区", "笔刷", "油漆桶", "橡皮擦"];
    public static readonly string[] ShapeNames = ["圆形", "方形", "菱形"];

    public const ImGuiWindowFlags Overlay =
        ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar |
        ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoDocking;

    public const ImGuiWindowFlags Chrome = Overlay | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoBackground |
                                           ImGuiWindowFlags.NoNav | ImGuiWindowFlags.NoBringToFrontOnFocus |
                                           ImGuiWindowFlags.NoFocusOnAppearing;

    public const ImGuiWindowFlags Toast = Overlay | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoInputs |
                                          ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoNav |
                                          ImGuiWindowFlags.AlwaysAutoResize;

    public static ImDrawListPtr WorldList => ImGui.GetBackgroundDrawList();

    public static bool Mouse { get; private set; }
    public static bool Keyboard { get; private set; }

    public static void Sync(bool extraMouse = false, bool extraKeyboard = false)
    {
        var io = ImGui.GetIO();
        Mouse = extraMouse || io.WantCaptureMouse;
        Keyboard = extraKeyboard || io.WantCaptureKeyboard;
        CheatHooks.BlockGameMouse = Mouse;
        CheatHooks.BlockGameKeyboard = Keyboard;
        CheatHooks.WantTextInput = io.WantTextInput;
    }

    public static unsafe string GetClipboardText()
    {
        byte* ptr = ImGui.GetClipboardText();
        if (ptr == null)
            return string.Empty;
        int len = 0;
        while (ptr[len] != 0)
            len++;
        if (len == 0)
            return string.Empty;
        var bytes = new byte[len];
        Marshal.Copy((IntPtr)ptr, bytes, 0, len);
        return Encoding.UTF8.GetString(bytes);
    }

    public static bool Invisible(string id, Vector2 screen, Vector2 size)
    {
        ImGui.SetCursorScreenPos(screen);
        return ImGui.InvisibleButton(id, size);
    }

    public static bool CornerCombo(string label, ref NotifyCorner corner) =>
        ComboEnumHelper<NotifyCorner>.Combo(label, ref corner);

    public static void Heading(string icon, string text)
    {
        var p = ImGui.GetCursorScreenPos();
        Icons.DrawDirect(ImGui.GetWindowDrawList(), p + new Vector2(12f, 10f), icon);
        ImGui.Dummy(new Vector2(24f, 20f));
        ImGui.SameLine();
        ImGui.TextColored(Accent, text);
    }

    public static void Chips(string id, int count, ref int selected, Func<int, string> name, float height = 22f, Func<int, string>? icon = null)
    {
        ImGui.PushID(id);
        float avail = ImGui.GetContentRegionAvail().X;
        float gap = ImGui.GetStyle().ItemSpacing.X;
        float width = (avail - gap * (count - 1)) / count;
        var dl = icon is null ? default : ImGui.GetWindowDrawList();
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            bool on = selected == i;
            if (on)
                ImGui.PushStyleColor(ImGuiCol.Button, Accent);
            string label = icon is null ? $"{name(i)}##{i}" : $"      {name(i)}##{i}";
            if (ImGui.Button(label, new Vector2(width, height)))
                selected = i;
            if (icon is not null)
                Icons.DrawDirect(dl, ImGui.GetItemRectMin() + new Vector2(16f, height * 0.5f), icon(i));
            if (on)
                ImGui.PopStyleColor();
        }

        ImGui.PopID();
    }

    public static bool BeginScroll(string id) =>
        ImGui.BeginChild(id, Vector2.Zero, ImGuiChildFlags.None);

    public static void DrawPixelSlot(ImDrawListPtr dl, Vector2 min, Vector2 max, bool hover, bool active, uint customBorder = 0)
    {
        uint bg = active ? 0xF8161E30 : (hover ? 0xF8121724 : SlotBg);
        uint border = customBorder != 0 ? customBorder : (active ? GoldBorder : (hover ? ChipOn : ChipLine));

        // 像素内凹槽：底色 + 边框
        dl.AddRectFilled(min, max, bg);
        dl.AddRect(min, max, border, 0f, ImDrawFlags.None, 1f);

        // 像素立体凹槽阴影 (左上暗角线，右下微亮反光线)
        dl.AddLine(min + new Vector2(1f, 1f), new Vector2(max.X - 1f, min.Y + 1f), 0xFF06070B);
        dl.AddLine(min + new Vector2(1f, 1f), new Vector2(min.X + 1f, max.Y - 1f), 0xFF06070B);
        dl.AddLine(new Vector2(min.X + 1f, max.Y - 1f), max - new Vector2(1f, 1f), SlotInner);
        dl.AddLine(new Vector2(max.X - 1f, min.Y + 1f), max - new Vector2(1f, 1f), SlotInner);
    }

    public static void DrawPixelPanel(ImDrawListPtr dl, Vector2 min, Vector2 max, uint bg = 0, uint borderOuter = 0, uint borderInner = 0)
    {
        uint fill = bg != 0 ? bg : 0xF20F121C;
        uint outer = borderOuter != 0 ? borderOuter : 0xFF242E46;
        uint inner = borderInner != 0 ? borderInner : 0xFF141926;

        dl.AddRectFilled(min, max, fill);
        dl.AddRect(min, max, outer, 0f, ImDrawFlags.None, 1.5f);
        if (max.X - min.X > 4f && max.Y - min.Y > 4f)
            dl.AddRect(min + new Vector2(2f, 2f), max - new Vector2(2f, 2f), inner, 0f, ImDrawFlags.None, 1f);
    }

    public static bool BeginChrome(string id, Vector2 pos, Vector2 size)
    {
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, Vector2.Zero);
        if (ImGui.Begin(id, Chrome))
            return true;
        EndChrome();
        return false;
    }

    public static void EndChrome()
    {
        ImGui.End();
        ImGui.PopStyleVar(4);
    }
}
