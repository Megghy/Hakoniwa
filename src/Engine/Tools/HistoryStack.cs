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

    public bool Undo(ITileGrid world)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (!CanUndo)
            return false;

        var changes = _slots[(_oldest + Count - 1) % _slots.Length];
        for (int i = changes.Length - 1; i >= 0; i--)
            world.Set(changes[i].X, changes[i].Y, changes[i].Before);

        _redoDepth++;
        return true;
    }

    public bool Redo(ITileGrid world)
    {
        if (world is null)
            throw new ArgumentNullException(nameof(world));
        if (!CanRedo)
            return false;

        var changes = _slots[(_oldest + Count) % _slots.Length];
        for (int i = 0; i < changes.Length; i++)
            world.Set(changes[i].X, changes[i].Y, changes[i].After);

        _redoDepth--;
        return true;
    }
}
