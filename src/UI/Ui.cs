using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Hexa.NET.ImGui.Widgets;

namespace Hakoniwa.UI;

public static class Ui
{
    public static bool CornerCombo(string label, ref NotifyCorner corner) =>
        ComboEnumHelper<NotifyCorner>.Combo(label, ref corner);

    public static void Heading(int itemId, string text)
    {
        UiIcons.DrawItem(itemId, 20f);
        ImGui.SameLine();
        ImGui.TextColored(new System.Numerics.Vector4(0.55f, 0.75f, 1f, 1f), text);
    }
}
