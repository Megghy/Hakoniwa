using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Num = System.Numerics;

namespace Hakoniwa.UI;

/// <summary>
/// FNA / XNA 与 Dear ImGui (Hexa.NET.ImGui) 的极简渲染与 Win32 输入法后端
/// </summary>
public sealed class ImGuiBackend : IDisposable
{
    private GraphicsDevice? _device;
    private BasicEffect? _effect;
    private Texture2D? _fontTexture;
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private int _vertexBufferSize;
    private int _indexBufferSize;
    private bool _texturesDirty;
    private int _scroll;
    private ImGuiIme? _ime;
    private readonly RasterizerState _rasterizer = new()
    {
        CullMode = CullMode.None,
        ScissorTestEnable = true,
    };

    private static readonly Dictionary<IntPtr, Texture2D> TextureById = [];
    private static readonly Dictionary<Texture2D, IntPtr> IdByTexture = [];
    private static int _nextTexId = 2;

    public static ImFontPtr IconFont { get; private set; }

    public ImGuiBackend(GraphicsDevice device)
    {
        if (device is null)
            throw new ArgumentNullException(nameof(device));
        var context = ImGui.CreateContext();
        ImGui.SetCurrentContext(context);

        var io = ImGui.GetIO();
        io.BackendFlags |= ImGuiBackendFlags.RendererHasTextures;
        var platform = ImGui.GetPlatformIO();
        platform.RendererTextureMaxWidth = 2048;
        platform.RendererTextureMaxHeight = 2048;

        Themes.HakoniwaTheme.Apply();
        BuildFont();
        AttachInput();
        if (!BindDevice())
            throw new InvalidOperationException("GraphicsDevice is not available.");
    }

    private bool BindDevice()
    {
        var device = Main.instance.GraphicsDevice;
        if (device is null || device.IsDisposed)
            return false;
        if (ReferenceEquals(_device, device))
            return true;

        if (_effect is { IsDisposed: false })
            _effect.Dispose();
        if (_vertexBuffer is { IsDisposed: false })
            _vertexBuffer.Dispose();
        if (_indexBuffer is { IsDisposed: false })
            _indexBuffer.Dispose();
        _effect = null;
        _vertexBuffer = null;
        _indexBuffer = null;
        _vertexBufferSize = 0;
        _indexBufferSize = 0;
        TextureById.Clear();
        IdByTexture.Clear();
        _fontTexture = null;
        _device = device;
        InitGraphics();
        _texturesDirty = true;
        return true;
    }

    public static IntPtr GetTextureId(Texture2D? texture)
    {
        if (texture is null || texture.IsDisposed)
            return IntPtr.Zero;

        if (IdByTexture.TryGetValue(texture, out var id))
            return id;

        var newId = (IntPtr)_nextTexId++;
        TextureById[newId] = texture;
        IdByTexture[texture] = newId;
        return newId;
    }

    public bool NewFrame()
    {
        if (!BindDevice())
            return false;
        var io = ImGui.GetIO();
        var pp = _device!.PresentationParameters;
        io.DisplaySize = new Num.Vector2(pp.BackBufferWidth, pp.BackBufferHeight);
        io.DisplayFramebufferScale = Num.Vector2.One;
        io.DeltaTime = Math.Max(1f / 60f, (float)Main.gameTimeCache.ElapsedGameTime.TotalSeconds);
        UpdateInput(io);
        ImGui.NewFrame();
        return true;
    }

    public void Render(bool extraCursor = false)
    {
        if (!BindDevice())
            return;
        var io = ImGui.GetIO();
        io.MouseDrawCursor = io.WantCaptureMouse || extraCursor;
        _ime?.Draw();
        ImGui.Render();
        var drawData = ImGui.GetDrawData();
        SyncTextures(drawData);
        if (drawData.CmdListsCount == 0)
            return;

        var oldViewport = _device!.Viewport;
        UpdateBuffers(drawData);
        SetupRenderState();
        RenderCommandLists(drawData);
        _device.Viewport = oldViewport;
    }

    public static unsafe ImTextureRef TexRef(IntPtr id) => new(null, id);

