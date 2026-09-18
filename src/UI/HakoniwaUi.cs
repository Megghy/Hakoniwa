using System;
using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.UI.Windows;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.Audio;

namespace Hakoniwa.UI;

public static class HakoniwaUi
{
    public static bool Visible = true;
    public static bool ChatOpen => Chat.IsOpen || Main.drawingPlayerChat;
    public static bool StudioIsOpen
    {
        get => Studio.IsOpen;
        set => Studio.IsOpen = value;
    }
    public static bool ItemPickerIsOpen
    {
        get => ItemPicker.IsOpen;
        set => ItemPicker.IsOpen = value;
    }
    public static bool ItemEditorIsOpen
    {
        get => ItemEditor.IsOpen;
        set => ItemEditor.IsOpen = value;
    }

    private static ImGuiBackend? _backend;
    private static readonly StudioWindow Studio = new();
    public static readonly ItemPickerWindow ItemPicker = new();
    public static readonly ItemEditorWindow ItemEditor = new();
    private static readonly FloatingBall FloatingBall = new();
    private static readonly EditorToolbar Toolbar = new();
    private static readonly ChatOverlay Chat = new();
    private static readonly SignEditorWindow SignEditor = new();

    private static bool _leftWasDown;
    private static bool _insertWasDown;
    private static bool _middleWasDown;
    private static bool _rightWasDown;
    private static bool _toolRightWasDown;
    private static bool _enterWasDown;
    private static bool _invWasOpen;
    private static bool _brushMerging;
    private static string _packRename = "";

    public static void SaveSelectionAsSchematic() => Studio.SaveFromSelection();

    public static void Install()
    {
        CheatHooks.PreUpdate += SyncMouseBlock;
        CheatHooks.LocalItemCheckBegin += TileStrokeRecorder.Before;
        CheatHooks.LocalItemCheckEnd += TileStrokeRecorder.After;
        CheatHooks.PostUpdate += Tick;
        Notices.Posted += NotifyHost.Enqueue;
        Main.OnEngineLoad += Init;
        Main.OnPostDraw += _ => Render();
    }

    public static void Tick()
    {
        if (_backend is null || Main.gameMenu)
            return;

        var kb = Keyboard.GetState();
        bool insert = kb.IsKeyDown(Keys.Insert);
        if (insert && !_insertWasDown)
            Visible = !Visible;
        _insertWasDown = insert;

        HandleChatToggle(kb);
        CaptureSelectKey(kb);
        if (CheatState.ImGuiInput)
            SignEditor.UpdateSignState();
        else
            SignEditor.IsOpen = false;
        HandleTeleport();
        HandleEditor();
        NotifyHost.WatchToggles();
    }

    private static void SyncMouseBlock()
    {
        if (_backend is null || Main.gameMenu)
            return;
        Ui.Sync(
            extraMouse: SignEditor.IsOpen,
            extraKeyboard: SignEditor.IsOpen || CheatState.WaitingSelectKey || Chat.InputFocused,
            blockGameMouse: (Visible || EditorSession.Pasting) && !Main.mapFullscreen && FocusHelper.AllowInputProcessing,
            skipImGuiKeyboard: Chat.IsOpen && !Chat.InputFocused);
    }

    private static void CaptureSelectKey(KeyboardState kb)
    {
        if (!CheatState.WaitingSelectKey)
            return;
        if (kb.IsKeyDown(Keys.Escape))
        {
            CheatState.WaitingSelectKey = false;
            EditorSession.SyncKeys(kb);
            return;
        }

        var pressed = kb.GetPressedKeys();
        for (int i = 0; i < pressed.Length; i++)
        {
            var key = pressed[i];
            if (key is Keys.None or Keys.Escape or Keys.Insert)
                continue;
            CheatState.SelectModifier = key;
            CheatState.WaitingSelectKey = false;
            EditorSession.SyncKeys(kb);
            Notices.Post($"框选键: {key}");
            return;
        }
    }

