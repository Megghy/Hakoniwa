using System;
using System.Collections.Generic;
using System.Numerics;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.UI.Windows;
using ImGuiNET;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace Hakoniwa.UI;

/// <summary>
/// 现代化圆形悬浮球 (支持像素图标、拖拽、靠边吸附、环绕展开与子按钮快捷控制)
/// </summary>
public sealed class FloatingBall
{
    private const float MainRadius = 24f;
    private const float SubRadius = 20f;
    private const float OrbitRadius = 68f;
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

    private record struct BallAction(string Label, string Hint, int IconItemId, Func<bool> IsActive, Action OnClick);

    private readonly List<BallAction> _actions = [];

    public FloatingBall()
    {
        _actions.Add(new("工坊", "箱庭工坊主工作台", ItemID.ArchitectGizmoPack, () => HakoniwaUi.StudioIsOpen, () => HakoniwaUi.StudioIsOpen = !HakoniwaUi.StudioIsOpen));
        _actions.Add(new("上帝", "上帝模式 [无敌]", ItemID.CobaltShield, () => CheatState.GodMode, () => CheatState.GodMode = !CheatState.GodMode));
        _actions.Add(new("全亮", "全图照明 [夜视]", ItemID.NightVisionHelmet, () => CheatState.FullBright, () => CheatState.FullBright = !CheatState.FullBright));
        _actions.Add(new("无限", "无限放置与触及", ItemID.BottomlessBucket, () => CheatState.InfiniteReach && CheatState.InfiniteItems, () =>
        {
            bool toggle = !(CheatState.InfiniteReach && CheatState.InfiniteItems);
            CheatState.InfiniteReach = toggle;
            CheatState.InfiniteItems = toggle;
        }));
        _actions.Add(new("选区", "笔刷/选区工具切换", ItemID.LaserRuler, () => EditorSession.SelectedTool == 3, () => EditorSession.SelectedTool = EditorSession.SelectedTool == 3 ? 0 : 3));
        _actions.Add(new("撤销", "撤销上一步操作", ItemID.MagicMirror, () => false, () => EditorSession.Undo()));
        _actions.Add(new("重做", "重做操作", ItemID.RecallPotion, () => false, () => EditorSession.Redo()));
    }

    public void Draw()
    {
        var io = ImGui.GetIO();
        var screenSize = io.DisplaySize;
        if (screenSize.X < 1f || screenSize.Y < 1f)
            return;

        if (!_initialized)
        {
            _pos = new Vector2(EdgeMargin, screenSize.Y * 0.35f);
            _targetPos = _pos;
            _initialized = true;
        }

        _expandAnim = MathHelper.Lerp(_expandAnim, _isExpanded ? 1f : 0f, 0.22f);
        if (!_isDragging)
        {
            _pos.X = MathHelper.Lerp(_pos.X, _targetPos.X, 0.25f);
            _pos.Y = MathHelper.Clamp(_pos.Y, EdgeMargin, screenSize.Y - EdgeMargin);
        }

        var mousePos = io.MousePos;
        bool isMouseDown = io.MouseDown[0];
        bool isMouseReleased = io.MouseReleased[0];

        bool isHoveringMain = Vector2.Distance(mousePos, _pos) <= MainRadius;
        bool isHoveringAny = isHoveringMain;
        string? hoveredHint = null;
        Vector2 hoveredSubCenter = Vector2.Zero;

        float cover = MainRadius + 6f;
        if (_expandAnim > 0.02f)
            cover = OrbitRadius + SubRadius + 10f;

        ImGui.SetNextWindowPos(_pos - new Vector2(cover, cover), ImGuiCond.Always);
        ImGui.SetNextWindowSize(new Vector2(cover * 2f, cover * 2f));
        ImGui.SetNextWindowBgAlpha(0f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, Vector2.Zero);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 0f);
        var flags = ImGuiWindowFlags.NoTitleBar |
                    ImGuiWindowFlags.NoResize |
                    ImGuiWindowFlags.NoMove |
                    ImGuiWindowFlags.NoScrollbar |
                    ImGuiWindowFlags.NoCollapse |
                    ImGuiWindowFlags.NoSavedSettings |
                    ImGuiWindowFlags.NoNav |
                    ImGuiWindowFlags.NoBackground |
                    ImGuiWindowFlags.NoDocking;
        if (!ImGui.Begin("##HakoniwaFloatingBall", flags))
        {
            ImGui.End();
            ImGui.PopStyleVar(2);
            return;
        }

        var drawList = ImGui.GetWindowDrawList();

        // 拖拽处理
        if (isMouseDown && !_isDragging && isHoveringMain)
        {
            _isDragging = true;
            _hasDragged = false;
            _dragStartMouse = mousePos;
            _dragOffset = mousePos - _pos;
        }

        if (_isDragging)
        {
            if (isMouseDown)
            {
                if (Vector2.Distance(mousePos, _dragStartMouse) > 5f)
                    _hasDragged = true;

                _pos = mousePos - _dragOffset;
                _pos.X = MathHelper.Clamp(_pos.X, EdgeMargin, screenSize.X - EdgeMargin);
                _pos.Y = MathHelper.Clamp(_pos.Y, EdgeMargin, screenSize.Y - EdgeMargin);
            }
            else if (isMouseReleased)
            {
                _isDragging = false;
                if (_hasDragged)
                {
                    _targetPos.X = (_pos.X < screenSize.X * 0.5f) ? EdgeMargin : screenSize.X - EdgeMargin;
                    _targetPos.Y = _pos.Y;
                }
                else
                {
                    _isExpanded = !_isExpanded;
                    SoundEngine.PlaySound(12);
                }
            }
        }

