using Hakoniwa.Engine.Data;

namespace Hakoniwa.Core;

public interface ITileGrid
{
    int Width { get; }
    int Height { get; }
    bool InBounds(int x, int y);
    TileDataBlock Get(int x, int y);
    void Set(int x, int y, in TileDataBlock tile);
}
