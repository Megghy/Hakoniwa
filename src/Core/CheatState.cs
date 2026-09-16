using System;
using System.IO;
using System.Text.Json;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace Hakoniwa.Core;

public static class CheatState
{
    public static bool GodMode = true;
    public static bool InfiniteReach = true;
    public static bool InfiniteItems = true;
    public static bool FreePlacement = true;
    public static bool FullBright;
    public static bool FreezeTime;
    public static bool ClickTeleport = true;
    public static bool UnlockFps = true;
    public static double FrozenTime;
    public static Keys SelectModifier = Keys.LeftControl;
    public static bool WaitingSelectKey;
    public static NotifyCorner NotifyAnchor = NotifyCorner.右下;
    public static float NotifyScale = 1f;

    private static string? _saved;

    public static bool SelectHeld(KeyboardState kb)
    {
        var key = SelectModifier;
        if (key is Keys.LeftControl or Keys.RightControl)
            return kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl);
        if (key is Keys.LeftShift or Keys.RightShift)
            return kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift);
        if (key is Keys.LeftAlt or Keys.RightAlt)
            return kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt);
        return kb.IsKeyDown(key);
    }

    public static void Load()
    {
        string path = PathFor();
        if (!File.Exists(path))
        {
            _saved = JsonSerializer.Serialize(Capture());
            return;
        }

        var data = JsonSerializer.Deserialize<Data>(File.ReadAllText(path))
            ?? throw new InvalidDataException($"Invalid settings file: {path}");
        GodMode = data.GodMode;
        InfiniteReach = data.InfiniteReach;
        InfiniteItems = data.InfiniteItems;
        FreePlacement = data.FreePlacement;
        FullBright = data.FullBright;
        FreezeTime = data.FreezeTime;
        ClickTeleport = data.ClickTeleport;
        UnlockFps = data.UnlockFps;
        if (data.SelectModifier != 0)
            SelectModifier = (Keys)data.SelectModifier;
        NotifyAnchor = (NotifyCorner)data.NotifyAnchor;
        if (data.NotifyScale > 0f)
            NotifyScale = data.NotifyScale;
        _saved = JsonSerializer.Serialize(Capture());
    }

    public static void SaveIfDirty()
    {
        var data = Capture();
        string now = JsonSerializer.Serialize(data);
        if (now == _saved)
            return;
        _saved = now;
        File.WriteAllText(PathFor(), JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static Data Capture() => new()
    {
        GodMode = GodMode,
        InfiniteReach = InfiniteReach,
        InfiniteItems = InfiniteItems,
        FreePlacement = FreePlacement,
        FullBright = FullBright,
        FreezeTime = FreezeTime,
        ClickTeleport = ClickTeleport,
        UnlockFps = UnlockFps,
        SelectModifier = (int)SelectModifier,
        NotifyAnchor = (int)NotifyAnchor,
        NotifyScale = NotifyScale,
    };

    private static string PathFor()
    {
        string dir = Terraria.Program.SavePath;
        if (string.IsNullOrEmpty(dir))
            throw new InvalidOperationException("Terraria SavePath is not initialized.");
        return Path.Combine(dir, "hakoniwa.json");
    }

    private sealed class Data
    {
        public bool GodMode { get; set; }
        public bool InfiniteReach { get; set; }
        public bool InfiniteItems { get; set; }
        public bool FreePlacement { get; set; }
        public bool FullBright { get; set; }
        public bool FreezeTime { get; set; }
        public bool ClickTeleport { get; set; }
        public bool UnlockFps { get; set; } = true;
        public int SelectModifier { get; set; } = (int)Keys.LeftControl;
        public int NotifyAnchor { get; set; } = (int)NotifyCorner.右下;
        public float NotifyScale { get; set; } = 1f;
    }
}