    public static void Render()
    {
        if (_backend is null)
            return;

        if (!_backend.NewFrame())
            return;
        // Tools → Panels → Notices. World overlay uses Ui.WorldList (behind all windows).
        if (!Main.gameMenu)
            SchematicDrawer.DrawWorld();
        if (Visible)
        {
            FloatingBall.Draw();
            if (!Main.gameMenu)
            {
                Toolbar.Draw();
                SelectionOverlay.Draw();
                if (Main.playerInventory && Main.LocalPlayer.active)
                    DrawInventoryButton();
                else if (_invWasOpen && Main.LocalPlayer.active)
                    InventoryPacks.Capture(Main.LocalPlayer);
                _invWasOpen = Main.playerInventory && Main.LocalPlayer.active;
            }
            Studio.Draw();
            ItemPicker.Draw();
            ItemEditor.Draw();
        }

        SignEditor.Draw();
        Chat.Draw();
        NotifyHost.Draw();
        _backend.Render(Visible && SelectionOverlay.ShouldBlock());
        SyncMouseBlock();
    }

    private static void DrawInventoryButton()
    {
        const float invScale = 0.85f;
        const float row1 = 28f;
        float maxW = 10f * 56f * invScale;
        MeasurePacks(maxW, out float packsW, out float packsH);
        float x = 20f;
        float y = 20f + 5f * 56f * invScale;
        if (Main.ChestOrShopUIVisible)
            y += 168f;
        var pos = new System.Numerics.Vector2(x, y);
        var size = new System.Numerics.Vector2(Math.Max(276f, packsW), row1 + 4f + packsH);

        if (!Ui.BeginChrome("##HakoniwaInvShortcut", pos, size))
            return;

        var dl = ImGui.GetWindowDrawList();
        var wp = ImGui.GetWindowPos();
        var min = wp;
        var max = wp + size;

        // 获取当前背包聚焦物品 (鼠标抓取 > 鼠标悬停 > 玩家手持)
        Item target = !Main.mouseItem.IsAir
            ? Main.mouseItem
            : (Main.HoverItem != null && !Main.HoverItem.IsAir ? Main.HoverItem : Main.LocalPlayer.HeldItem);

        float btnW = 88f;
        float gap = 4f;

        // 按钮 1: 物品库
        var btn1Min = wp;
        var btn1Max = wp + new System.Numerics.Vector2(btnW, 28f);
        Ui.Invisible("##btnPicker", btn1Min, new System.Numerics.Vector2(btnW, 28f));
        bool hover1 = ImGui.IsItemHovered();
        bool active1 = ItemPicker.IsOpen;
        if (ImGui.IsItemClicked()) ItemPicker.Toggle();

        uint bg1 = active1 ? 0xF81C263C : (hover1 ? 0xF8141C2A : 0xF00D111A);
        uint border1 = active1 ? Ui.GoldBorder : (hover1 ? Ui.ChipOn : Ui.ChipLine);
        dl.AddRectFilled(btn1Min, btn1Max, bg1);
        dl.AddRect(btn1Min, btn1Max, border1, 0f, ImDrawFlags.None, 1f);
        UiIcons.DrawItemDirect(dl, btn1Min + new System.Numerics.Vector2(14f, 14f), Terraria.ID.ItemID.Chest, 18f);
        dl.AddText(btn1Min + new System.Numerics.Vector2(26f, 6f), active1 ? 0xFFFFFFFF : 0xFFB4C2D6, "箱庭物品库");
        if (hover1) ImGui.SetTooltip("打开/关闭 独立物品选择器 (快捷拿取/查询物品)");

        // 按钮 2: 高级编辑
        var btn2Min = wp + new System.Numerics.Vector2(btnW + gap, 0f);
        var btn2Max = btn2Min + new System.Numerics.Vector2(btnW, 28f);
        Ui.Invisible("##btnEditor", btn2Min, new System.Numerics.Vector2(btnW, 28f));
        bool hover2 = ImGui.IsItemHovered();
        bool active2 = ItemEditor.IsOpen;
        if (ImGui.IsItemClicked())
        {
            if (!target.IsAir)
                ItemEditor.OpenWith(target);
            else
                ItemEditor.IsOpen = !ItemEditor.IsOpen;
        }

        uint bg2 = active2 ? 0xF81C263C : (hover2 ? 0xF8141C2A : 0xF00D111A);
        uint border2 = active2 ? Ui.GoldBorder : (hover2 ? Ui.ChipOn : Ui.ChipLine);
        dl.AddRectFilled(btn2Min, btn2Max, bg2);
        dl.AddRect(btn2Min, btn2Max, border2, 0f, ImDrawFlags.None, 1f);
        Icons.DrawDirect(dl, btn2Min + new System.Numerics.Vector2(14f, 14f), Icons.Pencil, 255, active2 ? Ui.ChipOn : 0xFF8A8794, 16f);
        dl.AddText(btn2Min + new System.Numerics.Vector2(26f, 6f), active2 ? 0xFFFFFFFF : 0xFFB4C2D6, "高级编辑");
        if (hover2)
        {
            string targetName = !target.IsAir ? target.Name : "无物品";
            ImGui.SetTooltip($"打开高级物品属性编辑器\n当前目标: {targetName}\n(支持修改伤害/射速/弹道/染色/大小等所有属性)");
        }

        // 按钮 3: 复制 /cw 命令
        var btn3Min = wp + new System.Numerics.Vector2((btnW + gap) * 2f, 0f);
        var btn3Max = btn3Min + new System.Numerics.Vector2(btnW, 28f);
        Ui.Invisible("##btnCopyCw", btn3Min, new System.Numerics.Vector2(btnW, 28f));
        bool hover3 = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            if (!target.IsAir)
            {
                var data = CustomWeaponData.FromItem(target);
                string cmd = data.ToCwCommand();
                ImGui.SetClipboardText(cmd);
                Notices.Post($"已复制 {target.Name} 为 /cwadd 命令");
                SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
            }
            else
            {
                Notices.Post("请先将鼠标悬停在物品上或手持一件物品");
            }
        }

