using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.UI.Windows;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace Hakoniwa.UI;

public static class HakoniwaUi
{
    public static bool Visible = true;
    public static bool StudioIsOpen
    {
        get => Studio.IsOpen;
        set => Studio.IsOpen = value;
    }

    private static ImGuiBackend? _backend;
    private static readonly StudioWindow Studio = new();
    private static readonly FloatingBall FloatingBall = new();
    private static readonly ChatOverlay Chat = new();
    private static readonly SignEditorWindow SignEditor = new();

    private static bool _leftWasDown;
    private static bool _insertWasDown;
    private static bool _middleWasDown;
    private static bool _rightWasDown;
    private static bool _enterWasDown;
    private static int _hotkeys;

    public static void Install()
    {
        CheatHooks.PreUpdate += SyncMouseBlock;
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

        var io = ImGui.GetIO();
        if (io.WantCaptureMouse)
            Main.LocalPlayer.mouseInterface = true;

        HandleChatToggle(kb, io);
        CaptureSelectKey(kb);
        SignEditor.UpdateSignState();
        HandleTeleport(io);
        HandleEditor(io);
        NotifyHost.WatchToggles();
    }

    private static void SyncMouseBlock()
    {
        if (_backend is null || Main.gameMenu)
            return;
        var io = ImGui.GetIO();
        CheatHooks.BlockGameMouse = io.WantCaptureMouse || Chat.IsOpen || SignEditor.IsOpen || (Visible && SelectionOverlay.ShouldBlock());
    }

    private static void CaptureSelectKey(KeyboardState kb)
    {
        if (!CheatState.WaitingSelectKey)
            return;
        if (kb.IsKeyDown(Keys.Escape))
        {
            CheatState.WaitingSelectKey = false;
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
            Notices.Post($"框选键: {key}");
            return;
        }
    }

    public static void Render()
    {
        if (_backend is null)
            return;

        _backend.NewFrame();
        if (Visible)
        {
            FloatingBall.Draw();
            Studio.Draw();
        }

        SignEditor.Draw();
        Chat.Draw();
        DrawOverlay();
        NotifyHost.Draw();
        _backend.Render();
        var io = ImGui.GetIO();
        CheatHooks.BlockGameMouse = io.WantCaptureMouse || Chat.IsOpen || SignEditor.IsOpen || (Visible && SelectionOverlay.ShouldBlock());
        CheatHooks.BlockGameKeyboard = Chat.IsOpen || SignEditor.IsOpen || io.WantCaptureKeyboard || CheatState.WaitingSelectKey;
    }

    private static void Init()
    {
        _backend = new ImGuiBackend(Main.instance.GraphicsDevice);
    }

    private static void HandleChatToggle(KeyboardState kb, ImGuiIOPtr io)
    {
        bool enter = kb.IsKeyDown(Keys.Enter);
        if (enter && !_enterWasDown)
        {
            if (!Chat.IsOpen && !io.WantCaptureKeyboard && !Main.editSign && !Main.editChest)
                Chat.Open();
        }

        _enterWasDown = enter;

        // 如果游戏原生尝试打开聊天框，自动接管转为 ImGui 聊天输入
        if (Main.drawingPlayerChat)
        {
            Main.drawingPlayerChat = false;
            if (!Chat.IsOpen)
                Chat.Open();
        }
    }

    private static void HandleTeleport(ImGuiIOPtr io)
    {
        if (!CheatState.ClickTeleport || io.WantCaptureMouse || !Main.LocalPlayer.active)
            return;

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

    private static void HandleEditor(ImGuiIOPtr io)
    {
        if (io.WantCaptureMouse || Main.mapFullscreen || Chat.IsOpen || SignEditor.IsOpen)
        {
            _leftWasDown = false;
            return;
        }

        bool left = Mouse.GetState().LeftButton == ButtonState.Pressed;
        bool blocked = Visible && SelectionOverlay.Update();
        if (!blocked && left && !_leftWasDown)
            EditorSession.ApplyToolAtCursor();

        var kb = Keyboard.GetState();
        bool ctrl = !io.WantCaptureKeyboard && (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl));
        int hotkeys = 0;
        if (ctrl && kb.IsKeyDown(Keys.C)) hotkeys |= 1;
        if (ctrl && kb.IsKeyDown(Keys.X)) hotkeys |= 2;
        if (ctrl && kb.IsKeyDown(Keys.V)) hotkeys |= 4;
        if (ctrl && kb.IsKeyDown(Keys.Z)) hotkeys |= 8;
        if (ctrl && kb.IsKeyDown(Keys.Y)) hotkeys |= 16;
        int pressed = hotkeys & ~_hotkeys;
        if ((pressed & 1) != 0) EditorSession.Copy();
        if ((pressed & 2) != 0) EditorSession.Cut();
        if ((pressed & 4) != 0) EditorSession.Paste();
        if ((pressed & 8) != 0) EditorSession.Undo();
        if ((pressed & 16) != 0) EditorSession.Redo();
        _hotkeys = hotkeys;
        _leftWasDown = left;
    }

    private static void DrawOverlay()
    {
        if (Visible)
            SelectionOverlay.Draw();
    }
}
