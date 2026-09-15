using System;
using System.Runtime.InteropServices;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI;

public sealed class ImGuiBackend : IDisposable
{
    private const int GwlWndProc = -4;
    private const uint WmChar = 0x0102;

    private readonly GraphicsDevice _device;
    private readonly BasicEffect _effect;
    private readonly RasterizerState _rasterizer = new()
    {
        CullMode = CullMode.None,
        DepthBias = 0,
        FillMode = FillMode.Solid,
        MultiSampleAntiAlias = false,
        ScissorTestEnable = true,
        SlopeScaleDepthBias = 0,
    };

    private Texture2D? _fontTexture;
    private byte[] _vtx = [];
    private byte[] _idx = [];
    private int _scroll;
    private IntPtr _prevWndProc;
    private WndProc? _wndProc;
    private bool _disposed;

    private delegate IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    public ImGuiBackend(GraphicsDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        ImGui.CreateContext();
        var io = ImGui.GetIO();
        io.ConfigFlags |= ImGuiConfigFlags.NavEnableKeyboard;
        io.BackendFlags |= ImGuiBackendFlags.RendererHasVtxOffset;
        Hakoniwa.UI.Themes.HakoniwaTheme.Apply();
        BuildFont();
        _effect = new BasicEffect(device)
        {
            LightingEnabled = false,
            VertexColorEnabled = true,
            TextureEnabled = true,
            World = Matrix.Identity,
            View = Matrix.Identity,
        };
        AttachInput();
    }

    public void NewFrame()
    {
        var io = ImGui.GetIO();
        io.DisplaySize = new Num.Vector2(_device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight);
        io.DeltaTime = Math.Max(1f / 60f, (float)Main.gameTimeCache.ElapsedGameTime.TotalSeconds);
        UpdateInput(io);
        ImGui.NewFrame();
    }

    public void Render()
    {
        ImGui.Render();
        var data = ImGui.GetDrawData();
        if (data.CmdListsCount == 0)
            return;

        _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, _device.PresentationParameters.BackBufferWidth, _device.PresentationParameters.BackBufferHeight, 0f, -1f, 1f);
        _device.BlendState = BlendState.NonPremultiplied;
        _device.DepthStencilState = DepthStencilState.None;
        _device.RasterizerState = _rasterizer;
        _device.SamplerStates[0] = SamplerState.LinearClamp;

        int vtxSize = data.TotalVtxCount * 20;
        int idxSize = data.TotalIdxCount * 2;
        if (_vtx.Length < vtxSize)
            _vtx = new byte[vtxSize];
        if (_idx.Length < idxSize)
            _idx = new byte[idxSize];

        int vtxOff = 0;
        int idxOff = 0;
        for (int n = 0; n < data.CmdListsCount; n++)
        {
            var list = data.CmdLists[n];
            int vBytes = list.VtxBuffer.Size * 20;
            int iBytes = list.IdxBuffer.Size * 2;
            Marshal.Copy(list.VtxBuffer.Data, _vtx, vtxOff, vBytes);
            Marshal.Copy(list.IdxBuffer.Data, _idx, idxOff, iBytes);
            vtxOff += vBytes;
            idxOff += iBytes;
        }

        int vtxBase = 0;
        int idxBase = 0;
        for (int n = 0; n < data.CmdListsCount; n++)
        {
            var list = data.CmdLists[n];
            for (int c = 0; c < list.CmdBuffer.Size; c++)
            {
                var cmd = list.CmdBuffer[c];
                var clip = new Rectangle(
                    (int)cmd.ClipRect.X,
                    (int)cmd.ClipRect.Y,
                    (int)(cmd.ClipRect.Z - cmd.ClipRect.X),
                    (int)(cmd.ClipRect.W - cmd.ClipRect.Y));
                _device.ScissorRectangle = Rectangle.Intersect(clip, _device.Viewport.Bounds);
                _effect.Texture = cmd.TextureId == IntPtr.Zero || _fontTexture is null ? _fontTexture : _fontTexture;
                foreach (var pass in _effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    DrawCmd(list, cmd, vtxBase, idxBase);
                }
            }

            vtxBase += list.VtxBuffer.Size;
            idxBase += list.IdxBuffer.Size;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        DetachInput();
        _fontTexture?.Dispose();
        _effect.Dispose();
        ImGui.DestroyContext();
    }