        uint bg3 = hover3 ? 0xF8141C2A : 0xF00D111A;
        uint border3 = hover3 ? Ui.ChipOn : Ui.ChipLine;
        dl.AddRectFilled(btn3Min, btn3Max, bg3);
        dl.AddRect(btn3Min, btn3Max, border3, 0f, ImDrawFlags.None, 1f);
        Icons.DrawDirect(dl, btn3Min + new System.Numerics.Vector2(14f, 14f), Icons.Copy, 255, hover3 ? Ui.ChipOn : 0xFF8A8794, 16f);
        dl.AddText(btn3Min + new System.Numerics.Vector2(26f, 6f), 0xFFB4C2D6, "复制 /cw");
        if (hover3)
        {
            string targetName = !target.IsAir ? target.Name : "无物品";
            ImGui.SetTooltip($"一键将当前悬停/手持物品生成并复制为 /cwadd 命令\n当前目标: {targetName}");
        }

        DrawPacks(dl, wp, row1 + 4f, maxW);
        Ui.EndChrome();
    }

    private const float PackChipH = 22f;
    private const float PackChipGap = 2f;
    private const float PackPlusW = 22f;

    private static float PackChipW(string name) =>
        Math.Max(28f, ImGui.CalcTextSize(name).X + 16f);

    private static void MeasurePacks(float maxW, out float width, out float height)
    {
        float cx = 0f, cy = 0f, used = 0f;
        for (int i = 0; i < InventoryPacks.Count; i++)
        {
            float w = PackChipW(InventoryPacks.NameAt(i));
            WrapPack(ref cx, ref cy, w, maxW);
            cx += w + PackChipGap;
            used = Math.Max(used, cx);
        }

        WrapPack(ref cx, ref cy, PackPlusW, maxW);
        width = Math.Max(used, cx + PackPlusW);
        height = cy + PackChipH;
    }

    private static void WrapPack(ref float x, ref float y, float w, float maxW)
    {
        if (x > 0f && x + w > maxW)
        {
            x = 0f;
            y += PackChipH + PackChipGap;
        }
    }

    private static void DrawPacks(ImDrawListPtr dl, System.Numerics.Vector2 wp, float originY, float maxW)
    {
        float cx = 0f, cy = originY;
        for (int i = 0; i < InventoryPacks.Count; i++)
        {
            string name = InventoryPacks.NameAt(i);
            float w = PackChipW(name);
            WrapPack(ref cx, ref cy, w, maxW);
            var minP = wp + new System.Numerics.Vector2(cx, cy);
            var sz = new System.Numerics.Vector2(w, PackChipH);
            Ui.Invisible($"##pack{i}", minP, sz);
            bool hover = ImGui.IsItemHovered();
            bool on = InventoryPacks.Active == i;
            if (ImGui.IsItemClicked())
            {
                InventoryPacks.SwitchTo(Main.LocalPlayer, i);
                SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
            }

            if (hover && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
                _packRename = name;

            Ui.DrawPixelSlot(dl, minP, minP + sz, hover, on);
            uint color = on ? 0xFFFFFFFF : (InventoryPacks.Occupied(i) ? 0xFFB4C2D6 : 0xFF6A7384);
            var textSize = ImGui.CalcTextSize(name);
            dl.AddText(minP + (sz - textSize) * 0.5f, color, name);
            if (hover)
                ImGui.SetTooltip($"{name}\n左键切换 · 右键重命名/删除");

            if (ImGui.BeginPopupContextItem($"packMenu{i}"))
            {
                ImGui.SetNextItemWidth(140f);
                if (ImGui.InputText("##packRename", ref _packRename, (UIntPtr)32, ImGuiInputTextFlags.EnterReturnsTrue))
                {
                    InventoryPacks.Rename(i, _packRename);
                    ImGui.CloseCurrentPopup();
                }

                if (ImGui.Button("重命名"))
                {
                    InventoryPacks.Rename(i, _packRename);
                    ImGui.CloseCurrentPopup();
                }

                ImGui.SameLine();
                if (ImGui.Button("删除"))
                {
                    InventoryPacks.Remove(Main.LocalPlayer, i);
                    ImGui.CloseCurrentPopup();
                    ImGui.EndPopup();
                    return;
                }

                ImGui.EndPopup();
            }

            cx += w + PackChipGap;
        }

        WrapPack(ref cx, ref cy, PackPlusW, maxW);
        var plusMin = wp + new System.Numerics.Vector2(cx, cy);
        var plusSz = new System.Numerics.Vector2(PackPlusW, PackChipH);
        Ui.Invisible("##packAdd", plusMin, plusSz);
        bool plusHover = ImGui.IsItemHovered();
        if (ImGui.IsItemClicked())
        {
            InventoryPacks.Add(Main.LocalPlayer);
            SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
        }

        Ui.DrawPixelSlot(dl, plusMin, plusMin + plusSz, plusHover, false);
        var plusSize = ImGui.CalcTextSize("+");
        dl.AddText(plusMin + (plusSz - plusSize) * 0.5f, plusHover ? 0xFFFFFFFF : 0xFFB4C2D6, "+");
        if (plusHover)
            ImGui.SetTooltip("新建背包（保存当前物品）");
    }

    private static void Init()
    {
        CheatHooks.ApplyFpsUnlock();
        _backend = new ImGuiBackend(Main.instance.GraphicsDevice);
        Studio.LoadLibrary();
    }

    private static void HandleChatToggle(KeyboardState kb)
    {
        if (!CheatState.ImGuiInput)
        {
            if (Chat.IsOpen)
                Chat.Close();
            _enterWasDown = kb.IsKeyDown(Keys.Enter);
            return;
        }

        bool enter = kb.IsKeyDown(Keys.Enter);
        if (enter && !_enterWasDown && !Main.editSign && !Main.editChest)
        {
            if (!Chat.IsOpen)
            {
                if (!Ui.Keyboard)
                    Chat.Open();
            }
            else if (!Chat.InputFocused)
                Chat.Focus();
        }

        _enterWasDown = enter;

        if (Main.drawingPlayerChat)
        {
            Main.drawingPlayerChat = false;
            if (!Chat.IsOpen)
                Chat.Open();
            else if (!Chat.InputFocused)
                Chat.Focus();
        }
    }

    private static void HandleTeleport()
    {
        if (!CheatState.ClickTeleport || Ui.Mouse || !Main.LocalPlayer.active || !FocusHelper.AllowInputProcessing)
        {
            _rightWasDown = true;
            _middleWasDown = true;
            return;
        }

        var mouse = Mouse.GetState();
        bool right = mouse.RightButton == ButtonState.Pressed;
        bool middle = mouse.MiddleButton == ButtonState.Pressed;
        if (Main.mapFullscreen && right && !_rightWasDown)
        {
            TeleportTo(MapToWorld(mouse));
            Main.mapFullscreen = false;
        }
        else if (!Main.mapFullscreen && middle && !_middleWasDown)
        {
            TeleportTo(Main.MouseWorld);
        }

        _rightWasDown = right;
        _middleWasDown = middle;
    }

    private static void TeleportTo(Vector2 world)
    {
        var player = Main.LocalPlayer;
        player.velocity = Vector2.Zero;
        player.Teleport(world - new Vector2(player.width / 2f, player.height), 1);
    }

    private static Vector2 MapToWorld(MouseState mouse)
    {
        var screen = new Vector2(mouse.X, mouse.Y);
        var center = new Vector2(Main.screenWidth, Main.screenHeight) / 2f;
        return ((screen - center) / Main.mapFullscreenScale + Main.mapFullscreenPos) * 16f;
    }

    private static int _lastScrollWheel;

    private static void HandleEditor()
    {
        var kb = Keyboard.GetState();
        if (!FocusHelper.AllowInputProcessing || Main.mapFullscreen || Chat.IsOpen || SignEditor.IsOpen || NativeTextInput.Busy)
        {
            _leftWasDown = true;
            _toolRightWasDown = true;
            EditorSession.SyncKeys(kb);
            EditorSession.Stroking = false;
            _lastScrollWheel = Mouse.GetState().ScrollWheelValue;
            return;
        }

        if (CheatState.WaitingSelectKey)
            EditorSession.SyncKeys(kb);
        else
            EditorSession.HandleKeys(kb, !Ui.Keyboard);

        var mouseState = Mouse.GetState();
        int scrollDelta = mouseState.ScrollWheelValue - _lastScrollWheel;
        _lastScrollWheel = mouseState.ScrollWheelValue;
        bool ctrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);

        // Ctrl + 滚轮动态调节笔刷大小
        if (ctrl && scrollDelta != 0 && (EditorSession.Tool is EditorTool.Brush or EditorTool.Eraser || EditorSession.Tool == EditorTool.Shape && EditorSession.DrawKind == DrawKind.Line || StudioIsOpen))
        {
            int step = scrollDelta > 0 ? 1 : -1;
            int nextRadius = Math.Max(0, Math.Min(50, EditorSession.BrushRadius + step));
            if (nextRadius != EditorSession.BrushRadius)
            {
                EditorSession.BrushRadius = nextRadius;
                SelectionOverlay.TriggerBrushHud();
            }
        }

        bool left = mouseState.LeftButton == ButtonState.Pressed;
        bool right = mouseState.RightButton == ButtonState.Pressed;
        if (EditorSession.Pasting && !Ui.Mouse)
        {
            if (left && !_leftWasDown)
                EditorSession.CommitPaste();
            else if (right && !_toolRightWasDown)
                EditorSession.CancelPaste();
        }

        bool blocked = Visible && SelectionOverlay.Update(!Ui.Mouse && !EditorSession.Pasting);
        if (Visible && !Ui.Mouse && !blocked && !EditorSession.Pasting)
        {
            var tool = EditorSession.Tool;
            EditorSession.CursorTile(out int tx, out int ty);
            if (tool == EditorTool.Shape)
            {
                if (left && !_leftWasDown)
                    EditorSession.BeginStroke(tx, ty);
                else if (left && EditorSession.Stroking)
                    EditorSession.DragStroke(tx, ty);
            }
            else if (right && !_toolRightWasDown && tool == EditorTool.Replace)
                EditorSession.PickMatch(tx, ty);
            else if (left && tool is EditorTool.Brush or EditorTool.Eraser)
            {
                if (!_leftWasDown)
                {
                    EditorSession.History.Merge();
                    _brushMerging = true;
                }

                EditorSession.ApplyToolAtCursor();
            }
            else if (left && !_leftWasDown && tool != EditorTool.Marquee)
                EditorSession.ApplyToolAtCursor();
        }

        _leftWasDown = left;
        _toolRightWasDown = right;
        if (!left && EditorSession.Stroking)
            EditorSession.EndStroke();
        if (!left && _brushMerging)
        {
            EditorSession.History.Seal();
            _brushMerging = false;
        }
    }
}
