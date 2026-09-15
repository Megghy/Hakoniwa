using Hakoniwa.Core;
using Hakoniwa.Engine;
using Hakoniwa.UI.Windows;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public static class HakoniwaUi
{
    public static bool Visible = true;
    private static ImGuiBackend? _backend;
    private static readonly WindowManager Windows = new();
    private static readonly RadialLauncher Launcher = new();
    private static bool _leftWasDown;
    private static bool _insertWasDown;
    private static bool _middleWasDown;
    private static bool _rightWasDown;
    private static int _hotkeys;

    public static void Install()
    {
        CheatHooks.PostUpdate += Tick;
        Main.OnEngineLoad += Init;
        Main.OnPostDraw += _ => Render();
        Windows.Add(new ToolboxWindow());
        Windows.Add(new WorldControlWindow());
        Windows.Add(new SelectionWindow(EditorSession.Selection));
        Windows.Add(new SchematicWindow());
        Windows.Add(new ItemBrowserWindow());
        Windows.Add(new CharacterWindow());
        Windows.Add(new ItemEditorWindow());
    }

    public static void Tick()
    {
        if (_backend is null || Main.gameMenu)
            return;

        bool insert = Keyboard.GetState().IsKeyDown(Keys.Insert);
        if (insert && !_insertWasDown)
            Visible = !Visible;
        _insertWasDown = insert;

        var io = ImGui.GetIO();
        if (io.WantCaptureMouse)
            Main.LocalPlayer.mouseInterface = true;

        HandleTeleport(io);
        HandleEditor(io);
    }

    public static void Render()
    {
        if (_backend is null)
            return;
        _backend.NewFrame();
        if (Visible)
            Windows.DrawAll();
        DrawOverlay();
        Launcher.Draw(Windows.Windows);
        _backend.Render();
    }

    private static void Init()
    {
        _backend = new ImGuiBackend(Main.instance.GraphicsDevice);
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
            TeleportTo(Main.MouseWorld);

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
        if (io.WantCaptureMouse || Main.mapFullscreen)
        {
            _leftWasDown = false;
            return;
        }

        bool left = Mouse.GetState().LeftButton == ButtonState.Pressed;
        int x = (int)(Main.MouseWorld.X / 16f);
        int y = (int)(Main.MouseWorld.Y / 16f);
        if (EditorSession.SelectedTool == 3)
        {
            if (left && !_leftWasDown)
                EditorSession.Selection.Begin(x, y);
            else if (left)
                EditorSession.Selection.DragTo(x, y);
        }
        else if (left && !_leftWasDown)
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
        var selection = EditorSession.Selection;
        if (!selection.Active || Main.gameMenu || Main.mapFullscreen)
            return;

        var list = ImGui.GetBackgroundDrawList();
        var min = WorldToScreen(new Vector2(selection.MinX * 16, selection.MinY * 16));
        var max = WorldToScreen(new Vector2((selection.MaxX + 1) * 16, (selection.MaxY + 1) * 16));
        list.AddRect(min, max, ImGui.ColorConvertFloat4ToU32(new Num.Vector4(0.85f, 0.65f, 1f, 0.9f)), 0f, ImDrawFlags.None, 2f);
    }

    private static Num.Vector2 WorldToScreen(Vector2 world)
    {
        var screen = Vector2.Transform(world - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);
        return new Num.Vector2(screen.X, screen.Y);
    }
}
