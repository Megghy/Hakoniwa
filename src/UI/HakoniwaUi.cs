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
    private static readonly EditorToolbar Toolbar = new();
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

        HandleChatToggle(kb);
        CaptureSelectKey(kb);
        SignEditor.UpdateSignState();
        HandleTeleport();
        HandleEditor();
        NotifyHost.WatchToggles();
    }

    private static void SyncMouseBlock()
    {
        if (_backend is null || Main.gameMenu)
            return;
        Ui.Sync(
            extraMouse: Chat.IsOpen || SignEditor.IsOpen || (Visible && SelectionOverlay.ShouldBlock()),
            extraKeyboard: Chat.IsOpen || SignEditor.IsOpen || CheatState.WaitingSelectKey);
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

        if (!_backend.NewFrame())
            return;
        // Tools → Panels → Notices. World overlay uses Ui.WorldList (behind all windows).
        if (Visible)
        {
            FloatingBall.Draw();
            if (!Main.gameMenu)
            {
                Toolbar.Draw();
                SelectionOverlay.Draw();
            }
            Studio.Draw();
        }

        SignEditor.Draw();
        Chat.Draw();
        NotifyHost.Draw();
        _backend.Render(Visible && SelectionOverlay.ShouldBlock());
        SyncMouseBlock();
    }

    private static void Init()
    {
        CheatHooks.ApplyFpsUnlock();
        _backend = new ImGuiBackend(Main.instance.GraphicsDevice);
    }

    private static void HandleChatToggle(KeyboardState kb)
    {
        bool enter = kb.IsKeyDown(Keys.Enter);
        if (enter && !_enterWasDown)
        {
            if (!Chat.IsOpen && !Ui.Keyboard && !Main.editSign && !Main.editChest)
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

    private static void HandleEditor()
    {
        if (!FocusHelper.AllowInputProcessing || Main.mapFullscreen || Chat.IsOpen || SignEditor.IsOpen)
        {
            _leftWasDown = true;
            return;
        }

        bool left = Mouse.GetState().LeftButton == ButtonState.Pressed;
        bool blocked = Visible && SelectionOverlay.Update(!Ui.Mouse);
        if (!Ui.Mouse && !blocked && left && !_leftWasDown && EditorSession.Tool != EditorTool.Marquee)
            EditorSession.ApplyToolAtCursor();

        var kb = Keyboard.GetState();
        bool ctrl = !Ui.Keyboard && (kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl));
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
}
