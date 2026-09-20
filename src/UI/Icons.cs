using Hexa.NET.ImGui;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public static class Icons
{
    public const float Size = 24f;

    public const string Archive = "\uEA58";
    public const string Box = "\uEAC1";
    public const string Brush = "\uEAD5";
    public const string Clipboard = "\uEB2E";
    public const string Clock = "\uEB2F";
    public const string Close = "\uEB30";
    public const string Colors = "\uEB3B";
    public const string Copy = "\uEB49";
    public const string Crop = "\uEB5F";
    public const string Cut = "\uEB68";
    public const string Drag = "\uEB83";
    public const string Earth = "\uEB85";
    public const string Eraser = "\uEB87";
    public const string FlipH = "\uEBA2";
    public const string FlipV = "\uEBA3";
    public const string Heart = "\uEBE7";
    public const string Home = "\uEBEA";
    public const string Human = "\uEBF3";
    public const string Infinity = "\uEC03";
    public const string Lightbulb = "\uEC5D";
    public const string Move = "\uECA7";
    public const string Pencil = "\uECC5";
    public const string Reload = "\uECFA";
    public const string Script = "\uED15";
    public const string SectionCopy = "\uED19";
    public const string Section = "\uED1D";
    public const string Settings = "\uED24";
    public const string Shield = "\uED29";
    public const string Sliders = "\uED3D";
    public const string Sun = "\uED7D";
    public const string Trash = "\uEDBE";
    public const string Undo = "\uEDCB";
    public const string Zap = "\uEE0A";

    internal static readonly uint[] GlyphRange =
    [
        0xEA58, 0xEA58,
        0xEAC1, 0xEAC1,
        0xEAD5, 0xEAD5,
        0xEB2E, 0xEB30,
        0xEB3B, 0xEB3B,
        0xEB49, 0xEB49,
        0xEB5F, 0xEB5F,
        0xEB68, 0xEB68,
        0xEB83, 0xEB83,
        0xEB85, 0xEB85,
        0xEB87, 0xEB87,
        0xEBA2, 0xEBA3,
        0xEBE7, 0xEBE7,
        0xEBEA, 0xEBEA,
        0xEBF3, 0xEBF3,
        0xEC03, 0xEC03,
        0xEC5D, 0xEC5D,
        0xECA7, 0xECA7,
        0xECC5, 0xECC5,
        0xECFA, 0xECFA,
        0xED15, 0xED15,
        0xED19, 0xED19,
        0xED1D, 0xED1D,
        0xED24, 0xED24,
        0xED29, 0xED29,
        0xED3D, 0xED3D,
        0xED7D, 0xED7D,
        0xEDBE, 0xEDBE,
        0xEDCB, 0xEDCB,
        0xEE0A, 0xEE0A,
        0,
    ];

    public static unsafe void DrawDirect(ImDrawListPtr drawList, Num.Vector2 center, string glyph, byte alpha = 255, uint color = 0, float size = 0f)
    {
        var font = ImGuiBackend.IconFont;
        if (font.IsNull)
            return;

        if (size <= 0f)
            size = Size;
        uint text = color == 0 ? ImGui.GetColorU32(ImGuiCol.Text) : color;
        byte a = (byte)(((text >> 24) * alpha) / 255);
        uint col = (text & 0x00FFFFFF) | ((uint)a << 24);
        float half = size * 0.5f;
        drawList.AddText(font, size, new Num.Vector2(center.X - half, center.Y - half), col, glyph);
    }
}