    private unsafe void BuildFont()
    {
        var io = ImGui.GetIO();
        io.Fonts.TexGlyphPadding = 1;

        var cfg = ImGui.ImFontConfig();
        cfg.OversampleH = 1;
        cfg.OversampleV = 1;
        cfg.PixelSnapH = true;

        string fontPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts", "fusion-pixel-12px-proportional-zh_hans.ttf");
        // UPEM 1200, hhea line 1600 → ImGui ScaleForPixelHeight(16) == 1 device pixel per font pixel
        if (File.Exists(fontPath))
            io.Fonts.AddFontFromFileTTF(fontPath, 16f, cfg);
        else
            io.Fonts.AddFontDefault(cfg);

        string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "Fonts", "pixelart-icons-font.ttf");
        if (File.Exists(iconPath))
        {
            var iconCfg = ImGui.ImFontConfig();
            iconCfg.OversampleH = 1;
            iconCfg.OversampleV = 1;
            iconCfg.PixelSnapH = true;
            iconCfg.GlyphMinAdvanceX = 24;
            fixed (uint* ranges = Icons.GlyphRange)
                IconFont = io.Fonts.AddFontFromFileTTF(iconPath, 24f, iconCfg, ranges);
            ImGui.Destroy(iconCfg);
        }

        ImGui.Destroy(cfg);
    }

    private void SyncTextures(ImDrawDataPtr drawData)
    {
        var textures = drawData.Textures;
        if (_texturesDirty)
        {
            for (int i = 0; i < textures.Size; i++)
            {
                var tex = textures[i];
                if (tex.IsNull || tex.Status == ImTextureStatus.Destroyed)
                    continue;
                tex.SetTexID(default);
                tex.SetStatus(ImTextureStatus.WantCreate);
            }
            _texturesDirty = false;
        }

        for (int i = 0; i < textures.Size; i++)
            ApplyTexture(textures[i]);
    }

    private unsafe void ApplyTexture(ImTextureDataPtr tex)
    {
        if (tex.IsNull || tex.Status == ImTextureStatus.Ok)
            return;

        if (tex.Status == ImTextureStatus.WantCreate || tex.Status == ImTextureStatus.WantUpdates)
        {
            int width = tex.Width;
            int height = tex.Height;
            if (width > 2048 || height > 2048)
                throw new NotSupportedException($"ImGui texture {width}x{height} exceeds XNA Reach 2048.");

            var id = (IntPtr)tex.TexID;
            TextureById.TryGetValue(id, out var gpu);
            if (gpu is null || gpu.Width != width || gpu.Height != height)
            {
                gpu?.Dispose();
                gpu = new Texture2D(_device, width, height, false, SurfaceFormat.Color);
                if (id == IntPtr.Zero)
                {
                    id = (IntPtr)_nextTexId++;
                    tex.SetTexID(id);
                }

                TextureById[id] = gpu;
                IdByTexture[gpu] = id;
                if (tex.Status == ImTextureStatus.WantCreate)
                    _fontTexture = gpu;
            }

            Upload(gpu, tex);
            tex.SetStatus(ImTextureStatus.Ok);
            return;
        }

        if (tex.Status != ImTextureStatus.WantDestroy || tex.UnusedFrames <= 0)
            return;

        var dead = (IntPtr)tex.TexID;
        if (dead != IntPtr.Zero && TextureById.TryGetValue(dead, out var old))
        {
            IdByTexture.Remove(old);
            TextureById.Remove(dead);
            if (_fontTexture == old)
                _fontTexture = null;
            old.Dispose();
        }

        tex.SetTexID(default);
        tex.SetStatus(ImTextureStatus.Destroyed);
    }

    private static unsafe void Upload(Texture2D gpu, ImTextureDataPtr tex)
    {
        int width = tex.Width;
        int height = tex.Height;
        var data = new byte[width * height * 4];
        if (tex.Format == ImTextureFormat.Rgba32)
            Marshal.Copy((IntPtr)tex.Pixels, data, 0, data.Length);
        else if (tex.Format == ImTextureFormat.Alpha8)
        {
            byte* src = tex.Pixels;
            for (int i = 0; i < width * height; i++)
            {
                int o = i * 4;
                data[o] = data[o + 1] = data[o + 2] = 255;
                data[o + 3] = src[i];
            }
        }
        else
            throw new NotSupportedException($"ImGui texture format {tex.Format} is not supported.");

        gpu.SetData(data);
    }

    private void InitGraphics()
    {
        _effect = new BasicEffect(_device)
        {
            VertexColorEnabled = true,
            TextureEnabled = true
        };
    }

    private void UpdateBuffers(ImDrawDataPtr drawData)
    {
        if (drawData.TotalVtxCount > _vertexBufferSize)
        {
            _vertexBuffer?.Dispose();
            _vertexBufferSize = (int)(drawData.TotalVtxCount * 1.5);
            _vertexBuffer = new VertexBuffer(_device, typeof(VertexPositionColorTexture), _vertexBufferSize, BufferUsage.WriteOnly);
        }

        if (drawData.TotalIdxCount > _indexBufferSize)
        {
            _indexBuffer?.Dispose();
            _indexBufferSize = (int)(drawData.TotalIdxCount * 1.5);
            _indexBuffer = new IndexBuffer(_device, IndexElementSize.SixteenBits, _indexBufferSize, BufferUsage.WriteOnly);
        }

        int vtxOffset = 0;
        int idxOffset = 0;
        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            var cmdList = drawData.CmdLists[i];
            var vtx = new VertexPositionColorTexture[cmdList.VtxBuffer.Size];
            for (int v = 0; v < cmdList.VtxBuffer.Size; v++)
            {
                var vert = cmdList.VtxBuffer[v];
                var col = vert.Col;
                var color = new Microsoft.Xna.Framework.Color(
                    (byte)(col & 0xFF),
                    (byte)((col >> 8) & 0xFF),
                    (byte)((col >> 16) & 0xFF),
                    (byte)((col >> 24) & 0xFF)
                );
                vtx[v] = new VertexPositionColorTexture(
                    new Microsoft.Xna.Framework.Vector3(vert.Pos.X - 0.5f, vert.Pos.Y - 0.5f, 0f),
                    color,
                    new Microsoft.Xna.Framework.Vector2(vert.Uv.X, vert.Uv.Y)
                );
            }

            var idx = new ushort[cmdList.IdxBuffer.Size];
            for (int k = 0; k < cmdList.IdxBuffer.Size; k++)
                idx[k] = (ushort)(cmdList.IdxBuffer[k] + vtxOffset);

            _vertexBuffer?.SetData(vtxOffset * Marshal.SizeOf<VertexPositionColorTexture>(), vtx, 0, vtx.Length, Marshal.SizeOf<VertexPositionColorTexture>());
            _indexBuffer?.SetData(idxOffset * sizeof(ushort), idx, 0, idx.Length);

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    private void SetupRenderState()
    {
        var device = _device!;
        var pp = device.PresentationParameters;
        device.Viewport = new Viewport(0, 0, pp.BackBufferWidth, pp.BackBufferHeight);
        device.RasterizerState = _rasterizer;
        device.BlendState = BlendState.NonPremultiplied;
        device.DepthStencilState = DepthStencilState.None;
        device.SamplerStates[0] = SamplerState.PointClamp;

        if (_effect is not null)
        {
            _effect.World = Matrix.Identity;
            _effect.View = Matrix.Identity;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0f, pp.BackBufferWidth, pp.BackBufferHeight, 0f, -1f, 1f);
        }
    }

    private void RenderCommandLists(ImDrawDataPtr drawData)
    {
        var device = _device!;
        var vp = device.Viewport;
        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        int vtxOffset = 0;
        int idxOffset = 0;
        for (int i = 0; i < drawData.CmdListsCount; i++)
        {
            var cmdList = drawData.CmdLists[i];
            for (int j = 0; j < cmdList.CmdBuffer.Size; j++)
            {
                var cmd = cmdList.CmdBuffer[j];
                if (cmd.ElemCount == 0)
                    continue;

                int clipX = Math.Max(0, (int)cmd.ClipRect.X);
                int clipY = Math.Max(0, (int)cmd.ClipRect.Y);
                int clipW = Math.Min(vp.Width - clipX, (int)(cmd.ClipRect.Z - cmd.ClipRect.X));
                int clipH = Math.Min(vp.Height - clipY, (int)(cmd.ClipRect.W - cmd.ClipRect.Y));

                if (clipW <= 0 || clipH <= 0)
                    continue;

                device.ScissorRectangle = new Microsoft.Xna.Framework.Rectangle(clipX, clipY, clipW, clipH);

                IntPtr texId = cmd.GetTexID();
                Texture2D? currentTex = _fontTexture;
                if (texId != IntPtr.Zero && TextureById.TryGetValue(texId, out var customTex) && customTex is { IsDisposed: false })
                    currentTex = customTex;

                device.Textures[0] = currentTex;
                if (_effect is not null)
                {
                    _effect.Texture = currentTex;
                    foreach (var pass in _effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawIndexedPrimitives(
                            PrimitiveType.TriangleList,
                            0,
                            0,
                            vtxOffset + cmdList.VtxBuffer.Size,
                            (int)cmd.IdxOffset + idxOffset,
                            (int)cmd.ElemCount / 3
                        );
                    }
                }
            }

            vtxOffset += cmdList.VtxBuffer.Size;
            idxOffset += cmdList.IdxBuffer.Size;
        }
    }

    private void UpdateInput(ImGuiIOPtr io)
    {
        var mouse = Mouse.GetState();
        var scale = PlayerInput.RawMouseScale;
        io.AddMousePosEvent(mouse.X * scale.X, mouse.Y * scale.Y);
        io.AddMouseButtonEvent(0, mouse.LeftButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(1, mouse.RightButton == ButtonState.Pressed);
        io.AddMouseButtonEvent(2, mouse.MiddleButton == ButtonState.Pressed);
        io.AddMouseWheelEvent(0f, (mouse.ScrollWheelValue - _scroll) / 120f);
        _scroll = mouse.ScrollWheelValue;

        _ime?.Flush(io);

        var kb = Keyboard.GetState();
        AddKey(io, kb, Keys.Tab, ImGuiKey.Tab);
        AddKey(io, kb, Keys.Left, ImGuiKey.LeftArrow);
        AddKey(io, kb, Keys.Right, ImGuiKey.RightArrow);
        AddKey(io, kb, Keys.Up, ImGuiKey.UpArrow);
        AddKey(io, kb, Keys.Down, ImGuiKey.DownArrow);
        AddKey(io, kb, Keys.Home, ImGuiKey.Home);
        AddKey(io, kb, Keys.End, ImGuiKey.End);
        AddKey(io, kb, Keys.Delete, ImGuiKey.Delete);
        AddKey(io, kb, Keys.Back, ImGuiKey.Backspace);
        AddKey(io, kb, Keys.Enter, ImGuiKey.Enter);
        AddKey(io, kb, Keys.Escape, ImGuiKey.Escape);
        if (_ime is not { Composing: true })
        {
            AddKey(io, kb, Keys.Space, ImGuiKey.Space);
            for (int i = 0; i < 26; i++)
                AddKey(io, kb, Keys.A + i, ImGuiKey.A + i);
            for (int i = 0; i < 10; i++)
                AddKey(io, kb, Keys.D0 + i, ImGuiKey.Key0 + i);
        }

        io.AddKeyEvent(ImGuiKey.ModCtrl, kb.IsKeyDown(Keys.LeftControl) || kb.IsKeyDown(Keys.RightControl));
        io.AddKeyEvent(ImGuiKey.ModShift, kb.IsKeyDown(Keys.LeftShift) || kb.IsKeyDown(Keys.RightShift));
        io.AddKeyEvent(ImGuiKey.ModAlt, kb.IsKeyDown(Keys.LeftAlt) || kb.IsKeyDown(Keys.RightAlt));
    }

    private static void AddKey(ImGuiIOPtr io, KeyboardState kb, Keys key, ImGuiKey mapped)
        => io.AddKeyEvent(mapped, kb.IsKeyDown(key));

    private unsafe void AttachInput()
    {
        var handle = Main.instance.Window.Handle;
        if (handle == IntPtr.Zero)
            return;

        var mainViewport = ImGui.GetMainViewport();
        mainViewport.PlatformHandle = (void*)handle;
        mainViewport.PlatformHandleRaw = (void*)handle;
        _ime = new ImGuiIme();
    }

    public void Dispose()
    {
        _ime?.Dispose();
        _ime = null;
        _fontTexture?.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _effect?.Dispose();
        _rasterizer.Dispose();
    }
}
