namespace Hakoniwa.Engine;

public sealed class Selection
{
    public bool Active { get; set; }
    public int StartX { get; set; }
    public int StartY { get; set; }
    public int EndX { get; set; }
    public int EndY { get; set; }

    public int MinX => StartX < EndX ? StartX : EndX;
    public int MinY => StartY < EndY ? StartY : EndY;
    public int MaxX => StartX > EndX ? StartX : EndX;
    public int MaxY => StartY > EndY ? StartY : EndY;
    public int Width => Active ? MaxX - MinX + 1 : 0;
    public int Height => Active ? MaxY - MinY + 1 : 0;

    public void Begin(int x, int y)
    {
        StartX = EndX = x;
        StartY = EndY = y;
        Active = true;
    }

    public void DragTo(int x, int y)
    {
        if (!Active)
            Begin(x, y);
        else
        {
            EndX = x;
            EndY = y;
        }
    }

    public void Clear() => Active = false;

    public bool Contains(int x, int y) =>
        Active && x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

    public void Set(int x0, int y0, int x1, int y1)
    {
        StartX = x0;
        StartY = y0;
        EndX = x1;
        EndY = y1;
        Active = true;
    }

    public void Offset(int dx, int dy)
    {
        if (!Active || (dx == 0 && dy == 0))
            return;
        StartX += dx;
        EndX += dx;
        StartY += dy;
        EndY += dy;
    }
}