        // 绘制环绕子按钮
        if (_expandAnim > 0.02f)
        {
            float centerAngle;
            float totalSpan;

            if (_pos.X < screenSize.X * 0.35f)
            {
                centerAngle = 0f;
                totalSpan = (float)(Math.PI * 0.85);
            }
            else if (_pos.X > screenSize.X * 0.65f)
            {
                centerAngle = (float)Math.PI;
                totalSpan = (float)(Math.PI * 0.85);
            }
            else
            {
                centerAngle = -(float)(Math.PI * 0.5);
                totalSpan = (float)(Math.PI * 2.0);
            }

            int count = _actions.Count;
            float startAngle = centerAngle - totalSpan * 0.5f;
            float step = totalSpan / (count > 1 ? count - 1 : 1);
            if (totalSpan >= (float)(Math.PI * 1.9))
                step = totalSpan / count;

            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + step * i;
                var subCenter = _pos + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * (OrbitRadius * _expandAnim);

                bool isSubHovered = Vector2.Distance(mousePos, subCenter) <= SubRadius;
                if (isSubHovered)
                {
                    isHoveringAny = true;
                    hoveredHint = _actions[i].Hint;
                    hoveredSubCenter = subCenter;
                }

                if (isSubHovered && isMouseReleased && !_isDragging)
                {
                    _actions[i].OnClick();
                    SoundEngine.PlaySound(12);
                }

                DrawSubButton(drawList, subCenter, _actions[i], isSubHovered, _expandAnim);
            }
        }

        DrawMainButton(drawList, _pos, isHoveringMain || _isDragging, _isExpanded);

        if (!Main.gameMenu && Main.LocalPlayer.active && (isHoveringAny || _isDragging))
            Main.LocalPlayer.mouseInterface = true;

        ImGui.End();
        ImGui.PopStyleVar(2);

        if (hoveredHint is not null && !_isDragging)
            DrawTooltip(screenSize, hoveredSubCenter, hoveredHint);
    }

    private static void DrawMainButton(ImDrawListPtr drawList, Vector2 center, bool isHovered, bool isExpanded)
    {
        uint bgColor = isHovered ? 0xF02A2440 : 0xE01A162B;
        uint borderColor = isExpanded ? 0xFF38BDF8 : (isHovered ? 0xFF8B5CF6 : 0xFF6D28D9);

        // 外层微阴影与圆形
        drawList.AddCircleFilled(center, MainRadius + 2f, 0x60000000);
        drawList.AddCircleFilled(center, MainRadius, bgColor);
        drawList.AddCircle(center, MainRadius, borderColor, 32, 2.0f);

        // 绘制主像素图标
        UiIcons.DrawItemDirect(drawList, center, ItemID.ArchitectGizmoPack, 28f);
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

        // 阴影与圆形背景
        drawList.AddCircleFilled(center, SubRadius + 2f, (uint)(((byte)(anim * 80)) << 24));
        drawList.AddCircleFilled(center, SubRadius, bgColor);
        drawList.AddCircle(center, SubRadius, borderColor, 24, isActive ? 2.0f : 1.5f);

        // 绘制子功能的原版像素图标
        UiIcons.DrawItemDirect(drawList, center, action.IconItemId, 22f, alpha);
    }

    private static void DrawTooltip(Vector2 screenSize, Vector2 anchor, string text)
    {
        var textSize = ImGui.CalcTextSize(text);
        var padding = new Vector2(8f, 4f);
        var boxSize = textSize + padding * 2f;
        var boxPos = new Vector2(anchor.X - boxSize.X * 0.5f, anchor.Y - SubRadius - boxSize.Y - 6f);
        boxPos.X = MathHelper.Clamp(boxPos.X, 4f, screenSize.X - boxSize.X - 4f);
        boxPos.Y = MathHelper.Clamp(boxPos.Y, 4f, screenSize.Y - boxSize.Y - 4f);

        ImGui.SetNextWindowPos(boxPos);
        ImGui.SetNextWindowSize(boxSize);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowPadding, padding);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowRounding, 4f);
        ImGui.PushStyleVar(ImGuiStyleVar.WindowBorderSize, 1f);
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.13f, 0.11f, 0.09f, 0.93f));
        ImGui.PushStyleColor(ImGuiCol.Border, new Vector4(0.22f, 0.74f, 0.97f, 1f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.99f, 0.96f, 0.94f, 1f));
        const ImGuiWindowFlags flags = ImGuiWindowFlags.NoTitleBar | ImGuiWindowFlags.NoResize | ImGuiWindowFlags.NoMove |
                                       ImGuiWindowFlags.NoScrollbar | ImGuiWindowFlags.NoSavedSettings | ImGuiWindowFlags.NoNav |
                                       ImGuiWindowFlags.NoFocusOnAppearing | ImGuiWindowFlags.NoInputs;
        if (ImGui.Begin("##HakoniwaBallTip", flags))
            ImGui.TextUnformatted(text);
        ImGui.End();
        ImGui.PopStyleColor(3);
        ImGui.PopStyleVar(3);
    }

    private static class MathHelper
    {
        public static float Lerp(float value1, float value2, float amount) => value1 + (value2 - value1) * amount;
        public static float Clamp(float value, float min, float max) => Math.Max(min, Math.Min(max, value));
    }
}
