using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Tools;
using ImGuiNET;
using Num = System.Numerics;

namespace Hakoniwa.UI.Windows;

public sealed class ToolboxWindow : IWindow
{
    public string Title => "Hakoniwa - Workshop###HakoniwaToolbox";
    public string Label => "Rules";
    public bool IsOpen { get; set; }

    private static readonly string[] Tools = ["Brush", "Fill", "Eraser", "Select"];
    private static readonly string[] Shapes = ["Circle", "Square", "Diamond"];

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Num.Vector2(320, 420), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.TextColored(new Num.Vector4(0.85f, 0.65f, 1f, 1f), "Rules");
            ImGui.Separator();
            ImGui.Checkbox("God mode", ref CheatState.GodMode);
            ImGui.Checkbox("Infinite reach", ref CheatState.InfiniteReach);
            ImGui.Checkbox("Infinite items", ref CheatState.InfiniteItems);
            ImGui.Checkbox("Free placement", ref CheatState.FreePlacement);
            ImGui.Checkbox("Full bright", ref CheatState.FullBright);

            ImGui.Spacing();
            ImGui.TextColored(new Num.Vector4(0.85f, 0.65f, 1f, 1f), "Tools");
            ImGui.Separator();
            ImGui.Combo("Current tool", ref EditorSession.SelectedTool, Tools, Tools.Length);
            int shape = (int)EditorSession.BrushShape;
            if (ImGui.Combo("Brush shape", ref shape, Shapes, Shapes.Length))
                EditorSession.BrushShape = (BrushShape)shape;
            ImGui.SliderInt("Brush radius", ref EditorSession.BrushRadius, 0, 50);
        }

        IsOpen = open;
        ImGui.End();
    }
}
