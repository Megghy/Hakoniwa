using System;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;

namespace Hakoniwa.UI.Windows;

internal static class WorldTab
{
    private static readonly int[] TownNpcs =
    [
        NPCID.Guide, NPCID.Merchant, NPCID.Nurse, NPCID.Demolitionist, NPCID.Dryad,
        NPCID.ArmsDealer, NPCID.Clothier, NPCID.Mechanic, NPCID.GoblinTinkerer,
        NPCID.Wizard, NPCID.WitchDoctor, NPCID.DyeTrader, NPCID.Truffle, NPCID.Pirate,
        NPCID.Steampunker, NPCID.Cyborg, NPCID.Painter, NPCID.PartyGirl, NPCID.Stylist,
        NPCID.Angler, NPCID.TaxCollector, NPCID.DD2Bartender, NPCID.Golfer,
        NPCID.BestiaryGirl, NPCID.Princess, NPCID.TownCat, NPCID.TownDog, NPCID.TownBunny,
    ];

    private static int _townIndex;

    public static void Draw()
    {
        Ui.BeginScroll("world-scroll");

        float availW = ImGui.GetContentRegionAvail().X;
        float halfW = (availW - 8f) * 0.5f;

        ImGui.BeginChild("rules-card-left", new Vector2(halfW, 186f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        Ui.Heading(Icons.Shield, "创造特权 (Privileges)");
        ImGui.Checkbox("上帝模式 (God Mode)", ref CheatState.GodMode);
        ImGui.Checkbox("全图照明 (Full Bright)", ref CheatState.FullBright);
        ImGui.Checkbox("穿墙模式 (No Clip)", ref CheatState.NoClip);
        ImGui.Checkbox("自由悬空放置 (Free Placement)", ref CheatState.FreePlacement);
        ImGui.Checkbox("快捷传送 (Click Teleport)", ref CheatState.ClickTeleport);
        ImGui.EndChild();

        ImGui.SameLine(0f, 8f);

        ImGui.BeginChild("rules-card-right", new Vector2(halfW, 186f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        Ui.Heading(Icons.Infinity, "建筑辅助 (Assists)");
        ImGui.Checkbox("无限放置范围 (Infinite Reach)", ref CheatState.InfiniteReach);
        ImGui.Checkbox("无限物块消耗 (Infinite Items)", ref CheatState.InfiniteItems);
        ImGui.Checkbox("锁定世界时间 (Freeze Time)", ref CheatState.FreezeTime);
        ImGui.EndChild();

        ImGui.Spacing();
        Ui.Heading(Icons.Clock, "世界时间控制 (World Time & Phase)");
        ImGui.BeginChild("time-card", new Vector2(0, 110f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        float time = GetTimeFraction();
        if (ImGui.SliderFloat("时间进度 (0:00 - 24:00)", ref time, 0f, 1f, GetTimeString(time)))
            SetTimeFraction(time);
        ImGui.Spacing();
        float btnW = (ImGui.GetContentRegionAvail().X - 12f) / 4f;
        if (ImGui.Button("清晨 04:30", new Vector2(btnW, 26f))) SetTimeFraction(0f);
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("正午 12:00", new Vector2(btnW, 26f))) SetTimeFraction(27000f / 86400f);
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("黄昏 19:30", new Vector2(btnW, 26f))) SetTimeFraction(54000f / 86400f);
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("午夜 00:00", new Vector2(btnW, 26f))) SetTimeFraction(70200f / 86400f);
        ImGui.EndChild();

        ImGui.Spacing();
        Ui.Heading(Icons.Home, "出生点 / NPC / 事件");
        ImGui.BeginChild("world-flags-card", new Vector2(0, 220f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        float actionW = (ImGui.GetContentRegionAvail().X - 8f) / 3f;
        if (ImGui.Button("设为出生点", new Vector2(actionW, 26f)))
            SetSpawnAtPlayer();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("传到出生点", new Vector2(actionW, 26f)))
            TeleportSpawn();
        ImGui.SameLine(0f, 4f);
        if (ImGui.Button("全部 Boss 已击败", new Vector2(actionW, 26f)))
            SetBossesDowned(true);

        ImGui.Spacing();
        bool blood = Main.bloodMoon;
        if (ImGui.Checkbox("血月", ref blood))
            Main.bloodMoon = blood;
        ImGui.SameLine();
        bool eclipse = Main.eclipse;
        if (ImGui.Checkbox("日食", ref eclipse))
            Main.eclipse = eclipse;
        ImGui.SameLine();
        bool rain = Main.raining;
        if (ImGui.Checkbox("雨", ref rain))
        {
            if (rain) Main.StartRain();
            else Main.StopRain();
        }

        ImGui.SameLine();
        bool pumpkin = Main.pumpkinMoon;
        if (ImGui.Checkbox("南瓜月", ref pumpkin))
            Main.pumpkinMoon = pumpkin;
        ImGui.SameLine();
        bool frost = Main.snowMoon;
        if (ImGui.Checkbox("霜月", ref frost))
            Main.snowMoon = frost;

        ImGui.Spacing();
        string preview = Lang.GetNPCNameValue(TownNpcs[_townIndex]);
        ImGui.SetNextItemWidth(220f);
        if (ImGui.BeginCombo("城镇 NPC", preview))
        {
            for (int i = 0; i < TownNpcs.Length; i++)
            {
                bool selected = i == _townIndex;
                if (ImGui.Selectable(Lang.GetNPCNameValue(TownNpcs[i]), selected))
                    _townIndex = i;
                if (selected)
                    ImGui.SetItemDefaultFocus();
            }

            ImGui.EndCombo();
        }

        ImGui.SameLine();
        if (ImGui.Button("在玩家处生成"))
            SpawnTown(TownNpcs[_townIndex]);
        ImGui.SameLine();
        if (ImGui.Button("生成全部城镇 NPC"))
        {
            for (int i = 0; i < TownNpcs.Length; i++)
                SpawnTown(TownNpcs[i]);
        }

        ImGui.TextDisabled($"出生点 ({Main.spawnTileX}, {Main.spawnTileY})");
        ImGui.EndChild();

        ImGui.Spacing();
        ImGui.TextDisabled("快捷传送：大地图右键，或世界中键。");
        ImGui.EndChild();
    }

    private static void SetSpawnAtPlayer()
    {
        var player = Main.LocalPlayer;
        int x = (int)(player.Center.X / 16f);
        int y = (int)((player.position.Y + player.height) / 16f);
        Main.spawnTileX = x;
        Main.spawnTileY = y;
        player.SpawnX = x;
        player.SpawnY = y;
        Notices.Post($"出生点 ({x}, {y})");
    }

    private static void TeleportSpawn()
    {
        var player = Main.LocalPlayer;
        var world = new Microsoft.Xna.Framework.Vector2(Main.spawnTileX * 16f, Main.spawnTileY * 16f - player.height);
        player.velocity = Microsoft.Xna.Framework.Vector2.Zero;
        player.Teleport(world, 1);
    }

    private static void SpawnTown(int type)
    {
        if (NPC.AnyNPCs(type))
            return;
        var player = Main.LocalPlayer;
        NPC.NewNPC(new EntitySource_DebugCommand(), (int)player.Center.X, (int)player.Center.Y, type);
    }

    private static void SetBossesDowned(bool downed)
    {
        NPC.downedSlimeKing = downed;
        NPC.downedBoss1 = downed;
        NPC.downedBoss2 = downed;
        NPC.downedBoss3 = downed;
        NPC.downedQueenBee = downed;
        NPC.downedDeerclops = downed;
        NPC.downedQueenSlime = downed;
        NPC.downedMechBoss1 = downed;
        NPC.downedMechBoss2 = downed;
        NPC.downedMechBoss3 = downed;
        NPC.downedMechBossAny = downed;
        NPC.downedPlantBoss = downed;
        NPC.downedGolemBoss = downed;
        NPC.downedFishron = downed;
        NPC.downedEmpressOfLight = downed;
        NPC.downedAncientCultist = downed;
        NPC.downedMoonlord = downed;
        NPC.downedGoblins = downed;
        NPC.downedPirates = downed;
        NPC.downedFrost = downed;
        NPC.downedMartians = downed;
        NPC.downedHalloweenTree = downed;
        NPC.downedHalloweenKing = downed;
        NPC.downedChristmasIceQueen = downed;
        NPC.downedChristmasTree = downed;
        NPC.downedChristmasSantank = downed;
        NPC.downedTowerSolar = downed;
        NPC.downedTowerVortex = downed;
        NPC.downedTowerNebula = downed;
        NPC.downedTowerStardust = downed;
        Notices.Post(downed ? "已标记全部 Boss 击败" : "已清除 Boss 旗");
    }

    private static float GetTimeFraction()
    {
        double cycle = Main.dayTime ? Main.time : 54000.0 + Main.time;
        return (float)(cycle / 86400.0);
    }

    private static void SetTimeFraction(float fraction)
    {
        fraction = Math.Max(0f, Math.Min(1f, fraction));
        double cycle = fraction * 86400.0;
        if (cycle < 54000.0)
            Main.SkipToTime((int)cycle, true);
        else
            Main.SkipToTime((int)(cycle - 54000.0), false);
        CheatState.FrozenTime = Main.time;
    }

    private static string GetTimeString(float fraction)
    {
        double totalSeconds = fraction * 86400.0;
        double clockSeconds = (totalSeconds + 16200.0) % 86400.0;
        int hours = (int)(clockSeconds / 3600.0);
        int minutes = (int)((clockSeconds % 3600.0) / 60.0);
        return $"{hours:D2}:{minutes:D2}";
    }
}
