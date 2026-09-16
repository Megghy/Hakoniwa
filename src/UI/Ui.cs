using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;
using System.Numerics;

namespace Hakoniwa.UI;

public static class Ui
{
    public static readonly Vector4 Accent = new(0.55f, 0.75f, 1f, 1f);
    public const uint ChipIdle = 0xE01A162B;
    public const uint ChipHover = 0xF02A2440;
    public const uint ChipLine = 0xFF6D28D9;
    public const uint ChipOn = 0xFF38BDF8;

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

    public static bool BeginChrome(string id, Vector2 pos, Vector2 size)
    {
        ImGui.SetNextWindowPos(pos, ImGuiCond.Always);
        ImGui.SetNextWindowSize(size, ImGuiCond.Always);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 8f);
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
