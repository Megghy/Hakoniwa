using System;
using System.Collections.Generic;
using System.IO;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.IO;
using Hexa.NET.ImGui;
using Vector2 = System.Numerics.Vector2;

namespace Hakoniwa.UI.Windows;

internal sealed class SchematicLibrary
{
    private const string DragType = "HAKO_SCHEM";
    private const float Thumb = 52f;

    private readonly List<Entry> _items = [];
    private readonly List<string> _cats = [];
    private string? _cat;
    private int _selected = -1;
    private string _filter = "";
    private string _saveName = "";
    private string _rename = "";
    private string _newCat = "";
    private string _importPath = "";

    public void Refresh(bool notify = false)
    {
        string? keep = _selected >= 0 && _selected < _items.Count ? _items[_selected].Path : null;
        _items.Clear();
        _cats.Clear();
        string root = LibraryDir();
        foreach (string dir in Directory.GetDirectories(root, "*", SearchOption.AllDirectories))
        {
            string rel = RelativeTo(root, dir);
            if (rel.Length > 0)
                _cats.Add(rel);
        }

        _cats.Sort(StringComparer.CurrentCultureIgnoreCase);
        foreach (string file in Directory.GetFiles(root, "*.schem", SearchOption.AllDirectories))
        {
            try
            {
                _items.Add(new Entry(SchematicSerializer.Load(file), file, RelativeTo(root, Path.GetDirectoryName(file)!)));
            }
            catch (Exception ex)
            {
                Notices.Post($"跳过蓝图 {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        _selected = keep is null ? -1 : _items.FindIndex(e => e.Path.Equals(keep, StringComparison.OrdinalIgnoreCase));
        SyncRename();
        if (notify)
            Notices.Post($"蓝图库 {_items.Count} 个");
    }

    public void SaveFromSelection()
    {
        var schem = EditorSession.CaptureSelection();
        if (schem is null)
        {
            Notices.Post("没有选区");
            return;
        }

        string cat = _cat ?? "";
        string name = _saveName.Trim();
        schem.Name = UniqueName(name.Length > 0 ? name : $"选区 {schem.Width}x{schem.Height}", cat);
        Add(schem, cat);
        Notices.Post($"已存为蓝图 {schem.Name}");
    }

    public void Draw()
    {
        Ui.BeginScroll("schem-scroll");
        DrawSaveBar();
        DrawToolbar();
        DrawSplit();
        DrawRename();
        DrawImport();
        ImGui.EndChild();
    }

    private void DrawSaveBar()
    {
        Ui.Heading(Icons.Script, "蓝图库");
        ImGui.SetNextItemWidth(240f);
        ImGui.InputTextWithHint("##saveName", "蓝图名称（可空，自动命名）", ref _saveName, (UIntPtr)64);
        ImGui.SameLine(0f, 6f);
        if (ImGui.Button("从选区保存", new Vector2(120f, 24f)))
            SaveFromSelection();
    }

    private void DrawToolbar()
    {
        if (ImGui.Button("刷新", new Vector2(72f, 24f)))
            Refresh(true);
        ImGui.SameLine(0f, 6f);
        ImGui.SetNextItemWidth(140f);
        ImGui.InputTextWithHint("##newCat", "新分类名", ref _newCat, (UIntPtr)48);
        ImGui.SameLine(0f, 6f);
        if (ImGui.Button("新建分类", new Vector2(88f, 24f)))
            CreateCategory();
        ImGui.SameLine(0f, 6f);
        ImGui.SetNextItemWidth(160f);
        ImGui.InputTextWithHint("##schemFilter", "过滤蓝图...", ref _filter, (UIntPtr)128);
    }

    private void DrawSplit()
    {
        float height = Math.Max(180f, ImGui.GetContentRegionAvail().Y - 88f);
        ImGui.BeginChild("schem-cats", new Vector2(150f, height), ImGuiChildFlags.Borders);
        DrawCats();
        ImGui.EndChild();
        ImGui.SameLine(0f, 6f);
        ImGui.BeginChild("schem-list", new Vector2(0f, height), ImGuiChildFlags.Borders);
        DrawList();
        ImGui.EndChild();
    }

    private void DrawCats()
    {
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        if (ImGui.Selectable("全部", _cat is null))
            _cat = null;
        if (ImGui.Selectable("未分类", _cat == ""))
            _cat = "";
        AcceptDrop("");
        for (int i = 0; i < _cats.Count; i++)
        {
            string cat = _cats[i];
            ImGui.PushID(i);
            if (ImGui.Selectable(cat, _cat == cat))
                _cat = cat;
            AcceptDrop(cat);
            ImGui.PopID();
        }
    }

    private void DrawList()
    {
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        int shown = 0;
        for (int i = 0; i < _items.Count; i++)
        {
            var entry = _items[i];
            if (_cat is not null && entry.Category != _cat)
                continue;
            if (_filter.Length > 0 && entry.Schem.Name.IndexOf(_filter, StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            shown++;
            DrawRow(dl, i, entry);
        }

        if (shown == 0)
            ImGui.TextDisabled(_items.Count == 0 ? "暂无蓝图。从选区保存，或刷新扫描文件夹。" : "该分类没有匹配的蓝图。");
    }

    private unsafe void DrawRow(ImDrawListPtr dl, int index, Entry entry)
    {
        ImGui.PushID(index);
        bool selected = _selected == index;
        ImGui.Selectable("##schemRow", selected, ImGuiSelectableFlags.None, new Vector2(0, Thumb));
        if (ImGui.BeginDragDropSource())
        {
            int payload = index;
            ImGui.SetDragDropPayload(DragType, &payload, (UIntPtr)sizeof(int));
            ImGui.TextUnformatted(entry.Schem.Name);
            ImGui.EndDragDropSource();
        }

        if (ImGui.IsItemHovered() && ImGui.IsMouseReleased(ImGuiMouseButton.Left) && !ImGui.IsMouseDragging(ImGuiMouseButton.Left))
        {
            _selected = index;
            SyncRename();
            EditorSession.Clipboard = entry.Schem.Clone();
            EditorSession.BeginPaste();
        }

        var min = ImGui.GetItemRectMin();
        if (ImGui.IsItemVisible())
            SchematicDrawer.DrawFit(dl, entry.Schem, min + new Vector2(6f, 2f), new Vector2(Thumb, Thumb));
        uint title = selected ? ImGui.ColorConvertFloat4ToU32(Ui.Gold) : 0xFFE2E8F0;
        dl.AddText(min + new Vector2(Thumb + 14f, 8f), title, entry.Schem.Name);
        string meta = $"{entry.Schem.Width} x {entry.Schem.Height}";
        if (_cat is null)
            meta += $"  {(entry.Category.Length == 0 ? "未分类" : entry.Category)}";
        dl.AddText(min + new Vector2(Thumb + 14f, 28f), 0xFF8A8794, meta);
        ImGui.PopID();
    }

    private void DrawRename()
    {
        if (_selected < 0 || _selected >= _items.Count)
            return;
        ImGui.SetNextItemWidth(240f);
        bool enter = ImGui.InputText("名称", ref _rename, (UIntPtr)64, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGui.SameLine(0f, 6f);
        if ((enter || ImGui.Button("重命名", new Vector2(72f, 24f))) && _rename.Trim().Length > 0)
            Rename(_items[_selected], _rename.Trim());
    }

    private void DrawImport()
    {
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 160f);
        ImGui.InputTextWithHint("##import", "导入 / 导出路径", ref _importPath, (UIntPtr)512);
        ImGui.SameLine(0f, 6f);
        if (ImGui.Button("导入", new Vector2(72f, 24f)) && _importPath.Length > 0)
            Import(_importPath);
        ImGui.SameLine(0f, 6f);
        if (ImGui.Button("导出", new Vector2(72f, 24f)) && _importPath.Length > 0 && _selected >= 0)
        {
            SchematicSerializer.Save(_items[_selected].Schem, _importPath);
            Notices.Post("蓝图已导出");
        }
    }

    private unsafe void AcceptDrop(string category)
    {
        if (!ImGui.BeginDragDropTarget())
            return;
        var payload = ImGui.AcceptDragDropPayload(DragType);
        if (!payload.IsNull && payload.DataSize >= sizeof(int))
            Move(*(int*)payload.Data, category);
        ImGui.EndDragDropTarget();
    }

    private void CreateCategory()
    {
        string name = SanitizeFolder(_newCat);
        if (name.Length == 0)
        {
            Notices.Post("分类名无效");
            return;
        }

        Directory.CreateDirectory(Path.Combine(LibraryDir(), name));
        _newCat = "";
        _cat = name;
        Refresh();
        Notices.Post($"已创建分类 {name}");
    }

    private void Add(Schematic schem, string category)
    {
        string path = UniqueFile(CatDir(category), Sanitize(schem.Name) + ".schem");
        SchematicSerializer.Save(schem, path);
        _items.Add(new Entry(schem, path, category));
        _selected = _items.Count - 1;
        SyncRename();
    }

    private void Import(string path)
    {
        try
        {
            var imported = SchematicSerializer.Load(path);
            if (string.IsNullOrWhiteSpace(imported.Name) || imported.Name == "Untitled")
                imported.Name = Path.GetFileNameWithoutExtension(path);
            string cat = _cat ?? "";
            imported.Name = UniqueName(imported.Name, cat);
            Add(imported, cat);
            Notices.Post("蓝图已导入");
        }
        catch (Exception ex)
        {
            Notices.Post($"导入失败: {ex.Message}");
        }
    }

    private void Rename(Entry entry, string name)
    {
        string cat = entry.Category;
        if (NameTaken(name, cat, entry.Path))
            name = UniqueName(name, cat);
        entry.Schem.Name = name;
        string dest = UniqueFile(CatDir(cat), Sanitize(name) + ".schem", entry.Path);
        SchematicSerializer.Save(entry.Schem, dest);
        if (!dest.Equals(entry.Path, StringComparison.OrdinalIgnoreCase) && File.Exists(entry.Path))
            File.Delete(entry.Path);
        entry.Path = dest;
        _rename = name;
        Notices.Post($"已重命名为 {name}");
    }

    private void Move(int index, string category)
    {
        if ((uint)index >= (uint)_items.Count)
            return;
        var entry = _items[index];
        if (entry.Category == category)
            return;
        string dest = UniqueFile(CatDir(category), Path.GetFileName(entry.Path));
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        File.Move(entry.Path, dest);
        entry.Path = dest;
        entry.Category = category;
        Notices.Post($"已移到 {(category.Length == 0 ? "未分类" : category)}");
    }

    private void SyncRename() =>
        _rename = _selected >= 0 && _selected < _items.Count ? _items[_selected].Schem.Name : "";

    private string UniqueName(string baseName, string category)
    {
        if (!NameTaken(baseName, category))
            return baseName;
        for (int n = 2; ; n++)
        {
            string name = $"{baseName} ({n})";
            if (!NameTaken(name, category))
                return name;
        }
    }

    private bool NameTaken(string name, string category, string? exceptPath = null)
    {
        for (int i = 0; i < _items.Count; i++)
        {
            var e = _items[i];
            if (e.Category == category && e.Schem.Name == name &&
                (exceptPath is null || !e.Path.Equals(exceptPath, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        return false;
    }

    private static string UniqueFile(string dir, string fileName, string? exceptPath = null)
    {
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir, fileName);
        if (!File.Exists(path) || path.Equals(exceptPath, StringComparison.OrdinalIgnoreCase))
            return path;
        string stem = Path.GetFileNameWithoutExtension(fileName);
        string ext = Path.GetExtension(fileName);
        for (int n = 2; ; n++)
        {
            path = Path.Combine(dir, $"{stem}-{n}{ext}");
            if (!File.Exists(path) || path.Equals(exceptPath, StringComparison.OrdinalIgnoreCase))
                return path;
        }
    }

    private static string CatDir(string category) =>
        category.Length == 0 ? LibraryDir() : Path.Combine(LibraryDir(), category.Replace('/', Path.DirectorySeparatorChar));

    private static string LibraryDir()
    {
        string dir = Path.Combine(Terraria.Program.SavePath, "Hakoniwa", "Schematics");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string RelativeTo(string root, string path)
    {
        root = Path.GetFullPath(root);
        path = Path.GetFullPath(path);
        if (root[root.Length - 1] != Path.DirectorySeparatorChar)
            root += Path.DirectorySeparatorChar;
        if (!path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            return "";
        return path.Substring(root.Length).Replace(Path.DirectorySeparatorChar, '/').TrimEnd('/');
    }

    private static string Sanitize(string name)
    {
        char[] invalid = Path.GetInvalidFileNameChars();
        char[] chars = name.ToCharArray();
        for (int i = 0; i < chars.Length; i++)
        {
            if (Array.IndexOf(invalid, chars[i]) >= 0)
                chars[i] = '_';
        }

        string s = new string(chars).Trim();
        return s.Length == 0 ? "schematic" : s;
    }

    private static string SanitizeFolder(string name)
    {
        name = name.Trim();
        if (name.Length == 0 || name.IndexOfAny(['/', '\\']) >= 0)
            return "";
        string s = Sanitize(name);
        return s is "." or ".." ? "" : s;
    }

    private sealed class Entry(Schematic schem, string path, string category)
    {
        public Schematic Schem = schem;
        public string Path = path;
        public string Category = category;
    }
}
