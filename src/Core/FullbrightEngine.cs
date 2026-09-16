using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Graphics.Light;

namespace Hakoniwa.Core;

public sealed class FullbrightEngine : ILightingEngine
{
    public static readonly FullbrightEngine Instance = new();

    private int _phase;

    public void Rebuild() => _phase = 0;
    public void AddLight(int x, int y, Vector3 color) { }
    public Vector3 GetColor(int x, int y) => Vector3.One;
    public void Clear() { }

    public void ProcessArea(Rectangle area)
    {
        if (_phase == 1)
            Main.UpdateSceneMetrics();
        Main.renderCount = _phase switch
        {
            0 => 3,
            1 => 0,
            2 => 1,
            _ => 2,
        };
        _phase = (_phase + 1) & 3;
    }
}
