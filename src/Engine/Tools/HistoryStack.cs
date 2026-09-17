using System;
using System.Collections.Generic;
using Hakoniwa.Core;
using Hakoniwa.Engine.Data;

namespace Hakoniwa.Engine.Tools;

public readonly struct TileChange
{
    public readonly int X;
    public readonly int Y;
    public readonly TileDataBlock Before;
    public readonly TileDataBlock After;

    public TileChange(int x, int y, in TileDataBlock before, in TileDataBlock after)
    {
        X = x;
        Y = y;
        Before = before;
        After = after;
    }
}

public sealed class HistoryStack
{
    private readonly TileChange[][] _slots;
    private int _oldest;
    private int _count;
    private int _redoDepth;
    private bool _merge;
    private bool _opened;

    public HistoryStack(int capacity = 64)
    {
        if (capacity <= 0)
            throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "Capacity must be positive.");
        _slots = new TileChange[capacity][];
    }

    public int Capacity => _slots.Length;
    public int Count => _count - _redoDepth;
    public bool CanUndo => Count > 0;
    public bool CanRedo => _redoDepth > 0;

    public void Merge()
    {
        _merge = true;
        _opened = false;
    }

    public void Seal() => _merge = false;

    public void Push(IReadOnlyList<TileChange> changes)
    {
        if (changes is null)
            throw new ArgumentNullException(nameof(changes));
        if (changes.Count == 0)
            return;

        var snapshot = new TileChange[changes.Count];
        for (int i = 0; i < changes.Count; i++)
            snapshot[i] = changes[i];

        if (_merge && _opened && Count > 0)
        {
            Absorb(snapshot);
            return;
        }

        if (_redoDepth > 0)
        {
            _count -= _redoDepth;
            _redoDepth = 0;
        }

        if (_count == _slots.Length)
        {
            _slots[_oldest] = snapshot;
            _oldest = (_oldest + 1) % _slots.Length;
        }
        else
        {
            _slots[(_oldest + _count) % _slots.Length] = snapshot;
            _count++;
        }

        if (_merge)
            _opened = true;
    }

    private void Absorb(TileChange[] incoming)
    {
        int index = (_oldest + Count - 1) % _slots.Length;
        var last = _slots[index];
        var map = new Dictionary<long, int>(last.Length);
        var list = new List<TileChange>(last.Length + incoming.Length);
        for (int i = 0; i < last.Length; i++)
        {
            map[Key(last[i].X, last[i].Y)] = i;
            list.Add(last[i]);
        }

        foreach (var change in incoming)
        {
            long key = Key(change.X, change.Y);
            if (map.TryGetValue(key, out int at))
                list[at] = new TileChange(change.X, change.Y, list[at].Before, change.After);
            else
            {
                map[key] = list.Count;
                list.Add(change);
            }
        }

        list.RemoveAll(static c => c.Before == c.After);
        _slots[index] = list.ToArray();
    }

    private static long Key(int x, int y) => ((long)x << 32) | (uint)y;

    public bool Undo(ITileGrid world) => Undo(world, out _, out _, out _, out _);

    public bool Undo(ITileGrid world, out int x, out int y, out int width, out int height)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        x = y = width = height = 0;
        if (!CanUndo)
            return false;

        var changes = _slots[(_oldest + Count - 1) % _slots.Length];
        for (int i = changes.Length - 1; i >= 0; i--)
            world.Set(changes[i].X, changes[i].Y, changes[i].Before);

        _redoDepth++;
        Bounds(changes, out x, out y, out width, out height);
        return true;
    }

    public bool Redo(ITileGrid world) => Redo(world, out _, out _, out _, out _);

    public bool Redo(ITileGrid world, out int x, out int y, out int width, out int height)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        x = y = width = height = 0;
        if (!CanRedo)
            return false;

        var changes = _slots[(_oldest + Count) % _slots.Length];
        for (int i = 0; i < changes.Length; i++)
            world.Set(changes[i].X, changes[i].Y, changes[i].After);

        _redoDepth--;
        Bounds(changes, out x, out y, out width, out height);
        return true;
    }

    public bool TryGetLastBounds(out int x, out int y, out int width, out int height)
    {
        x = y = width = height = 0;
        if (Count <= 0)
            return false;
        Bounds(_slots[(_oldest + Count - 1) % _slots.Length], out x, out y, out width, out height);
        return true;
    }

    private static void Bounds(TileChange[] changes, out int x, out int y, out int width, out int height)
    {
        if (changes.Length == 0)
        {
            x = y = width = height = 0;
            return;
        }

        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        for (int i = 0; i < changes.Length; i++)
        {
            int cx = changes[i].X;
            int cy = changes[i].Y;
            if (cx < minX) minX = cx;
            if (cy < minY) minY = cy;
            if (cx > maxX) maxX = cx;
            if (cy > maxY) maxY = cy;
        }

        x = minX;
        y = minY;
        width = maxX - minX + 1;
        height = maxY - minY + 1;
    }
}
