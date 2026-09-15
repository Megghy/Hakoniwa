using System;
using System.Collections.Generic;
using Hakoniwa.UI.Windows;
using ImGuiNET;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public sealed class RadialLauncher
{
    private const float HubRadius = 26f;
    private const float PetalRadius = 20f;
    private const float RingRadius = 92f;
    private const float DragSlop = 5f;

    private static readonly uint HubFill = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.22f, 0.14f, 0.36f, 0.94f));
    private static readonly uint HubFillHot = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.48f, 0.32f, 0.75f, 0.96f));
    private static readonly uint HubRing = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.85f, 0.65f, 1f, 0.95f));
    private static readonly uint PetalFill = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.14f, 0.12f, 0.20f, 0.94f));
    private static readonly uint PetalFillOn = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.38f, 0.26f, 0.58f, 0.96f));
    private static readonly uint PetalFillHot = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.55f, 0.38f, 0.82f, 1f));
    private static readonly uint Ink = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.95f, 0.95f, 0.98f, 1f));
    private static readonly uint Spoke = ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.85f, 0.65f, 1f, 0.35f));

    private Num.Vector2 _pos = new(56f, 56f);
    private bool _open;
    private bool _dragged;
    private float _expand;

    public void Draw(IReadOnlyList<IWindow> windows)
    {
        var io = ImGui.GetIO();
        _expand += ((_open ? 1f : 0f) - _expand) * Math.Min(1f, io.DeltaTime * 14f);
        if (!_open && _expand < 0.02f)
            _expand = 0f;
        Clamp(io.DisplaySize);

        if (_expand > 0f)
        {
            for (int i = 0; i < windows.Count; i++)
                DrawPetal(windows, i);
        }

        DrawHub(io.DisplaySize);
    }

    private void DrawHub(Num.Vector2 display)
    {
        if (!HitWindow("##fab-hub", _pos, HubRadius, out bool hovered, out bool active))
            return;

        if (active && ImGui.IsMouseDragging(ImGuiMouseButton.Left, DragSlop))
        {
            _pos += ImGui.GetIO().MouseDelta;
            _dragged = true;
            Clamp(display);
        }

        if (ImGui.IsItemDeactivated())
        {
            if (!_dragged)
                _open = !_open;
            _dragged = false;
        }

        var draw = ImGui.GetForegroundDrawList();
        uint fill = hovered || _open ? HubFillHot : HubFill;
        draw.AddCircleFilled(_pos, HubRadius, fill, 32);
        draw.AddCircle(_pos, HubRadius - 1.5f, HubRing, 32, 1.6f);
        CenterText(draw, _pos, _open ? "x" : "H", Ink);
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private void DrawPetal(IReadOnlyList<IWindow> windows, int index)
    {
        var window = windows[index];
        var center = PetalPos(index, windows.Count);
        if (!HitWindow($"##fab-petal-{index}", center, PetalRadius, out bool hovered, out _))
            return;

        if (ImGui.IsItemClicked())
        {
            window.IsOpen = !window.IsOpen;
            if (window.IsOpen)
                HakoniwaUi.Visible = true;
        }

        var draw = ImGui.GetForegroundDrawList();
        draw.AddLine(_pos, center, Spoke, 1.4f);
        uint fill = window.IsOpen ? PetalFillOn : PetalFill;
        if (hovered)
            fill = PetalFillHot;
        draw.AddCircleFilled(center, PetalRadius, fill, 28);
        draw.AddCircle(center, PetalRadius - 1f, HubRing, 28, 1.2f);
        string mark = window.Label.Length == 0 ? "?" : window.Label.Substring(0, 1);
        CenterText(draw, center, mark, Ink);
        var caption = ImGui.CalcTextSize(window.Label);
        draw.AddText(new Num.Vector2(center.X - caption.X * 0.5f, center.Y + PetalRadius + 3f), Ink, window.Label);
        ImGui.End();
        ImGui.PopStyleVar(2);
    }

    private Num.Vector2 PetalPos(int index, int count)
    {
        double angle = -Math.PI / 2 + index * (Math.PI * 2 / count);
        float reach = RingRadius * _expand;
        return _pos + new Num.Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * reach;
    }

    private void Clamp(Num.Vector2 display)
    {
        float pad = HubRadius + RingRadius * _expand + PetalRadius;
        _pos.X = Math.Min(Math.Max(_pos.X, pad), Math.Max(pad, display.X - pad));
        _pos.Y = Math.Min(Math.Max(_pos.Y, pad), Math.Max(pad, display.Y - pad));
    }

    private static bool HitWindow(string id, Num.Vector2 center, float radius, out bool hovered, out bool active)
    {
        var size = new Num.Vector2(radius * 2f, radius * 2f);
        ImGui.SetNextWindowPos(center - size * 0.5f);
        ImGui.SetNextWindowSize(size);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Num.Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        if (!ImGui.Begin(id, ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoScrollWithMouse | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoBackground | ImGuiWindowFlags.NoCollapse | ImGuiWindowFlags.NoNav))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            hovered = false;
            active = false;
            return false;
        }

        ImGui.InvisibleButton("hit", size);
        hovered = ImGui.IsItemHovered();
        active = ImGui.IsItemActive();
        return true;
    }

    private static void CenterText(ImDrawListPtr draw, Num.Vector2 center, string text, uint color)
    {
        var size = ImGui.CalcTextSize(text);
        draw.AddText(center - size * 0.5f, color, text);
    }
}
