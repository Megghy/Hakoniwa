using Hexa.NET.ImGui;
using Terraria;
using Terraria.Testing;
using Terraria.Testing.ChatCommands;
using Xna = Microsoft.Xna.Framework;
using Vector2 = System.Numerics.Vector2;

namespace Hakoniwa.UI.Windows;

internal static class VanillaDebug
{
    private static int _tileId;
    private static int _wallId = 1;

    public static void Draw()
    {
        Ui.Heading(Icons.Lightbulb, "原版调试");
        ImGui.BeginChild("vanilla-debug", new Vector2(0, 168f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(ImGui.GetWindowDrawList(), ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        ImGui.Checkbox("碰撞箱", ref DebugOptions.DrawHitboxes);
        ImGui.SameLine();
        ImGui.Checkbox("仇恨范围", ref DebugOptions.DrawAggro);
        ImGui.SameLine();
        ImGui.Checkbox("无世界边界", ref DebugOptions.noLimits);

        ImGui.Checkbox("隐藏物块", ref DebugOptions.hideTiles);
        ImGui.SameLine();
        ImGui.Checkbox("隐藏非实心", ref DebugOptions.hideTiles2);
        ImGui.SameLine();
        ImGui.Checkbox("隐藏墙", ref DebugOptions.hideWalls);
        ImGui.SameLine();
        ImGui.Checkbox("隐藏水", ref DebugOptions.hideWater);
        ImGui.Checkbox("显示不可破坏墙", ref DebugOptions.ShowUnbreakableWall);

        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("物块 ID", ref _tileId, 0, 0);
        ImGui.SameLine();
        if (ImGui.Button("查找物块"))
            Run($"/find {_tileId}");
        ImGui.SameLine();
        if (ImGui.Button("下一个"))
            Run("/next", next: true);

        ImGui.SetNextItemWidth(80f);
        ImGui.InputInt("墙 ID", ref _wallId, 0, 0);
        ImGui.SameLine();
        if (ImGui.Button("查找墙"))
            Run($"/findwall {_wallId}");

        ImGui.EndChild();
    }

    private static void Run(string command, bool next = false)
    {
        var before = Main.mapFullscreenPos;
        if (next)
            ToolkitDebugCommands.NextCommand(Msg(command));
        else
        {
            ToolkitDebugCommands.FindNextEnumerable = null;
            if (command.StartsWith("/findwall"))
                ToolkitDebugCommands.FindWallCommand(Msg(command));
            else
                ToolkitDebugCommands.FindCommand(Msg(command));
        }
        if (Main.mapFullscreenPos != before)
            WarpToPing();
    }

    private static void WarpToPing()
    {
        var pos = Main.mapFullscreenPos;
        if (pos == default)
            return;
        var player = Main.LocalPlayer;
        HakoniwaUi.PlaceLocalPlayer(new Xna.Vector2(pos.X * 16f - player.width / 2f, pos.Y * 16f - player.height));
    }

    private static DebugMessage Msg(string command) => new((byte)Main.myPlayer, command);
}
