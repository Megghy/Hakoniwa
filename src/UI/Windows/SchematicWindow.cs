using System;
using System.Collections.Generic;
using System.Numerics;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.IO;
using ImGuiNET;

namespace Hakoniwa.UI.Windows;

public sealed class SchematicWindow : IWindow
{
    public string Title => "Hakoniwa - Schematics###HakoniwaSchematics";
    public string Label => "Plans";
    public bool IsOpen { get; set; }
    public List<Schematic> Library { get; } = [];
    public string Search = string.Empty;
    public int SelectedIndex = -1;
    public string PathInput = string.Empty;

    public void ImportFile(string path)
    {
        Library.Add(SchematicSerializer.Load(path));
        SelectedIndex = Library.Count - 1;
    }

    public void ExportSelected(string path)
    {
        if ((uint)SelectedIndex >= (uint)Library.Count)
            throw new InvalidOperationException("No schematic is selected.");
        SchematicSerializer.Save(Library[SelectedIndex], path);
    }

    public void Draw()
    {
        if (!IsOpen) return;

        ImGui.SetNextWindowSize(new Vector2(360, 460), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.InputText("Search", ref Search, 128);
            ImGui.InputText("Path", ref PathInput, 512);

            if (ImGui.Button("Import") && PathInput.Length > 0)
                ImportFile(PathInput);
            ImGui.SameLine();
            if (ImGui.Button("Export") && PathInput.Length > 0 && SelectedIndex >= 0)
                ExportSelected(PathInput);

            ImGui.Separator();
            for (int i = 0; i < Library.Count; i++)
            {
                var schematic = Library[i];
                if (Search.Length > 0 && schematic.Name.IndexOf(Search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (ImGui.Selectable($"{schematic.Name} ({schematic.Width}x{schematic.Height})", SelectedIndex == i))
                    SelectedIndex = i;
            }
        }

        IsOpen = open;
        ImGui.End();
    }
}
