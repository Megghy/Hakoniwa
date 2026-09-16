using System;
using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameInput;
using Terraria.Graphics.Light;

namespace Hakoniwa.Core;

public static class CheatHooks
{
    public static event Action? PreUpdate;
    public static event Action? PostUpdate;
    public static bool BlockGameMouse;
    public static bool BlockGameKeyboard;

    private static ILightingEngine? _vanillaLighting;
    private static bool _fullBrightApplied;
    private static readonly TimeSpan LogicTick = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
    private static TimeSpan _logicDebt;
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
        hooks.RegisterDetour(Req(typeof(PlayerInput), nameof(PlayerInput.UpdateInput)), UpdateInput);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.ClearHoverItem)), ClearHoverItem);
        hooks.RegisterDetour(Req(typeof(Lighting), nameof(Lighting.LightTiles), typeof(Rectangle)), LightTiles);
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
            self.tileSpeed = 0.01f;
            self.wallSpeed = 0.01f;
            self.pickSpeed = 0.01f;
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
        if (!CheatState.UnlockFps)
        {
            RunLogic(orig, self, time);
            RestoreFpsCap(self);
            return;
        }

        UnlockFps(self);
        _logicDebt += time.ElapsedGameTime;
        if (_logicDebt.Ticks > LogicTick.Ticks * 3)
            _logicDebt = TimeSpan.FromTicks(LogicTick.Ticks * 3);
        if (_logicDebt < LogicTick)
            return;

        while (_logicDebt >= LogicTick)
        {
            _logicDebt -= LogicTick;
            RunLogic(orig, self, new GameTime(time.TotalGameTime, LogicTick));
            UnlockFps(self);
        }
    }

    private static void RunLogic(Action<Main, GameTime> orig, Main self, GameTime time)
    {
        PreUpdate?.Invoke();
        orig(self, time);
        ApplyLighting();
        ApplyTime();
        CheatState.SaveIfDirty();
        PostUpdate?.Invoke();
    }

    private static void UnlockFps(Main self)
    {
        self.IsFixedTimeStep = false;
        var graphics = Main.graphics;
        graphics.SynchronizeWithVerticalRetrace = false;
        if (self.GraphicsDevice.PresentationParameters.PresentationInterval != PresentInterval.Immediate)
            graphics.ApplyChanges();
    }

    private static void RestoreFpsCap(Main self)
    {
        if (self.GraphicsDevice.PresentationParameters.PresentationInterval != PresentInterval.Immediate)
            return;
        Main.graphics.SynchronizeWithVerticalRetrace = true;
        Main.graphics.ApplyChanges();
    }

    private static void UpdateInput(Action orig)
    {
        if (BlockGameKeyboard)
            PlayerInput.WritingText = true;
        orig();
        if (BlockGameKeyboard)
            PlayerInput.WritingText = true;
        if (!BlockGameMouse && !CheatState.SelectHeld(Keyboard.GetState()))
            return;

        PlayerInput.Triggers.Current.MouseLeft = false;
        PlayerInput.Triggers.Current.MouseRight = false;
        PlayerInput.Triggers.JustPressed.MouseLeft = false;
        PlayerInput.Triggers.JustPressed.MouseRight = false;
        Main.mouseLeft = false;
        Main.mouseRight = false;
        Main.blockMouse = true;
        PlayerInput.ScrollWheelDelta = 0;
        PlayerInput.ScrollWheelDeltaForUI = 0;
        if (Main.myPlayer >= 0 && Main.player[Main.myPlayer].active)
            Main.player[Main.myPlayer].mouseInterface = true;
    }

    private static void ClearHoverItem(Action orig)
    {
        orig();
        if (!BlockGameMouse || Main.myPlayer < 0)
            return;
        var player = Main.player[Main.myPlayer];
        if (!player.active)
            return;
        player.mouseInterface = true;
        Main.blockMouse = true;
    }

    private static void LightTiles(Action<Rectangle> orig, Rectangle area)
    {
        ApplyLighting();
        orig(area);
    }

    private static void ApplyLighting()
    {
        if (CheatState.FullBright)
        {
            _vanillaLighting ??= Lighting._activeEngine;
            Lighting._activeEngine = FullbrightEngine.Instance;
            if (!_fullBrightApplied)
            {
                _fullBrightApplied = true;
                FullbrightEngine.Instance.Rebuild();
                Main.renderCount = 0;
            }
            return;
        }

        if (_fullBrightApplied)
        {
            if (_vanillaLighting is not null)
                Lighting._activeEngine = _vanillaLighting;
            _vanillaLighting = null;
            _fullBrightApplied = false;
            Main.renderCount = 0;
        }
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
