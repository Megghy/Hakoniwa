using System;
using System.Collections.Generic;
using Hakoniwa.Core;
using ImGuiNET;
using Terraria;
using Terraria.ID;
using Num = System.Numerics;

namespace Hakoniwa.UI.Windows;

public sealed class ItemBrowserWindow : IWindow
{
    public string Title => "Hakoniwa - Items###HakoniwaItems";
    public string Label => "Items";
    public bool IsOpen { get; set; }

    private string _search = string.Empty;
    private int _category;
    private readonly List<int> _hits = [];
    private string _lastQuery = "\0";
    private static readonly string[] Categories = ["All", "Tools", "Armor", "Vanity", "Dyes"];

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Num.Vector2(360, 520), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            ImGui.InputText("Search", ref _search, 128);
            ImGui.Combo("Category", ref _category, Categories, Categories.Length);
            Refresh();
            ImGui.TextUnformatted($"{_hits.Count} items");
            ImGui.BeginChild("item-list");
            for (int i = 0; i < _hits.Count; i++)
            {
                int id = _hits[i];
                if (ImGui.Selectable($"{Lang.GetItemNameValue(id)}##{id}"))
                    Give(id);
            }

            ImGui.EndChild();
        }

        IsOpen = open;
        ImGui.End();
    }

    private void Refresh()
    {
        string key = _category + "\n" + _search;
        if (key == _lastQuery)
            return;
        _lastQuery = key;
        _hits.Clear();
        if (Main.gameMenu)
            return;

        var category = (ItemCategory)_category;
        var probe = new Item();
        for (int id = 1; id < ItemID.Count; id++)
        {
            string name = Lang.GetItemNameValue(id);
            if (name.Length == 0)
                continue;
            if (_search.Length > 0 && name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            probe.SetDefaults(id);
            if (!ItemCatalog.Matches(probe, category))
                continue;
            _hits.Add(id);
        }
    }

    private static void Give(int id)
    {
        var item = new Item();
        item.SetDefaults(id);
        item.stack = Math.Max(1, item.maxStack);
        Main.mouseItem = item;
    }
}
