using System;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public static class UiIcons
{
    private const int LoadsPerFrame = 8;
    private static int _loads;

    public static void BeginFrame() => _loads = 0;

    public static Texture2D? GetItemTexture2D(int itemId)
    {
        if (itemId <= 0 || itemId >= ItemID.Count)
            return null;

        var asset = TextureAssets.Item[itemId];
        if (asset is null || !TryLoad(asset))
            return null;

        var tex = asset.Value;
        return tex is { IsDisposed: false } ? tex : null;
    }

    public static IntPtr GetItemTexture(int itemId, out Num.Vector2 uv0, out Num.Vector2 uv1, out Num.Vector2 size)
    {
        uv0 = Num.Vector2.Zero;
        uv1 = Num.Vector2.One;
        size = Num.Vector2.Zero;

        var tex = GetItemTexture2D(itemId);
        if (tex is null)
            return IntPtr.Zero;

        var frame = Main.itemAnimations[itemId] is { } anim
            ? anim.GetFrame(tex)
            : tex.Frame();
        if (frame.Width <= 0 || frame.Height <= 0)
            return IntPtr.Zero;

        uv0 = new Num.Vector2(frame.X / (float)tex.Width, frame.Y / (float)tex.Height);
        uv1 = new Num.Vector2((frame.X + frame.Width) / (float)tex.Width, (frame.Y + frame.Height) / (float)tex.Height);
        size = new Num.Vector2(frame.Width, frame.Height);
        return ImGuiBackend.GetTextureId(tex);
    }

    public static void DrawItem(int itemId, float targetSize = 20f)
    {
        var texId = GetItemTexture(itemId, out var uv0, out var uv1, out var origSize);
        if (texId == IntPtr.Zero || origSize.X <= 0f || origSize.Y <= 0f)
        {
            ImGui.Dummy(new Num.Vector2(targetSize, targetSize));
            return;
        }

        float scale = targetSize / Math.Max(origSize.X, origSize.Y);
        var drawSize = origSize * scale;
        float padX = (targetSize - drawSize.X) * 0.5f;
        float padY = (targetSize - drawSize.Y) * 0.5f;
        if (padX > 0f || padY > 0f)
        {
            var cur = ImGui.GetCursorPos();
            ImGui.SetCursorPos(new Num.Vector2(cur.X + padX, cur.Y + padY));
        }

        ImGui.Image(ImGuiBackend.TexRef(texId), drawSize, uv0, uv1);
    }

    public static void DrawItemDirect(ImDrawListPtr drawList, Num.Vector2 center, int itemId, float targetSize = 22f, byte alpha = 255)
    {
        var texId = GetItemTexture(itemId, out var uv0, out var uv1, out var origSize);
        DrawFitted(drawList, center, texId, uv0, uv1, origSize, targetSize, alpha);
    }

    public static void DrawProjectileDirect(ImDrawListPtr drawList, Num.Vector2 center, int projId, float targetSize = 22f, byte alpha = 255)
    {
        var texId = GetProjectileTexture(projId, out var uv0, out var uv1, out var origSize);
        DrawFitted(drawList, center, texId, uv0, uv1, origSize, targetSize, alpha);
    }

    public static IntPtr GetProjectileTexture(int projId, out Num.Vector2 uv0, out Num.Vector2 uv1, out Num.Vector2 size)
    {
        uv0 = Num.Vector2.Zero;
        uv1 = Num.Vector2.One;
        size = Num.Vector2.Zero;
        if (projId <= 0 || projId >= ProjectileID.Count)
            return IntPtr.Zero;

        var asset = TextureAssets.Projectile[projId];
        if (asset is null || !TryLoad(asset))
            return IntPtr.Zero;

        var tex = asset.Value;
        if (tex is not { IsDisposed: false } || tex.Width <= 0 || tex.Height <= 0)
            return IntPtr.Zero;

        int frames = projId < Main.projFrames.Length ? Math.Max(1, Main.projFrames[projId]) : 1;
        int frameH = Math.Max(1, tex.Height / frames);
        uv1 = new Num.Vector2(1f, frameH / (float)tex.Height);
        size = new Num.Vector2(tex.Width, frameH);
        return ImGuiBackend.GetTextureId(tex);
    }

    private static bool TryLoad(Asset<Texture2D> asset)
    {
        if (asset.State == AssetState.Loaded)
            return true;
        if (_loads >= LoadsPerFrame)
            return false;
        _loads++;
        Main.Assets.Request<Texture2D>(asset.Name, AssetRequestMode.ImmediateLoad);
        return asset.State == AssetState.Loaded;
    }

    private static void DrawFitted(ImDrawListPtr drawList, Num.Vector2 center, IntPtr texId, Num.Vector2 uv0, Num.Vector2 uv1, Num.Vector2 origSize, float targetSize, byte alpha)
    {
        if (texId == IntPtr.Zero || origSize.X <= 0f || origSize.Y <= 0f)
            return;
        float scale = targetSize / Math.Max(origSize.X, origSize.Y);
        var drawSize = origSize * scale;
        var halfSize = drawSize * 0.5f;
        uint tint = (uint)((alpha << 24) | 0x00FFFFFF);
        drawList.AddImage(ImGuiBackend.TexRef(texId), center - halfSize, center + halfSize, uv0, uv1, tint);
    }
}
