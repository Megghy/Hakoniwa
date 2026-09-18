using System;
using System.Collections.Generic;
using Hexa.NET.ImGui;
using Terraria;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;

namespace Hakoniwa.UI.Windows;

internal static class IdPicker
{
    public enum Kind { Projectile, Ammo }

    private const float Cell = 42f;
    private static string _search = string.Empty;
    private static Kind _searchKind;
    private static string _lastKey = "\0";
    private static readonly List<int> Hits = [];
    private static int[]? _ammoClasses;

    public static bool Draw(string label, ref int id, Kind kind)
    {
        bool changed = false;
        ImGui.PushID(label);
        float avail = ImGui.GetContentRegionAvail().X;
        const float pick = 24f;
        var dl = ImGui.GetWindowDrawList();
        var slotMin = ImGui.GetCursorScreenPos();
        var slotMax = slotMin + new Vector2(pick, pick);
        if (ImGui.InvisibleButton("##pick", new Vector2(pick, pick)))
            ImGui.OpenPopup("##grid");
        bool hover = ImGui.IsItemHovered();
        Ui.DrawPixelSlot(dl, slotMin, slotMax, hover, id > 0);
        DrawIcon(dl, (slotMin + slotMax) * 0.5f, id, kind, 18f);
        if (hover)
            ImGui.SetTooltip(id > 0 ? $"{NameOf(id, kind)} (#{id})\n点击从列表选择" : "点击从列表选择，或右侧直接输入 ID");

        ImGui.SameLine(0f, 6f);
        ImGui.SetNextItemWidth(Math.Max(40f, avail - pick - 6f - 90f));
        if (ImGui.InputInt(label, ref id, 0, 0))
        {
            id = Math.Max(0, id);
            changed = true;
        }

        if (ImGui.BeginPopup("##grid"))
        {
            ImGui.SetNextItemWidth(280f);
            ImGui.InputTextWithHint("##search", "搜索名称 / ID...", ref _search, (UIntPtr)64);
            ImGui.SameLine();
            if (ImGui.SmallButton("清空"))
            {
                id = 0;
                changed = true;
                ImGui.CloseCurrentPopup();
            }

            Refresh(kind);
            ImGui.BeginChild("##grid-view", new Vector2(360f, 280f), ImGuiChildFlags.Borders);
            DrawGrid(kind, ref id, ref changed);
            ImGui.EndChild();
            ImGui.EndPopup();
        }

        ImGui.PopID();
        return changed;
    }

    private static void DrawGrid(Kind kind, ref int id, ref bool changed)
    {
        float avail = ImGui.GetContentRegionAvail().X;
        int cols = Math.Max(1, (int)(avail / Cell));
        int rows = (Hits.Count + cols - 1) / cols;
        float height = ImGui.GetWindowHeight();
        int first = Math.Max(0, (int)(ImGui.GetScrollY() / Cell));
        int last = Math.Min(rows, first + (int)(height / Cell) + 2);
        if (first > 0)
            ImGui.Dummy(new Vector2(1f, first * Cell));

        var dl = ImGui.GetWindowDrawList();
        for (int row = first; row < last; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                int i = row * cols + col;
                if (i >= Hits.Count)
                    break;
                if (col > 0)
                    ImGui.SameLine(0f, 0f);
                int hit = Hits[i];
                ImGui.PushID(hit);
                if (ImGui.InvisibleButton("##cell", new Vector2(Cell, Cell)))
                {
                    id = hit;
                    changed = true;
                    ImGui.CloseCurrentPopup();
                }

                var min = ImGui.GetItemRectMin();
                var max = ImGui.GetItemRectMax();
                bool hover = ImGui.IsItemHovered();
                Ui.DrawPixelSlot(dl, min + new Vector2(1f, 1f), max - new Vector2(1f, 1f), hover, hit == id);
                DrawIcon(dl, (min + max) * 0.5f, hit, kind, 30f);
                if (hover)
                    ImGui.SetTooltip($"{NameOf(hit, kind)} (#{hit})");
                ImGui.PopID();
            }
        }

        if (last < rows)
            ImGui.Dummy(new Vector2(1f, (rows - last) * Cell));
    }

    private static void Refresh(Kind kind)
    {
        string key = $"{kind}\n{_search}";
        if (key == _lastKey && kind == _searchKind)
            return;
        _lastKey = key;
        _searchKind = kind;
        Hits.Clear();
        string q = _search.Trim();
        if (kind == Kind.Ammo)
        {
            foreach (int ammo in AmmoClasses())
            {
                if (Matches(ammo, NameOf(ammo, kind), q))
                    Hits.Add(ammo);
            }
            return;
        }

        for (int i = 1; i < ProjectileID.Count; i++)
        {
            if (Matches(i, NameOf(i, kind), q))
                Hits.Add(i);
        }
    }

    private static int[] AmmoClasses()
    {
        if (_ammoClasses is not null)
            return _ammoClasses;
        var set = new SortedSet<int>();
        foreach (int known in KnownAmmo)
            set.Add(known);
        var probe = new Item();
        for (int i = 1; i < ItemID.Count; i++)
        {
            probe.SetDefaults(i);
            if (probe.ammo > 0)
                set.Add(probe.ammo);
        }

        _ammoClasses = [.. set];
        return _ammoClasses;
    }

    private static readonly int[] KnownAmmo =
    [
        AmmoID.Gel, AmmoID.Arrow, AmmoID.Coin, AmmoID.FallenStar, AmmoID.Bullet, AmmoID.Sand,
        AmmoID.Dart, AmmoID.Rocket, AmmoID.Solution, AmmoID.Flare, AmmoID.Snowball, AmmoID.StyngerBolt,
        AmmoID.CandyCorn, AmmoID.JackOLantern, AmmoID.Stake, AmmoID.NailFriendly, AmmoID.Acorn,
    ];

    private static bool Matches(int id, string name, string q)
    {
        if (q.Length == 0)
            return true;
        if (name.IndexOf(q, StringComparison.OrdinalIgnoreCase) >= 0)
            return true;
        return id.ToString().Contains(q);
    }

    private static string NameOf(int id, Kind kind)
    {
        if (id <= 0)
            return "无";
        if (kind == Kind.Ammo)
        {
            string item = Lang.GetItemNameValue(id);
            return item.Length > 0 ? item : $"弹药 #{id}";
        }

        string proj = Lang.GetProjectileName(id).Value;
        return proj.Length > 0 ? proj : $"弹幕 #{id}";
    }

    private static void DrawIcon(ImDrawListPtr dl, Vector2 center, int id, Kind kind, float size)
    {
        if (id <= 0)
            return;
        if (kind == Kind.Ammo)
            UiIcons.DrawItemDirect(dl, center, id, size);
        else
            UiIcons.DrawProjectileDirect(dl, center, id, size);
    }
}
