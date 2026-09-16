using System;
using System.Collections.Generic;
using System.Numerics;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.UI.Windows;
using Hexa.NET.ImGui;
using Terraria.Audio;

namespace Hakoniwa.UI;

public sealed class FloatingBall
{
    private const float MainRadius = 24f;
    private const float SubRadius = 20f;
    private const float OrbitRadius = 68f;
    private const float ButtonGap = 3f;
    private const float EdgeMargin = 28f;

    private Vector2 _pos = new(EdgeMargin, 220f);
    private Vector2 _targetPos = new(EdgeMargin, 220f);
    private bool _initialized;
    private bool _isExpanded;
    private float _expandAnim;

    private bool _isDragging;
    private Vector2 _dragOffset;
    private Vector2 _dragStartMouse;
    private bool _hasDragged;

    private record struct BallAction(string Label, string Hint, string Icon, Func<bool> IsActive, Action OnClick);

    private readonly List<BallAction> _actions = [];

    public FloatingBall()
    {
        _actions.Add(new("工坊", "箱庭工坊主工作台", Icons.Home, () => HakoniwaUi.StudioIsOpen, () => HakoniwaUi.StudioIsOpen = !HakoniwaUi.StudioIsOpen));
        _actions.Add(new("上帝", "上帝模式 [无敌]", Icons.Shield, () => CheatState.GodMode, () => CheatState.GodMode = !CheatState.GodMode));
        _actions.Add(new("全亮", "全图照明 [夜视]", Icons.Sun, () => CheatState.FullBright, () => CheatState.FullBright = !CheatState.FullBright));
        _actions.Add(new("无限", "无限放置与触及", Icons.Infinity, () => CheatState.InfiniteReach && CheatState.InfiniteItems, () =>
        {
            bool toggle = !(CheatState.InfiniteReach && CheatState.InfiniteItems);
            CheatState.InfiniteReach = toggle;
            CheatState.InfiniteItems = toggle;
        }));
        _actions.Add(new("选区", "清除当前选区", Icons.Crop, () => EditorSession.Selection.Active, () => EditorSession.Selection.Clear()));
        _actions.Add(new("撤销", "撤销上一步操作", Icons.Undo, () => false, () => EditorSession.Undo()));
        _actions.Add(new("重做", "重做操作", Icons.Redo, () => false, () => EditorSession.Redo()));
    }

