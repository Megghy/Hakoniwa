using ImGuiNET;
using Microsoft.Xna.Framework;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI.Windows;

public sealed class CharacterWindow : IWindow
{
    public string Title => "Hakoniwa - Character###HakoniwaCharacter";
    public string Label => "Char";
    public bool IsOpen { get; set; }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Num.Vector2(340, 420), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            if (Main.gameMenu || !Main.LocalPlayer.active)
                ImGui.TextUnformatted("Enter a world to edit the player.");
            else
                DrawPlayer(Main.LocalPlayer);
        }

        IsOpen = open;
        ImGui.End();
    }

    private static void DrawPlayer(Player player)
    {
        int hair = player.hair;
        if (ImGui.SliderInt("Hair", ref hair, 0, Main.maxHairStyles - 1))
            player.hair = hair;

        bool male = player.Male;
        if (ImGui.Checkbox("Male", ref male))
            player.Male = male;

        int life = player.statLifeMax;
        if (ImGui.SliderInt("Max life", ref life, 1, 5000))
        {
            player.statLifeMax = life;
            player.statLife = life;
        }

        int mana = player.statManaMax;
        if (ImGui.SliderInt("Max mana", ref mana, 0, 400))
        {
            player.statManaMax = mana;
            player.statMana = mana;
        }

        ColorEdit("Hair color", ref player.hairColor);
        ColorEdit("Skin", ref player.skinColor);
        ColorEdit("Eyes", ref player.eyeColor);
        ColorEdit("Shirt", ref player.shirtColor);
        ColorEdit("Undershirt", ref player.underShirtColor);
        ColorEdit("Pants", ref player.pantsColor);
        ColorEdit("Shoes", ref player.shoeColor);
    }

    private static void ColorEdit(string label, ref Color color)
    {
        var rgb = new Num.Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        if (!ImGui.ColorEdit3(label, ref rgb))
            return;
        color = new Color(rgb.X, rgb.Y, rgb.Z);
    }
}
