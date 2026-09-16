using System.Collections.Generic;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public static class NotifyHost
{
    private const float Life = 3.2f;
    private const float Fade = 0.5f;
    private const int Max = 5;

    private struct Toast
    {
        public string Text;
        public float Born;
    }

    private static readonly List<Toast> _items = [];
    private static bool _god, _reach, _itemsOn, _free, _bright, _noclip, _freeze, _teleport, _unlockFps, _armed;

    public static void Enqueue(string text)
    {
        _items.Add(new Toast { Text = text, Born = Now() });
        while (_items.Count > Max)
            _items.RemoveAt(0);
    }

    public static void WatchToggles()
    {
        Check(ref _god, CheatState.GodMode, "上帝模式");
        Check(ref _reach, CheatState.InfiniteReach, "无限范围");
        Check(ref _itemsOn, CheatState.InfiniteItems, "无限物品");
        Check(ref _free, CheatState.FreePlacement, "悬空放置");
        Check(ref _bright, CheatState.FullBright, "全图照明");
        Check(ref _noclip, CheatState.NoClip, "穿墙模式");
        Check(ref _freeze, CheatState.FreezeTime, "锁定时间");
        Check(ref _teleport, CheatState.ClickTeleport, "快捷传送");
        Check(ref _unlockFps, CheatState.UnlockFps, "帧率跟随屏幕刷新率");
        _armed = true;
    }

    public static void Draw()
    {
        float now = Now();
        for (int i = _items.Count - 1; i >= 0; i--)
        {
            if (now - _items[i].Born >= Life)
                _items.RemoveAt(i);
        }

        if (_items.Count == 0)
            return;

        float scale = CheatState.NotifyScale;
        if (scale < 0.6f) scale = 0.6f;
        if (scale > 1.8f) scale = 1.8f;
        float width = 252f * scale;
        const float pad = 14f;
        var display = ImGui.GetIO().DisplaySize;
        bool right = CheatState.NotifyAnchor is NotifyCorner.右上 or NotifyCorner.右下;
        bool bottom = CheatState.NotifyAnchor is NotifyCorner.左下 or NotifyCorner.右下;
        float x = right ? display.X - width - pad : pad;
        float y = bottom ? display.Y - pad : pad;

        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, new Num.Vector2(10f, 8f) * scale);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Num.Vector4(0.13f, 0.11f, 0.09f, 0.94f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Num.Vector4(0.22f, 0.74f, 0.97f, 0.95f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Num.Vector4(0.99f, 0.96f, 0.94f, 1f));
        const ImGuiWindowFlags flags = Ui.Toast;

        for (int i = 0; i < _items.Count; i++)
        {
            var toast = _items[i];
            float age = now - toast.Born;
            float alpha = age > Life - Fade ? (Life - age) / Fade : 1f;
            ImGui.PushStyleVar(ImGuiStyleVar.Alpha, alpha);
            ImGui.SetNextWindowPos(new Num.Vector2(x, y), ImGuiCond.Always, new Num.Vector2(0f, bottom ? 1f : 0f));
            ImGui.SetNextWindowSizeConstraints(new Num.Vector2(width, 0f), new Num.Vector2(width, 240f * scale));
            if (ImGui.Begin($"##HakoniwaToast{i}", flags))
            {
                ImGui.PushFont(ImGui.GetFont(), 16f * scale);
                ImGui.PushTextWrapPos(width - 20f * scale);
                ImGui.TextUnformatted(toast.Text);
                ImGui.PopTextWrapPos();
                ImGui.PopFont();
                float height = ImGui.GetWindowSize().Y;
                y += (height + 6f) * (bottom ? -1f : 1f);
            }
            ImGui.End();
            ImGui.PopStyleVar();
        }

        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
    }

    private static void Check(ref bool prev, bool now, string name)
    {
        if (prev == now)
            return;
        prev = now;
        if (_armed)
            Notices.Post(now ? $"已启用 {name}" : $"已关闭 {name}");
    }

    private static float Now() => (float)Main.gameTimeCache.TotalGameTime.TotalSeconds;
}
