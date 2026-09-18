using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System;

using Hakoniwa.Engine;
using Terraria.DataStructures;
using Terraria.GameContent.UI.Chat;
using Terraria.GameInput;
using Terraria.Graphics.Light;
using Terraria.IO;
using Terraria;

namespace Hakoniwa.Core;

public static class CheatHooks
{
    public static event Action? PreUpdate;
    public static event Action? PostUpdate;
    public static event Action? LocalItemCheckBegin;
    public static event Action? LocalItemCheckEnd;
    public static bool BlockGameMouse;
    public static bool BlockGameScroll;
    public static bool BlockGameKeyboard;
    public static bool WantTextInput;
    public static bool HideVanillaChat;
    public static bool HideVanillaCursor;

    private static ILightingEngine? _vanillaLighting;
    private static bool _fullBrightApplied;
    private static readonly Stopwatch LogicClock = Stopwatch.StartNew();
    private static long _nextLogicTick;
    private static bool _prepHooked;
    private static int _cappedHz;
    private static int _refreshHz;
    private static long _refreshHzAt;
    private static long _menuFxSlot = -1;
    private static int _menuFxCount;
    private const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    public static void Install(HookManager hooks)
    {
        if (hooks is null)
            throw new ArgumentNullException(nameof(hooks));

        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.Hurt), typeof(PlayerDeathReason), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(bool), typeof(int), typeof(bool)), Hurt);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.KillMe), typeof(PlayerDeathReason), typeof(double), typeof(int), typeof(bool)), KillMe);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.ConsumeItem), typeof(int), typeof(bool), typeof(bool)), ConsumeItem);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.ItemCheck)), ItemCheck);
        hooks.RegisterDetour(
            Req(typeof(Player), nameof(Player.PlaceThing), typeof(bool), typeof(Player.ItemCheckContext).MakeByRefType()),
            new PlaceThingHook(PlaceThing));
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.ResetEffects)), ResetEffects);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.DryCollision), typeof(bool), typeof(bool)), DryCollision);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.WetCollision), typeof(bool), typeof(bool), typeof(float)), WetCollision);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.SlopingCollision), typeof(bool), typeof(bool)), SlopingCollision);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.TryBouncingBlocks), typeof(bool)), TryBouncingBlocks);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.IsInTileInteractionRange), typeof(int), typeof(int), typeof(TileReachCheckSettings), typeof(int)), InRange);
        hooks.RegisterDetour(Req(typeof(Player), "PlaceThing_Tiles_BlockPlacementForAssortedThings", typeof(bool)), BlockPlacementForAssortedThings);
        hooks.RegisterDetour(Req(typeof(Player), "PlaceThing_Walls"), PlaceThingWalls);
        hooks.RegisterDetour(Req(typeof(WorldGen), nameof(WorldGen.PlaceTile), typeof(int), typeof(int), typeof(int), typeof(bool), typeof(bool), typeof(int), typeof(int)), PlaceTile);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.Update), typeof(GameTime)), Update);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.SetTitle), typeof(bool)), SetTitle);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.UpdateDisplaySettings)), UpdateDisplaySettings);
        hooks.RegisterDetour(Req(typeof(PlayerInput), nameof(PlayerInput.UpdateInput)), UpdateInput);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.HandleIME)), HandleIME);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.DrawIMEPanel)), DrawIMEPanel);
        hooks.RegisterDetour(Req(typeof(Main), nameof(Main.ClearHoverItem)), ClearHoverItem);
        hooks.RegisterDetour(Req(typeof(Main), "DrawInterface_36_Cursor"), DrawInterfaceCursor);
        hooks.RegisterDetour(Req(typeof(Player), nameof(Player.SavePlayer), typeof(PlayerFileData), typeof(bool), typeof(bool)), SavePlayer);
        hooks.RegisterDetour(Req(typeof(Lighting), nameof(Lighting.LightTiles), typeof(Rectangle)), LightTiles);
        hooks.RegisterDetour(Req(typeof(RemadeChatMonitor), nameof(RemadeChatMonitor.DrawChat), typeof(bool)), DrawVanillaChat);
        hooks.RegisterDetour(Req(typeof(Cloud), nameof(Cloud.UpdateClouds)), UpdateClouds);
        hooks.RegisterDetour(Req(typeof(Star), nameof(Star.UpdateStars)), UpdateStars);
    }

    private static void DrawInterfaceCursor(Action orig)
    {
        if (!HideVanillaCursor)
            orig();
    }

    private static void DrawVanillaChat(Action<RemadeChatMonitor, bool> orig, RemadeChatMonitor self, bool drawing)
    {
        if (!HideVanillaChat)
            orig(self, drawing);
    }

    private static void SavePlayer(Action<PlayerFileData, bool, bool> orig, PlayerFileData playerFile, bool skipMapSave, bool canBeSkipped)
    {
        var player = playerFile.Player;
        if (player is null || player.whoAmI != Main.myPlayer)
        {
            orig(playerFile, skipMapSave, canBeSkipped);
            return;
        }

        CharacterPacks.WriteThrough(player, () => orig(playerFile, skipMapSave, canBeSkipped));
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

    private delegate void PlaceThingOrig(Player self, bool doPlacementAction, ref Player.ItemCheckContext context);
    private delegate void PlaceThingHook(PlaceThingOrig orig, Player self, bool doPlacementAction, ref Player.ItemCheckContext context);

    private static void PlaceThing(PlaceThingOrig orig, Player self, bool doPlacementAction, ref Player.ItemCheckContext context)
    {
        Item item = self.inventory[self.selectedItem];
        if (CheatState.InfiniteReach && self.whoAmI == Main.myPlayer && (item.createTile >= 0 || item.createWall > 0))
            self.itemTime = 0;
        orig(self, doPlacementAction, ref context);
    }

    private static bool BlockPlacementForAssortedThings(Func<Player, bool, bool> orig, Player self, bool canPlace)
    {
        if (orig(self, canPlace))
            return true;
        return CheatState.FreePlacement && self.whoAmI == Main.myPlayer;
    }

    private static void PlaceThingWalls(Action<Player> orig, Player self)
    {
        orig(self);
        if (!CheatState.FreePlacement || self.whoAmI != Main.myPlayer)
            return;
        Item item = self.inventory[self.selectedItem];
        int x = Player.tileTargetX;
        int y = Player.tileTargetY;
        if (item.createWall <= 0 || !self.ItemTimeIsZero || self.itemAnimation <= 0 || !self.controlUseItem)
            return;
        if (Main.tile[x, y].wall == item.createWall)
            return;
        if (!self.IsInTileInteractionRange(x, y, TileReachCheckSettings.Simple, item.tileBoost + self.blockRange))
            return;
        WorldGen.PlaceWall(x, y, item.createWall);
        if (Main.tile[x, y].wall != item.createWall)
            return;
        self.ApplyItemTime(item, self.wallSpeed);
        if (Main.netMode == 1)
            NetMessage.SendData(17, -1, -1, null, 3, x, y, item.createWall);
    }

    private static void ItemCheck(Action<Player> orig, Player self)
    {
        int type = self.inventory[self.selectedItem].type;
        int stack = self.inventory[self.selectedItem].stack;
        int mouseType = Main.mouseItem.type;
        int mouseStack = Main.mouseItem.stack;
        if (self.whoAmI == Main.myPlayer)
            LocalItemCheckBegin?.Invoke();
        orig(self);
        if (self.whoAmI == Main.myPlayer)
            LocalItemCheckEnd?.Invoke();
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

        if (CheatState.NoClip)
            self.noFallDmg = true;

        if (!CheatState.GodMode)
            return;
        self.statLife = self.statLifeMax2;
        self.breath = self.breathMax;
        for (int i = 0; i < Player.maxBuffs; i++)
        {
            int type = self.buffType[i];
            if (type > 0 && type < Main.debuff.Length && Main.debuff[type])
            {
                self.DelBuff(i);
                i--;
            }
        }

        for (int i = 0; i < Main.debuff.Length; i++)
        {
            if (Main.debuff[i])
                self.buffImmune[i] = true;
        }
    }

    private static void DryCollision(Action<Player, bool, bool> orig, Player self, bool fallThrough, bool ignorePlats)
    {
        if (NoClipStep(self))
            return;
        orig(self, fallThrough, ignorePlats);
    }

    private static void WetCollision(Action<Player, bool, bool, float> orig, Player self, bool fallThrough, bool ignorePlats, float movementSpeed)
    {
        if (NoClipStep(self))
            return;
        orig(self, fallThrough, ignorePlats, movementSpeed);
    }

    private static void SlopingCollision(Action<Player, bool, bool> orig, Player self, bool fallThrough, bool ignorePlats)
    {
        if (CheatState.NoClip && self.whoAmI == Main.myPlayer)
            return;
        orig(self, fallThrough, ignorePlats);
    }

    private static void TryBouncingBlocks(Action<Player, bool> orig, Player self, bool falling)
    {
        if (CheatState.NoClip && self.whoAmI == Main.myPlayer)
            return;
        orig(self, falling);
    }

    private static bool NoClipStep(Player self)
    {
        if (!CheatState.NoClip || self.whoAmI != Main.myPlayer)
            return false;
        if (self.controlJump)
        {
            // 与原版 WingMovement 默认上升上限一致（jumpSpeed * 1.5）；已有更快的翅膀速度则保留。
            float up = -Player.jumpSpeed * 1.5f * self.gravDir;
            self.velocity.Y = self.gravDir > 0f ? Math.Min(self.velocity.Y, up) : Math.Max(self.velocity.Y, up);
        }

        self.position += self.velocity;
        float pad = 640f;
        self.position.X = MathHelper.Clamp(self.position.X, Main.leftWorld + pad, Main.rightWorld - pad - self.width);
        self.position.Y = MathHelper.Clamp(self.position.Y, Main.topWorld + pad, Main.bottomWorld - pad - self.height);
        return true;
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

    private static void UpdateDisplaySettings(Action<Main> orig, Main self)
    {
        // 原版发现 VSync 关着就会 ApplyChanges，XNA 会释放全部世界 RT 且不重建。
        if (CheatState.UnlockFps)
            Main.graphics.SynchronizeWithVerticalRetrace = true;
        orig(self);
    }

    private static void Update(Action<Main, GameTime> orig, Main self, GameTime time)
    {
        ApplyFpsUnlock();
        if (!CheatState.UnlockFps)
        {
            RunLogic(orig, self, time);
            if (_cappedHz != 0)
            {
                self.TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
                self.IsFixedTimeStep = true;
                _cappedHz = 0;
            }
            return;
        }

        long now = LogicClock.ElapsedTicks;
        long step = Stopwatch.Frequency / 60;
        if (_nextLogicTick == 0)
            _nextLogicTick = now;
        if (now >= _nextLogicTick)
        {
            int runs = 0;
            while (now >= _nextLogicTick && runs < 3)
            {
                _nextLogicTick += step;
                RunLogic(orig, self, time);
                runs++;
            }
            if (runs == 3)
                _nextLogicTick = now + step;
        }

        // 原版 DoUpdate 会把 IsFixedTimeStep 改回 false，必须在 Update 结束时写回去，下一拍 Tick 才能按刷新率限帧。
        ApplyUnlockTiming(self);
    }

    private static void RunLogic(Action<Main, GameTime> orig, Main self, GameTime time)
    {
        PreUpdate?.Invoke();
        orig(self, time);
        ApplyGodFlight();
        CharacterPacks.Tick();
        MapReveal.Tick();
        ApplyLighting();
        ApplyTime();
        CheatState.SaveIfDirty();
        PostUpdate?.Invoke();
    }

    private static void ApplyGodFlight()
    {
        if (!CheatState.GodMode)
            return;
        var player = Main.LocalPlayer;
        if (!player.active)
            return;
        player.wingTime = player.wingTimeMax;
        player.rocketTime = player.rocketTimeMax;
    }

    public static void ApplyFpsUnlock()
    {
        if (Main.graphics is null)
            return;
        if (!_prepHooked)
        {
            _prepHooked = true;
            Main.graphics.PreparingDeviceSettings += (_, e) =>
            {
                e.GraphicsDeviceInformation.PresentationParameters.PresentationInterval =
                    CheatState.UnlockFps ? PresentInterval.Immediate : PresentInterval.One;
            };
        }

        var device = Main.instance.GraphicsDevice;
        if (device is null || device.IsDisposed)
            return;
        var want = CheatState.UnlockFps ? PresentInterval.Immediate : PresentInterval.One;
        if (device.PresentationParameters.PresentationInterval == want)
            return;

        // 管理器属性必须保持 true：原版 UpdateDisplaySettings 见 false 会 ApplyChanges，XNA 释放世界 RT。
        Main.graphics.SynchronizeWithVerticalRetrace = true;
        Main.graphics.ApplyChanges();
        Main.instance.InitTargets();
    }

    private static void ApplyUnlockTiming(Main self)
    {
        int hz = RefreshHz();
        _cappedHz = hz;
        self.IsFixedTimeStep = true;
        self.TargetElapsedTime = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / hz);
    }

    private static bool MenuFxDue()
    {
        if (!CheatState.UnlockFps || !Main.gameMenu)
            return true;
        long slot = LogicClock.ElapsedTicks / (Stopwatch.Frequency / 60);
        if (slot != _menuFxSlot)
        {
            _menuFxSlot = slot;
            _menuFxCount = 0;
        }

        _menuFxCount++;
        return _menuFxCount <= 2;
    }

    private static void UpdateClouds(Action orig)
    {
        if (MenuFxDue())
            orig();
    }

    private static void UpdateStars(Action orig)
    {
        if (MenuFxDue())
            orig();
    }

    private static int RefreshHz()
    {
        long now = LogicClock.ElapsedMilliseconds;
        if (_refreshHz != 0 && now - _refreshHzAt < 1000)
            return _refreshHz;
        IntPtr dc = GetDC(IntPtr.Zero);
        int hz = GetDeviceCaps(dc, 116);
        ReleaseDC(IntPtr.Zero, dc);
        _refreshHz = hz < 30 ? 60 : hz;
        _refreshHzAt = now;
        return _refreshHz;
    }

    private static void HandleIME(Action<Main> orig, Main self)
    {
        if (WantTextInput || NativeTextInput.Busy)
            PlayerInput.WritingText = true;
        orig(self);
        if (BlockGameKeyboard || NativeTextInput.Busy)
            PlayerInput.WritingText = true;
    }

    private static void DrawIMEPanel(Action<Main> orig, Main self)
    {
        int w = Main.screenWidth;
        int h = Main.screenHeight;
        int mx = Main.mouseX;
        int my = Main.mouseY;
        int lx = Main.lastMouseX;
        int ly = Main.lastMouseY;
        orig(self);
        // 只撤销 DrawIMEPanel 自己的 SetZoom_UI，不要整帧强制 Unscaled，否则画面底部会空出一条。
        Main.screenWidth = w;
        Main.screenHeight = h;
        Main.mouseX = mx;
        Main.mouseY = my;
        Main.lastMouseX = lx;
        Main.lastMouseY = ly;
    }

    private static void UpdateInput(Action orig)
    {
        if (BlockGameKeyboard || NativeTextInput.Busy)
            PlayerInput.WritingText = true;
        orig();
        if (BlockGameKeyboard || NativeTextInput.Busy)
            PlayerInput.WritingText = true;
        if (WantTextInput || NativeTextInput.Busy)
            Main.instance.HandleIME();

        var kb = Keyboard.GetState();
        bool isCtrl = kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        if (BlockGameScroll || isCtrl && (EditorSession.Tool is EditorTool.Brush or EditorTool.Eraser))
        {
            PlayerInput.ScrollWheelDelta = 0;
            PlayerInput.ScrollWheelDeltaForUI = 0;
        }

        if (!BlockGameMouse)
            return;

        PlayerInput.Triggers.Current.MouseLeft = false;
        PlayerInput.Triggers.Current.MouseRight = false;
        PlayerInput.Triggers.JustPressed.MouseLeft = false;
        PlayerInput.Triggers.JustPressed.MouseRight = false;
        Main.mouseLeft = false;
        Main.mouseRight = false;
        Main.blockMouse = true;
    }

    private static void ClearHoverItem(Action orig)
    {
        orig();
        if (BlockGameMouse)
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

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetDeviceCaps(IntPtr hdc, int index);
}