    private void DrawCmd(ImDrawListPtr list, ImDrawCmdPtr cmd, int vtxBase, int idxBase)
    {
        if (cmd.ElemCount == 0)
            return;

        var vertices = new VertexPositionColorTexture[cmd.ElemCount];
        var indices = new short[cmd.ElemCount];
        for (int i = 0; i < cmd.ElemCount; i++)
        {
            int index = BitConverter.ToUInt16(_idx, (idxBase + (int)cmd.IdxOffset + i) * 2) + vtxBase + (int)cmd.VtxOffset;
            float x = BitConverter.ToSingle(_vtx, index * 20);
            float y = BitConverter.ToSingle(_vtx, index * 20 + 4);
            float u = BitConverter.ToSingle(_vtx, index * 20 + 8);
            float v = BitConverter.ToSingle(_vtx, index * 20 + 12);
            uint col = BitConverter.ToUInt32(_vtx, index * 20 + 16);
            vertices[i] = new VertexPositionColorTexture(
                new Vector3(x, y, 0f),
                new Color((byte)col, (byte)(col >> 8), (byte)(col >> 16), (byte)(col >> 24)),
                new Vector2(u, v));
            indices[i] = (short)i;
        }

        _device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList, vertices, 0, vertices.Length, indices, 0, indices.Length / 3);
    }

    private void BuildFont()
    {
        var io = ImGui.GetIO();
        io.Fonts.AddFontDefault();
        io.Fonts.GetTexDataAsRGBA32(out IntPtr pixels, out int width, out int height, out int _);
        var data = new byte[width * height * 4];
        Marshal.Copy(pixels, data, 0, data.Length);
        _fontTexture = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
        _fontTexture.SetData(data);
        io.Fonts.SetTexID((IntPtr)1);
        io.Fonts.ClearTexData();
    }

    private void UpdateInput(ImGuiIOPtr io)
    {
        var mouse = Mouse.GetState();
        io.AddMousePosEvent(mouse.X, mouse.Y);
        io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
        io.AddMouseWheelEvent(0f, (mouse.ScrollWheelValue - _scroll) / 120f);
        _scroll = mouse.ScrollWheelValue;

        var kb = Keyboard.GetState();
        foreach (Keys key in Enum.GetValues(typeof(Keys)))
        {
            if (TryMap(key, out var mapped))
                io.AddKeyEvent(mapped, kb.IsKeyDown(key));
        }

        io.AddKeyEvent(ImGuiKey.ModCtrl, kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift, kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt, kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt));
    }

    private static bool TryMap(Keys key, out ImGuiKey mapped)
    {
        mapped = key switch
        {
            Keys.Tab => ImGuiKey.Tab,
            Keys.Left => ImGuiKey.LeftArrow,
            Keys.Right => ImGuiKey.RightArrow,
            Keys.Up => ImGuiKey.UpArrow,
            Keys.Down => ImGuiKey.DownArrow,
            Keys.Home => ImGuiKey.Home,
            Keys.End => ImGuiKey.End,
            Keys.Delete => ImGuiKey.Delete,
            Keys.Back => ImGuiKey.Backspace,
            Keys.Enter => ImGuiKey.Enter,
            Keys.Escape => ImGuiKey.Escape,
            Keys.Space => ImGuiKey.Space,
            Keys.A => ImGuiKey.A,
            Keys.C => ImGuiKey.C,
            Keys.V => ImGuiKey.V,
            Keys.X => ImGuiKey.X,
            Keys.Y => ImGuiKey.Y,
            Keys.Z => ImGuiKey.Z,
            _ => ImGuiKey.None,
        };
        if (key is >= Keys.A and <= Keys.Z)
        {
            mapped = ImGuiKey.A + (key - Keys.A);
            return true;
        }

        if (key is >= Keys.D0 and <= Keys.D9)
        {
            mapped = ImGuiKey._0 + (key - Keys.D0);
            return true;
        }

        return mapped != ImGuiKey.None;
    }

    private void AttachInput()
    {
        var handle = Main.instance.Window.Handle;
        if (handle == IntPtr.Zero)
            return;
        _wndProc = Hook;
        _prevWndProc = SetWindowLong(handle, GwlWndProc, Marshal.GetFunctionPointerForDelegate(_wndProc));
    }

    private void DetachInput()
    {
        var handle = Main.instance.Window.Handle;
        if (handle != IntPtr.Zero && _prevWndProc != IntPtr.Zero)
            SetWindowLong(handle, GwlWndProc, _prevWndProc);
        _wndProc = null;
    }

    private IntPtr Hook(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        if (msg == WmChar)
        {
            int ch = wParam.ToInt32();
            if (ch >= 32)
                ImGui.GetIO().AddInputCharacter((uint)ch);
        }

        return CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    [DllImport("user32.dll", EntryPoint = "SetWindowLong")]
    private static extern IntPtr SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
