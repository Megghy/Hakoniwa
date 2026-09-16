using System;
using System.Collections.Generic;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Terraria;
using Vector2 = System.Numerics.Vector2;

namespace Hakoniwa.UI.Windows;

public sealed class ItemPicker
{
    private const float Cell = 38f;
    private const uint HeartOn = 0xFF5B8CFF;

    private string _search = string.Empty;
    private int _category;
    private int _sub;
    private readonly List<int> _hits = [];
    private string _last = "\0";

    public void Draw()
    {
        Ui.Heading(Icons.Box, "放入背包");
        if (Main.gameMenu || !Main.LocalPlayer.active)
        {
            ImGui.TextUnformatted("进入世界后搜索物品，左键一组、右键一个、中键收藏。");
            return;
        }

        ImGui.SetNextItemWidth(-64f);
        ImGui.InputTextWithHint("##itemSearch", "搜索名称", ref _search, (UIntPtr)128);
        ImGui.SameLine();
        ImGui.TextDisabled($"{_hits.Count}");

        int prev = _category;
        DrawChips("cat", ItemCatalog.Names.Length, ref _category, i => ItemCatalog.Names[i]);
        if (_category != prev)
            _sub = 0;
        var subs = ItemCatalog.Subs((ItemCategory)_category);
        if (subs.Length > 0)
            DrawChips("sub", subs.Length, ref _sub, i => subs[i].Name);
        Refresh();
        DrawGrid();
    }

    private static void DrawChips(string id, int count, ref int selected, Func<int, string> name)
    {
        ImGui.PushID(id);
        float avail = ImGui.GetContentRegionAvail().X;
        float gap = ImGui.GetStyle().ItemSpacing.X;
        float width = (avail - gap * (count - 1)) / count;
        for (int i = 0; i < count; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            bool on = selected == i;
            if (on)
                ImGui.PushStyleColor(ImGuiCol.Button, Ui.Accent);
            if (ImGui.Button(name(i), new Vector2(width, 22f)))
                selected = i;
            if (on)
                ImGui.PopStyleColor();
        }

        ImGui.PopID();
    }

    private void Refresh()
    {
        string key = $"{_category}\n{_sub}\n{_search}\n{CheatState.FavRev}";
        if (key == _last)
            return;
        _last = key;
        ItemCatalog.Search(_search, (ItemCategory)_category, _sub, CheatState.Favorites, _hits);
    }

    private void DrawGrid()
    {
        ImGui.BeginChild("item-grid", Vector2.Zero, ImGuiChildFlags.None);
        float avail = ImGui.GetContentRegionAvail().X;
        int cols = Math.Max(1, (int)(avail / Cell));
        int rows = (_hits.Count + cols - 1) / cols;
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
                if (i >= _hits.Count)
                    break;
                if (col > 0)
                    ImGui.SameLine(0f, 0f);
                DrawCell(dl, _hits[i]);
            }
        }

        if (last < rows)
            ImGui.Dummy(new Vector2(1f, (rows - last) * Cell));
        ImGui.EndChild();
    }

    private static void DrawCell(ImDrawListPtr dl, int id)
    {
        ImGui.PushID(id);
        ImGui.InvisibleButton("##c", new Vector2(Cell, Cell));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        bool hover = ImGui.IsItemHovered();
        bool fav = CheatState.Favorites.Contains(id);
        dl.AddRectFilled(min + new Vector2(1f, 1f), max - new Vector2(1f, 1f), hover ? Ui.ChipHover : 0xE014121C, 3f);
        dl.AddRect(min + new Vector2(1f, 1f), max - new Vector2(1f, 1f), hover ? Ui.ChipOn : Ui.ChipLine, 3f);
        UiIcons.DrawItemDirect(dl, (min + max) * 0.5f, id, 28f);

        var heart = new Vector2(max.X - 8f, min.Y + 8f);
        bool overHeart = hover && Vector2.Distance(ImGui.GetIO().MousePos, heart) < 8f;
        if (fav || hover)
            Icons.DrawDirect(dl, heart, Icons.Heart, (byte)(fav || overHeart ? 255 : 160), fav ? HeartOn : 0xFF8A8794, 12f);

        if (hover)
            ImGui.SetTooltip($"{Lang.GetItemNameValue(id)}\nID {id}\n左键一组  右键一个  中键收藏");
        if (ImGui.IsItemClicked(ImGuiMouseButton.Middle) || (ImGui.IsItemClicked(ImGuiMouseButton.Left) && overHeart))
            CheatState.ToggleFavorite(id);
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Left))
            ItemCatalog.Give(id, true);
        else if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
            ItemCatalog.Give(id, false);
        ImGui.PopID();
    }
}
