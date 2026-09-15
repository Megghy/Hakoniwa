using Hakoniwa.Core;
using ImGuiNET;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI.Windows;

public sealed class WorldControlWindow : IWindow
{
    public string Title => "Hakoniwa - World###HakoniwaWorld";
    public string Label => "World";
    public bool IsOpen { get; set; }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Num.Vector2(300, 220), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            float time = TimeFraction();
            if (ImGui.SliderFloat("Time of day", ref time, 0f, 1f))
                SetTimeFraction(time);
            ImGui.Checkbox("Freeze time", ref CheatState.FreezeTime);
            ImGui.Checkbox("Click teleport", ref CheatState.ClickTeleport);
            ImGui.TextWrapped("Map right-click / world middle-click teleport.");
        }

        IsOpen = open;
        ImGui.End();
    }

    private static float TimeFraction()
    {
        double cycle = Main.dayTime ? Main.time : 54000.0 + Main.time;
        return (float)(cycle / 86400.0);
    }

    private static void SetTimeFraction(float fraction)
    {
        if (fraction < 0f)
            fraction = 0f;
        if (fraction > 1f)
            fraction = 1f;
        double cycle = fraction * 86400.0;
        if (cycle < 54000.0)
            Main.SkipToTime((int)cycle, true);
        else
            Main.SkipToTime((int)(cycle - 54000.0), false);
        CheatState.FrozenTime = Main.time;
    }
}
