using Terraria;
using Terraria.ID;
using Terraria.Testing;

namespace Hakoniwa.Core;

public static class MapReveal
{
    private const int RequestsPerTick = 1;
    private static bool[]? _asked;
    private static bool _active;

    public static void Reveal()
    {
        if (Main.gameMenu || Main.Map is null)
            return;
        Main.clearMap = true;
        DebugOptions.unlockMap = 1;
        Main.refreshMap = true;
        Main.sectionManager.ClearMapDraw();
        _asked = new bool[Main.maxSectionsX * Main.maxSectionsY];
        _active = Main.netMode == 1;
        Notices.Post(_active ? "正在点亮地图（联机会拉取未加载区块）" : "正在点亮地图");
    }

    public static void Clear()
    {
        if (Main.gameMenu || Main.Map is null)
            return;
        _active = false;
        DebugOptions.unlockMap = 0;
        Main.clearMap = true;
        Main.Map.Clear();
        Main.refreshMap = true;
        Notices.Post("已清除地图探索");
    }

    public static void Tick()
    {
        if (!_active || Main.gameMenu || Main.netMode != 1)
            return;
        int sent = 0;
        int pending = 0;
        int w = Main.maxSectionsX;
        int h = Main.maxSectionsY;
        var asked = _asked ??= new bool[w * h];
        if (asked.Length != w * h)
            asked = _asked = new bool[w * h];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                if (Main.sectionManager.SectionLoaded(x, y))
                    continue;
                int i = y * w + x;
                if (asked[i])
                {
                    pending++;
                    continue;
                }
                if (sent >= RequestsPerTick)
                {
                    pending++;
                    continue;
                }
                asked[i] = true;
                NetMessage.SendData(MessageID.RequestSection, -1, -1, null, x, y);
                sent++;
                pending++;
            }
        }
        if (pending == 0)
        {
            _active = false;
            DebugOptions.unlockMap = 1;
            Main.refreshMap = true;
            Notices.Post("地图已点亮");
            return;
        }
        if (DebugOptions.unlockMap == 0)
        {
            DebugOptions.unlockMap = 1;
            Main.refreshMap = true;
        }
    }
}
