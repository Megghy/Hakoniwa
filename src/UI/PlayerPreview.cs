using System;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI;

internal static class PlayerPreview
{
    internal const int Width = 48;
    internal const int Height = 80;

    private static RenderTarget2D? _rt;
    private static Player? _dummy;
    private static Player? _queued;
    private static bool _wanted;

    public static void Prepare()
    {
        if (!_wanted)
            return;
        _wanted = false;
        if (_queued is not { active: true })
            return;
        Render(_queued);
    }

    public static void Draw(Player src, Num.Vector2 size)
    {
        _queued = src;
        _wanted = true;
        if (_rt is null || _rt.IsDisposed)
            return;
        var id = ImGuiBackend.GetTextureId(_rt);
        if (id == IntPtr.Zero)
            return;
        ImGui.Image(ImGuiBackend.TexRef(id), size);
    }

    private static void Render(Player src)
    {
        var device = Main.instance.GraphicsDevice;
        if (device is null || device.IsDisposed)
            return;
        EnsureTarget(device);
        _dummy ??= new Player();
        CopyLook(src, _dummy);
        _dummy.ResetEffects();
        _dummy.ResetVisibleAccessories();
        _dummy.UpdateDyes();
        _dummy.PlayerFrame();
        _dummy.bodyFrame.Y = _dummy.legFrame.Y = _dummy.headFrame.Y = 0;

        var oldRt = device.GetRenderTargets();
        var oldVp = device.Viewport;
        device.SetRenderTarget(_rt);
        device.Clear(new Color(16, 18, 28, 255));
        Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);
        try
        {
            // 与 UICharacter 相同：盒子中心对齐 hitbox，scale=1。scale≠1 会绕脚底放大，头发会画出 RT。
            var pos = Main.screenPosition + new Vector2(
                Width * 0.5f - _dummy.width * 0.5f,
                Height - _dummy.height - 8f);
            Main.PlayerRenderer.DrawPlayer(Main.Camera, _dummy, pos, 0f, Vector2.Zero);
        }
        finally
        {
            Main.spriteBatch.End();
            if (oldRt.Length == 0)
                device.SetRenderTarget(null);
            else
                device.SetRenderTargets(oldRt);
            device.Viewport = oldVp;
        }
    }

    private static void EnsureTarget(GraphicsDevice device)
    {
        if (_rt is { IsDisposed: false } && _rt.GraphicsDevice == device)
            return;
        _rt?.Dispose();
        _rt = new RenderTarget2D(device, Width, Height, false, SurfaceFormat.Color, DepthFormat.None, 0, RenderTargetUsage.PreserveContents);
    }

    private static void CopyLook(Player src, Player dst)
    {
        dst.active = true;
        dst.dead = false;
        dst.stealth = 1f;
        dst.gravDir = 1f;
        dst.direction = 1;
        dst.width = src.width;
        dst.height = src.height;
        dst.hair = src.hair;
        dst.hairColor = src.hairColor;
        dst.skinColor = src.skinColor;
        dst.eyeColor = src.eyeColor;
        dst.shirtColor = src.shirtColor;
        dst.underShirtColor = src.underShirtColor;
        dst.pantsColor = src.pantsColor;
        dst.shoeColor = src.shoeColor;
        dst.skinVariant = src.skinVariant;
        dst.hairDye = 0;
        dst.head = dst.body = dst.legs = 0;
        dst.wings = 0;
        dst.inventory[0] ??= new Item();
        dst.inventory[0].TurnToAir();
        for (int i = 0; i < dst.armor.Length; i++)
            dst.armor[i].TurnToAir();
    }
}
