using System;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Hakoniwa.UI.Windows;

internal static class CharacterTab
{
    private static readonly string[] ClothNames = ["便装", "贴纸", "街头", "风衣", "礼服", "人偶"];
    private static readonly string[] DiffNames = ["软核", "中核", "硬核", "旅途"];
    private static readonly string[] VoiceNames = ["男声", "女声", "中性"];
    private static readonly Vector2 PreviewSize = new(PlayerPreview.Width * 2f, PlayerPreview.Height * 2f);
    private static string _rename = "";

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
        DrawPacks(player);
        DrawStats(player, ref changed);
        Ui.Heading(Icons.Human, "角色外观");
        DrawLook(player, ref changed);
        DrawHair(player, ref changed);
        if (changed)
        {
            ContentSamples.FixItemsUsingPlayerColours();
            if (Main.netMode == 1)
            {
                NetMessage.SendData(4, -1, -1, null, player.whoAmI);
                NetMessage.SendData(16, -1, -1, null, player.whoAmI);
                NetMessage.SendData(42, -1, -1, null, player.whoAmI);
            }
        }

        ImGui.EndChild();
    }

    private static void DrawPacks(Player player)
    {
        Ui.Heading(Icons.Archive, "外观存档");
        if (ImGui.Button("保存到角色文件"))
            CharacterPacks.Commit(player);
        ImGui.SameLine();
        if (ImGui.Button("+ 新建"))
            CharacterPacks.Add(player);
        if (CharacterPacks.Dirty(player))
        {
            ImGui.SameLine();
            ImGui.TextDisabled("未写入角色文件");
        }

        for (int i = 0; i < CharacterPacks.Count; i++)
        {
            if (i > 0)
                ImGui.SameLine();
            bool on = CharacterPacks.Active == i;
            if (on)
                ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.42f, 0.28f, 0.72f, 1f));
            if (ImGui.Button(CharacterPacks.NameAt(i)))
                CharacterPacks.SwitchTo(player, i);
            if (on)
                ImGui.PopStyleColor();
            if (!ImGui.BeginPopupContextItem($"cpack{i}"))
                continue;
            if (ImGui.IsWindowAppearing())
                _rename = CharacterPacks.NameAt(i);
            ImGui.SetNextItemWidth(140f);
            if (ImGui.InputText("##crn", ref _rename, (UIntPtr)32, ImGuiInputTextFlags.EnterReturnsTrue))
            {
                CharacterPacks.Rename(i, _rename);
                ImGui.CloseCurrentPopup();
            }
            if (ImGui.Button("重命名"))
            {
                CharacterPacks.Rename(i, _rename);
                ImGui.CloseCurrentPopup();
            }
            ImGui.SameLine();
            if (ImGui.Button("删除"))
            {
                CharacterPacks.Remove(player, i);
                ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
                return;
            }
            ImGui.EndPopup();
        }
    }

    private static void DrawStats(Player player, ref bool changed)
    {
        Ui.Heading(Icons.Heart, "属性");
        StatInt("生命上限", ref player.statLifeMax, 1, 9999, ref changed);
        ImGui.SameLine();
        int lifeCap = Math.Max(player.statLifeMax, player.statLifeMax2);
        int life = Math.Min(player.statLife, lifeCap);
        StatInt("当前生命", ref life, 0, lifeCap, ref changed);
        player.statLife = life;

        StatInt("魔力上限", ref player.statManaMax, 0, 999, ref changed);
        ImGui.SameLine();
        int manaCap = Math.Max(player.statManaMax, player.statManaMax2);
        int mana = Math.Min(player.statMana, Math.Max(0, manaCap));
        StatInt("当前魔力", ref mana, 0, Math.Max(0, manaCap), ref changed);
        player.statMana = mana;

        int diff = Math.Min(DiffNames.Length - 1, Math.Max(0, (int)player.difficulty));
        Ui.Chips("diff", DiffNames.Length, ref diff, i => DiffNames[i]);
        if (player.difficulty != (byte)diff)
        {
            player.difficulty = (byte)diff;
            changed = true;
        }

        Flag("恶魔之心（额外饰品栏）", ref player.extraAccessory, ref changed);
        Flag("活力水晶", ref player.usedAegisCrystal, ref changed);
        ImGui.SameLine();
        Flag("埃癸斯果", ref player.usedAegisFruit, ref changed);
        Flag("奥术水晶", ref player.usedArcaneCrystal, ref changed);
        ImGui.SameLine();
        Flag("银河珍珠", ref player.usedGalaxyPearl, ref changed);
        Flag("软糖虫", ref player.usedGummyWorm, ref changed);
        ImGui.SameLine();
        Flag("仙馔", ref player.usedAmbrosia, ref changed);
    }

    private static void StatInt(string label, ref int value, int min, int max, ref bool changed)
    {
        ImGui.SetNextItemWidth(100f);
        int v = value;
        if (!ImGui.InputInt(label, ref v))
            return;
        v = Math.Min(max, Math.Max(min, v));
        if (v == value)
            return;
        value = v;
        changed = true;
    }

    private static void Flag(string label, ref bool value, ref bool changed)
    {
        if (ImGui.Checkbox(label, ref value))
            changed = true;
    }

    private static void DrawLook(Player player, ref bool changed)
    {
        PlayerPreview.Draw(player, PreviewSize);

        ImGui.TextUnformatted("性别");
        int gender = player.Male ? 0 : 1;
        Ui.Chips("gender", 2, ref gender, i => i == 0 ? "男性" : "女性");
        if (player.Male != (gender == 0))
        {
            player.Male = gender == 0;
            if (player.voiceVariant is PlayerVoiceID.Male or PlayerVoiceID.Female)
                player.voiceVariant = player.Male ? PlayerVoiceID.Male : PlayerVoiceID.Female;
            changed = true;
        }

        ImGui.TextUnformatted("声音");
        int voice = VoiceIndex(player.voiceVariant);
        Ui.Chips("voice", VoiceNames.Length, ref voice, i => VoiceNames[i]);
        int nextVoice = PlayerVoiceID.VariantOrder[voice];
        if (player.voiceVariant != nextVoice)
        {
            player.voiceVariant = nextVoice;
            changed = true;
        }

        ImGui.TextUnformatted("服装");
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

        ImGui.TextUnformatted("颜色");
        ColorEdit("肤色", ref player.skinColor, ref changed);
        ImGui.SameLine(0f, 12f);
        ColorEdit("眼睛", ref player.eyeColor, ref changed);
        ImGui.SameLine(0f, 12f);
        ColorEdit("上衣", ref player.shirtColor, ref changed);
        ColorEdit("内衬", ref player.underShirtColor, ref changed);
        ImGui.SameLine(0f, 12f);
        ColorEdit("长裤", ref player.pantsColor, ref changed);
        ImGui.SameLine(0f, 12f);
        ColorEdit("鞋子", ref player.shoeColor, ref changed);
    }

    private static int VoiceIndex(int variant)
    {
        var order = PlayerVoiceID.VariantOrder;
        for (int i = 0; i < order.Length; i++)
        {
            if (order[i] == variant)
                return i;
        }
        return 0;
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
        ImGui.PushID(label);
        ImGui.BeginGroup();
        if (ImGui.ColorButton("##swatch", new Vector4(rgb.X, rgb.Y, rgb.Z, 1f), ImGuiColorEditFlags.None, new Vector2(28f, 22f)))
            ImGui.OpenPopup("##picker");
        if (ImGui.BeginPopup("##picker"))
        {
            if (ImGui.ColorPicker3("##c", ref rgb, ImGuiColorEditFlags.NoSidePreview | ImGuiColorEditFlags.NoSmallPreview))
            {
                color = new Color(rgb.X, rgb.Y, rgb.Z);
                changed = true;
            }
            ImGui.EndPopup();
        }
        ImGui.SameLine(0f, 6f);
        ImGui.TextUnformatted(label);
        ImGui.EndGroup();
        ImGui.PopID();
    }

    private static uint Abgr(Color c) => (uint)(c.A << 24 | c.B << 16 | c.G << 8 | c.R);
}
