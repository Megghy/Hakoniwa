using Microsoft.Xna.Framework;
using Terraria.Graphics.Light;

namespace Hakoniwa.Core;

public sealed class FullbrightEngine : ILightingEngine
{
    public static readonly FullbrightEngine Instance = new();

    public void Rebuild() { }
    public void AddLight(int x, int y, Vector3 color) { }
    public void ProcessArea(Rectangle area) { }
    public Vector3 GetColor(int x, int y) => Vector3.One;
    public void Clear() { }
}
