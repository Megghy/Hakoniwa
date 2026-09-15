using Hakoniwa.Engine;
using ImGuiNET;
using Num = System.Numerics;

namespace Hakoniwa.UI.Windows;

public sealed class SelectionWindow : IWindow
{
    public string Title => "Hakoniwa - Selection###HakoniwaSelection";
    public string Label => "Select";
    public bool IsOpen { get; set; }
    public Selection Selection { get; }

    public SelectionWindow(Selection selection)
    {
        Selection = selection;
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Num.Vector2(300, 260), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.TextUnformatted(Selection.Active
                ? $"{Selection.MinX},{Selection.MinY}  {Selection.Width}x{Selection.Height}"
                : "No selection");
            if (ImGui.Button("Copy"))
                EditorSession.Copy();
            ImGui.SameLine();
            if (ImGui.Button("Cut"))
                EditorSession.Cut();
            ImGui.SameLine();
            if (ImGui.Button("Paste"))
                EditorSession.Paste();
            if (ImGui.Button("Flip H"))
                EditorSession.FlipHorizontal();
            ImGui.SameLine();
            if (ImGui.Button("Flip V"))
                EditorSession.FlipVertical();
            ImGui.SameLine();
            if (ImGui.Button("Rotate 90"))
                EditorSession.Rotate90();
            if (ImGui.Button("Undo"))
                EditorSession.Undo();
            ImGui.SameLine();
            if (ImGui.Button("Redo"))
                EditorSession.Redo();
            ImGui.SameLine();
            if (ImGui.Button("Clear"))
                Selection.Clear();
        }

        IsOpen = open;
        ImGui.End();
    }
}
