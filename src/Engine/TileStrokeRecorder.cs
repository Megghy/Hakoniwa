using System.Collections.Generic;
using Hakoniwa.Core;
using Hakoniwa.Engine.Data;
using Hakoniwa.Engine.Tools;
using Microsoft.Xna.Framework.Input;
using Terraria;

namespace Hakoniwa.Engine;

internal static class TileStrokeRecorder
{
    private const int Radius = 32;

    private static readonly TileDataBlock[] _snap = new TileDataBlock[(Radius * 2 + 1) * (Radius * 2 + 1)];
    private static int _ox, _oy, _w, _h;
    private static bool _open;
    private static bool _dirty;

    public static void Before()
    {
        if (Main.gameMenu || Main.tile is null || CheatHooks.BlockGameMouse || !Holding())
        {
            Stop();
            return;
        }

        if (!_open)
        {
            EditorSession.History.Merge();
            _open = true;
            _dirty = false;
        }

        Snapshot();
    }

    public static void After()
    {
        if (!_open)
            return;
        Diff();
        if (!Holding())
            Stop();
    }

    private static bool Holding()
    {
        var mouse = Mouse.GetState();
        return mouse.LeftButton == ButtonState.Pressed || mouse.RightButton == ButtonState.Pressed;
    }

    private static void Stop()
    {
        if (!_open)
            return;
        _open = false;
        EditorSession.History.Seal();
        if (_dirty && EditorSession.History.TryGetLastBounds(out int x, out int y, out int w, out int h))
            WorldTiles.Refresh(x, y, w, h);
        _dirty = false;
    }

    private static void Snapshot()
    {
        EditorSession.CursorTile(out int cx, out int cy);
        _ox = cx - Radius;
        _oy = cy - Radius;
        _w = Radius * 2 + 1;
        _h = _w;
        var world = WorldTiles.Instance;
        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                int wx = _ox + x;
                int wy = _oy + y;
                _snap[y * _w + x] = world.InBounds(wx, wy) ? world.Get(wx, wy) : default;
            }
        }
    }

    private static void Diff()
    {
        var world = WorldTiles.Instance;
        var changes = new List<TileChange>();
        for (int y = 0; y < _h; y++)
        {
            for (int x = 0; x < _w; x++)
            {
                int wx = _ox + x;
                int wy = _oy + y;
                if (!world.InBounds(wx, wy))
                    continue;
                var now = world.Get(wx, wy);
                var before = _snap[y * _w + x];
                if (now != before)
                    changes.Add(new TileChange(wx, wy, before, now));
            }
        }

        if (changes.Count == 0)
            return;
        EditorSession.History.Push(changes);
        _dirty = true;
    }
}
