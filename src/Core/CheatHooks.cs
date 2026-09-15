using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.Graphics.Light;

namespace Hakoniwa.Core;

public static class CheatHooks
{
    public static event Action? PostUpdate;

    private static ILightingEngine? _vanillaLighting;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void Install(HookManager hooks)
    {
        if (hooks is null)
            throw new ArgumentNullException(nameof(hooks));

        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.Hurt), typeof(PlayerDeathReason), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(int), typeof(bool)), Hurt);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.KillMe), typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool)), KillMe);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.ConsumeItem), typeof(int), typeof(bool), typeof(bool)), ConsumeItem);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.ItemCheck)), ItemCheck);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.ResetEffects)), ResetEffects);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.IsInTileInteractionRange), typeof(int), typeof(int), typeof(TileReachCheckSettings), typeof(int)), InRange);
        hooks.RegisterDetour(Req(typeof(WorldGen), nameof(WorldGen.PlaceTile), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int), typeof(int)), PlaceTile);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.Update), typeof(GameTime)), Update);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.SetTitle), typeof(bool)), SetTitle);
    }

    private static MethodInfo Req(Type type, string name, params Type[] args)
    {
        var method = type.GetMethod(name, Flags | BindingFlags.DeclaredOnly, null, args, null)
            ?? type.GetMethod(name, Flags, null, args, null);
        if (method is null)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }

    private static double Hurt(Func<Player, PlayerDeathReason, int, int, bool, bool, bool, int, bool, double> orig, Player self, PlayerDeathReason source, int damage, int hitDirection, bool pvp, bool quiet, bool crit, int cooldown, bool dodgeable)
    {
        if (CheatState.GodMode && self.whoAmI == Main.myPlayer)
            return 0;
        return orig(self, source, damage, hitDirection, pvp, quiet, crit, cooldown, dodgeable);
    }

    private static void KillMe(Action<Player, PlayerDeathReason, double, int, bool> orig, Player self, PlayerDeathReason source, double dmg, int hitDirection, bool pvp)
    {
        if (CheatState.GodMode && self.whoAmI == Main.myPlayer)
            return;
        orig(self, source, dmg, hitDirection, pvp);
    }

    private static bool ConsumeItem(Func<Player, int, bool, bool, bool> orig, Player self, int type, bool reverseOrder, bool includeVoidBag)
    {
        if (CheatState.InfiniteItems && self.whoAmI == Main.myPlayer)
            return true;
        return orig(self, type, reverseOrder, includeVoidBag);
    }

    private static void ItemCheck(Action<Player> orig, Player self)
    {
        int type = self.inventory[self.selectedItem].type;
        int stack = self.inventory[self.selectedItem].stack;
        int mouseType = Main.mouseItem.type;
        int mouseStack = Main.mouseItem.stack;
        orig(self);
        if (!CheatState.InfiniteItems || self.whoAmI != Main.myPlayer)
            return;
        Restore(self.inventory[self.selectedItem], type, stack);
        Restore(Main.mouseItem, mouseType, mouseStack);
    }

    private static void Restore(Item item, int type, int stack)
    {
        if (type == 0)
            return;
        if (item.type != type)
            item.SetDefaults(type);
        item.stack = stack;
    }

    private static void ResetEffects(Action<Player> orig, Player self)
    {
        orig(self);
        if (self.whoAmI != Main.myPlayer)
            return;
        if (CheatState.InfiniteReach)
        {
            Player.tileRangeX = 1000;
            Player.tileRangeY = 1000;
            self.blockRange = 1000;
        }

        if (!CheatState.GodMode)
            return;
        self.statLife = self.statLifeMax2;
        self.statMana = self.statManaMax2;
        self.immune = true;
        self.immuneTime = 2;
        self.breath = self.breathMax;
    }

    private static bool InRange(Func<Player, int, int, TileReachCheckSettings, int, bool> orig, Player self, int targetX, int targetY, TileReachCheckSettings settings, int extra)
    {
        if (CheatState.InfiniteReach && self.whoAmI == Main.myPlayer)
            return true;
        return orig(self, targetX, targetY, settings, extra);
    }

    private static bool PlaceTile(Func<int, int, int, bool, bool, int, int, bool> orig, int i, int j, int type, bool mute, bool forced, int plr, int style)
    {
        if (CheatState.FreePlacement)
            forced = true;
        return orig(i, j, type, mute, forced, plr, style);
    }

    private static void Update(Action<Main, GameTime> orig, Main self, GameTime time)
    {
        orig(self, time);
        ApplyLighting();
        ApplyTime();
        PostUpdate?.Invoke();
    }

    private static void ApplyLighting()
    {
        if (CheatState.FullBright)
        {
            _vanillaLighting ??= Lighting._activeEngine;
            if (!ReferenceEquals(Lighting._activeEngine, FullbrightEngine.Instance))
                Lighting._activeEngine = FullbrightEngine.Instance;
            return;
        }

        if (_vanillaLighting is not null && ReferenceEquals(Lighting._activeEngine, FullbrightEngine.Instance))
            Lighting._activeEngine = _vanillaLighting;
        _vanillaLighting = null;
    }

    private static void ApplyTime()
    {
        if (CheatState.FreezeTime)
            Main.time = CheatState.FrozenTime;
        else
            CheatState.FrozenTime = Main.time;
    }

    private static void SetTitle(Action<Main, bool> orig, Main self, bool initialSetup)
    {
        orig(self, initialSetup);
        if (Main.dedServ)
            return;
        const string mark = " Hakoniwa";
        string title = self._cachedTitle;
        if (!title.EndsWith(mark, StringComparison.Ordinal))
            title += mark;
        self._cachedTitle = title;
        SetWindowText(self.Window.Handle, title);
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern bool SetWindowText(IntPtr hWnd, string lpString);
}