    public void Draw()
    {
        var io = ImGui.GetIO();
        var screenSize = io.DisplaySize;
        if (screenSize.X < 1f || screenSize.Y < 1f)
            return;

        if (!_initialized)
        {
            bool right = CheatState.BallRight == true;
            float x = right ? screenSize.X - EdgeMargin : EdgeMargin;
            float y = CheatState.BallY ?? screenSize.Y * 0.35f;
            _pos = _targetPos = new Vector2(x, y);
            _initialized = true;
        }

        _expandAnim += ((_isExpanded ? 1f : 0f) - _expandAnim) * 0.22f;
        if (!_isDragging)
        {
            _pos.X += (_targetPos.X - _pos.X) * 0.25f;
            _pos.Y = Math.Max(EdgeMargin, Math.Min(_pos.Y, screenSize.Y - EdgeMargin));
        }

        GetFan(screenSize, out float centerAngle, out float totalSpan, out bool fullCircle);
        float cover = MainRadius + 6f;
        if (_expandAnim > 0.02f)
            cover = OrbitOf(CountRings(_actions.Count, totalSpan, fullCircle) - 1) + SubRadius + 10f;

        ImGui.SetNextWindowPos(_pos - new Vector2(cover, cover), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(cover * 2f, cover * 2f));
        ImGui.SetNextWindowBgAlpha(0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        if (!ImGui.Begin("##HakoniwaFloatingBall", Ui.Chrome))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();
        Ui.Invisible("##main", _pos - new Vector2(MainRadius), new Vector2(MainRadius * 2f));
        bool hoverMain = ImGui.IsItemHovered();
        HandleDrag(screenSize);

        string? hoveredHint = null;
        if (_expandAnim > 0.02f)
            DrawOrbit(drawList, centerAngle, totalSpan, fullCircle, ref hoveredHint);

        DrawMainButton(drawList, _pos, hoverMain || _isDragging, _isExpanded);

        ImGui.End();
        ImGui.PopStyleVar(2);

        if (hoveredHint is not null && !_isDragging)
            ImGui.SetTooltip(hoveredHint);
    }

    private void HandleDrag(Vector2 screenSize)
    {
        var mousePos = ImGui.GetIO().MousePos;
        if (ImGui.IsItemActive())
        {
            if (!_isDragging)
            {
                _isDragging = true;
                _hasDragged = false;
                _dragStartMouse = mousePos;
                _dragOffset = mousePos - _pos;
            }

            if (Vector2.Distance(mousePos, _dragStartMouse) > 5f)
                _hasDragged = true;
            if (!_hasDragged)
                return;

            _pos = mousePos - _dragOffset;
            _pos.X = Math.Max(EdgeMargin, Math.Min(_pos.X, screenSize.X - EdgeMargin));
            _pos.Y = Math.Max(EdgeMargin, Math.Min(_pos.Y, screenSize.Y - EdgeMargin));
            return;
        }

        if (!_isDragging)
            return;

        _isDragging = false;
        if (_hasDragged)
        {
            _targetPos.X = _pos.X < screenSize.X * 0.5f ? EdgeMargin : screenSize.X - EdgeMargin;
            _targetPos.Y = _pos.Y;
            CheatState.BallRight = _targetPos.X > screenSize.X * 0.5f;
            CheatState.BallY = _targetPos.Y;
            return;
        }

        _isExpanded = !_isExpanded;
        SoundEngine.PlaySound(12);
    }

    private void DrawOrbit(ImDrawListPtr drawList, float centerAngle, float totalSpan, bool fullCircle, ref string? hoveredHint)
    {
        int index = 0;
        for (int ring = 0; index < _actions.Count; ring++)
        {
            float radius = OrbitOf(ring) * _expandAnim;
            int take = Math.Min(RingCapacity(OrbitOf(ring), totalSpan, fullCircle), _actions.Count - index);
            float startAngle = centerAngle - totalSpan * 0.5f;
            float step = take <= 1 ? 0f : fullCircle ? totalSpan / take : totalSpan / (take - 1);

            for (int i = 0; i < take; i++)
            {
                float angle = take == 1 ? centerAngle : startAngle + step * i;
                var subCenter = _pos + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;
                var action = _actions[index++];
                float d = SubRadius * 2f;
                if (Ui.Invisible($"##o{index}", subCenter - new Vector2(SubRadius), new Vector2(d)) && !_isDragging)
                {
                    action.OnClick();
                    SoundEngine.PlaySound(12);
                }

                bool hover = ImGui.IsItemHovered();
                if (hover)
                    hoveredHint = action.Hint;
                DrawSubButton(drawList, subCenter, action, hover, _expandAnim);
            }
        }
    }

    private void GetFan(Vector2 screenSize, out float centerAngle, out float totalSpan, out bool fullCircle)
    {
        if (_pos.X < screenSize.X * 0.35f)
        {
            centerAngle = 0f;
            totalSpan = (float)(Math.PI * 0.85);
            fullCircle = false;
            return;
        }

        if (_pos.X > screenSize.X * 0.65f)
        {
            centerAngle = (float)Math.PI;
            totalSpan = (float)(Math.PI * 0.85);
            fullCircle = false;
            return;
        }

        centerAngle = -(float)(Math.PI * 0.5);
        totalSpan = (float)(Math.PI * 2.0);
        fullCircle = true;
    }

    private static float RingStep => SubRadius * 2f + ButtonGap;

    private static float OrbitOf(int ring) => OrbitRadius + ring * RingStep;

    private static int RingCapacity(float radius, float totalSpan, bool fullCircle)
    {
        float ratio = (SubRadius * 2f + ButtonGap) / (2f * radius);
        if (ratio >= 1f)
            return 1;

        int n = (int)Math.Floor(totalSpan / (2f * (float)Math.Asin(ratio)));
        if (!fullCircle)
            n++;
        return Math.Max(1, n);
    }

    private static int CountRings(int count, float totalSpan, bool fullCircle)
    {
        int remaining = count;
        int rings = 0;
        while (remaining > 0)
        {
            remaining -= RingCapacity(OrbitOf(rings), totalSpan, fullCircle);
            rings++;
        }

        return Math.Max(1, rings);
    }

    private static void DrawMainButton(ImDrawListPtr drawList, Vector2 center, bool isHovered, bool isExpanded)
    {
        uint bgColor = isHovered ? Ui.ChipHover : Ui.ChipIdle;
        uint borderColor = isExpanded ? Ui.ChipOn : (isHovered ? 0xFF8B5CF6 : Ui.ChipLine);

        drawList.AddCircleFilled(center, MainRadius + 2f, 0x60000000);
        drawList.AddCircleFilled(center, MainRadius, bgColor);
        drawList.AddCircle(center, MainRadius, borderColor, 32, 2.0f);
        Icons.DrawDirect(drawList, center, Icons.Home);
    }

    private static void DrawSubButton(ImDrawListPtr drawList, Vector2 center, BallAction action, bool isHovered, float anim)
    {
        bool isActive = action.IsActive();
        byte alpha = (byte)(anim * 245);

        uint bgColor = isActive
            ? (uint)((alpha << 24) | (isHovered ? 0x008E581A : 0x00704312))
            : (uint)((alpha << 24) | (isHovered ? 0x00332822 : 0x00221B18));

        uint borderColor = isActive
            ? (uint)((alpha << 24) | 0x0038BDF8)
            : (uint)((alpha << 24) | (isHovered ? 0x008B5CF6 : 0x0055443B));

        drawList.AddCircleFilled(center, SubRadius + 2f, (uint)((byte)(anim * 80) << 24));
        drawList.AddCircleFilled(center, SubRadius, bgColor);
        drawList.AddCircle(center, SubRadius, borderColor, 24, isActive ? 2.0f : 1.5f);
        Icons.DrawDirect(drawList, center, action.Icon, alpha);
    }
}
