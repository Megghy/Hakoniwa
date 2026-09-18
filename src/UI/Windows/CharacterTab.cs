using System;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;

namespace Hakoniwa.UI.Windows;

internal static class CharacterTab
{
    private static readonly string[] ClothNames = ["便装", "贴纸", "街头", "风衣", "礼服", "人偶"];
    private static readonly Vector2 PreviewSize = new(PlayerPreview.Width * 2f, PlayerPreview.Height * 2f);

    public static void Draw()
    {
        Ui.BeginScroll("char-scroll");
        if (Main.gameMenu || !Main.LocalPlayer.active)
        {
            Ui.Heading(Icons.Human, "角色外观");
            ImGui.TextUnformatted("请先进入世界以编辑角色。");
            ImGui.EndChild();
            return;
        }

        var player = Main.LocalPlayer;
        bool changed = false;
        Ui.Heading(Icons.Human, "角色外观");
        DrawLook(player, ref changed);
        DrawHair(player, ref changed);
        if (changed)
        {
            ContentSamples.FixItemsUsingPlayerColours();
            if (Main.netMode == 1)
                NetMessage.SendData(4, -1, -1, null, player.whoAmI);
        }

        ImGui.EndChild();
    }

    private static void DrawLook(Player player, ref bool changed)
    {
        ImGui.BeginGroup();
        PlayerPreview.Draw(player, PreviewSize);
        ImGui.EndGroup();
        ImGui.SameLine(0f, 12f);
        ImGui.BeginGroup();
        ImGui.BeginChild("look-edit", new Vector2(0f, PreviewSize.Y), ImGuiChildFlags.None);

        int gender = player.Male ? 0 : 1;
        Ui.Chips("gender", 2, ref gender, i => i == 0 ? "男性" : "女性");
        if (player.Male != (gender == 0))
        {
            player.Male = gender == 0;
            changed = true;
        }

        ColorEdit("肤色", ref player.skinColor, ref changed);
        ImGui.SameLine(0f, 12f);
        ColorEdit("眼睛", ref player.eyeColor, ref changed);

        ImGui.Spacing();
        var order = player.Male ? PlayerVariantID.Sets.VariantOrderMale : PlayerVariantID.Sets.VariantOrderFemale;
        int cloth = 0;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == player.skinVariant)
                cloth = i;
        }

        Ui.Chips("cloth", ClothNames.Length, ref cloth, i => ClothNames[i]);
        if (player.skinVariant != order[cloth])
        {
            player.skinVariant = order[cloth];
            changed = true;
        }

        ColorEdit("上衣", ref player.shirtColor, ref changed);
        ImGui.SameLine(0f, 8f);
        ColorEdit("内衬", ref player.underShirtColor, ref changed);
        ColorEdit("长裤", ref player.pantsColor, ref changed);
        ImGui.SameLine(0f, 8f);
        ColorEdit("鞋子", ref player.shoeColor, ref changed);

        ImGui.EndChild();
        ImGui.EndGroup();
    }

    private static void DrawHair(Player player, ref bool changed)
    {
        Ui.Heading(Icons.Colors, "发型");
        ColorEdit("发色", ref player.hairColor, ref changed);
        ImGui.SameLine();
        ImGui.TextDisabled($"{player.hair + 1} / {Main.maxHairStyles}");

        ImGui.BeginChild("hair-grid", new Vector2(0f, 220f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());
        const float cellW = 48f;
        const float cellH = 64f;
        float gap = 4f;
        int cols = Math.Max(1, (int)((ImGui.GetContentRegionAvail().X + gap) / (cellW + gap)));
        for (int i = 0; i < Main.maxHairStyles; i++)
        {
            if (i > 0 && i % cols != 0)
                ImGui.SameLine(0f, gap);
            ImGui.PushID(i);
            var pos = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton("h", new Vector2(cellW, cellH)))
            {
                player.hair = i;
                changed = true;
            }

            bool on = player.hair == i;
            bool hover = ImGui.IsItemHovered();
            var max = pos + new Vector2(cellW, cellH);
            dl.AddRectFilled(pos, max, on ? 0xF8243048 : (hover ? 0xF8182030 : 0xF010141F));
            dl.AddRect(pos, max, on ? Ui.GoldBorder : Ui.ChipLine);
            if (ImGui.IsItemVisible())
                DrawHairThumb(dl, pos, cellW, cellH, i, player);
            ImGui.PopID();
        }

        ImGui.EndChild();
    }

    private static void DrawHairThumb(ImDrawListPtr dl, Vector2 pos, float cellW, float cellH, int hair, Player player)
    {
        Main.instance.LoadHair(hair);
        var hairTex = TextureOf(TextureAssets.PlayerHair[hair]);
        if (hairTex is null)
            return;
        int srcW = hairTex.Width;
        int srcH = Math.Min(hairTex.Height, 56);
        float scale = Math.Min((cellW - 4f) / srcW, (cellH - 4f) / srcH);
        var dest = new Vector2(srcW * scale, srcH * scale);
        var min = pos + new Vector2((cellW - dest.X) * 0.5f, (cellH - dest.Y) * 0.5f);
        var max = min + dest;
        int skin = player.skinVariant;
        DrawPart(dl, TextureAssets.Players[skin, PlayerTextureID.Head], min, max, srcW, srcH, Abgr(player.skinColor));
        DrawPart(dl, TextureAssets.Players[skin, PlayerTextureID.EyeWhites], min, max, srcW, srcH, 0xFFFFFFFFu);
        DrawPart(dl, TextureAssets.Players[skin, PlayerTextureID.Eyes], min, max, srcW, srcH, Abgr(player.eyeColor));
        DrawSprite(dl, hairTex, min, max, srcW, srcH, Abgr(player.hairColor));
    }

    private static void DrawPart(ImDrawListPtr dl, Asset<Texture2D>? asset, Vector2 min, Vector2 max, int srcW, int srcH, uint tint)
    {
        var tex = TextureOf(asset);
        if (tex is not null)
            DrawSprite(dl, tex, min, max, srcW, srcH, tint);
    }

    private static void DrawSprite(ImDrawListPtr dl, Texture2D tex, Vector2 min, Vector2 max, int srcW, int srcH, uint tint)
    {
        srcW = Math.Min(srcW, tex.Width);
        srcH = Math.Min(srcH, tex.Height);
        if (srcW <= 0 || srcH <= 0)
            return;
        var id = ImGuiBackend.GetTextureId(tex);
        if (id == IntPtr.Zero)
            return;
        var uv1 = new Vector2(srcW / (float)tex.Width, srcH / (float)tex.Height);
        dl.AddImage(ImGuiBackend.TexRef(id), min, max, Vector2.Zero, uv1, tint);
    }

    private static Texture2D? TextureOf(Asset<Texture2D>? asset)
    {
        if (asset is null)
            return null;
        if (asset.State != AssetState.Loaded)
            Main.Assets.Request<Texture2D>(asset.Name, AssetRequestMode.ImmediateLoad);
        var tex = asset.Value;
        return tex is { IsDisposed: false } ? tex : null;
    }

    private static void ColorEdit(string label, ref Color color, ref bool changed)
    {
        var rgb = new Vector3(color.R / 255f, color.G / 255f, color.B / 255f);
        ImGui.SetNextItemWidth(72f);
        if (ImGui.ColorEdit3(label, ref rgb, ImGuiColorEditFlags.NoInputs))
        {
            color = new Color(rgb.X, rgb.Y, rgb.Z);
            changed = true;
        }
    }

    private static uint Abgr(Color c) => (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R);
}
