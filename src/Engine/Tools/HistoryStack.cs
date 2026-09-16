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

    public void Push(IReadOnlyList<TileChange> changes)
    {
        if (changes is null)
            throw new ArgumentNullException(nameof(changes));
        if (changes.Count == 0)
            return;

        var snapshot = new TileChange[changes.Count];
        for (int i = 0; i < changes.Count; i++)
            snapshot[i] = changes[i];

        if (_redoDepth > 0)
        {
            _count -= _redoDepth;
            _redoDepth = 0;
        }

        if (_count == _slots.Length)
        {
            _slots[_oldest] = snapshot;
            _oldest = (_oldest + 1) % _slots.Length;
            return;
        }

        _slots[(_oldest + _count) % _slots.Length] = snapshot;
        _count++;
    }

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
