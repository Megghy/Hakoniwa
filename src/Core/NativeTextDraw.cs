using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameInput;
using Terraria.UI.Chat;

namespace Hakoniwa.Core;

internal static partial class NativeTextInput
{
    private static void DrawPlayerChat(Action<Main> orig, Main self)
    {
        if (CheatState.ImGuiInput)
        {
            orig(self);
            return;
        }

        var stamp = TimeLogger.Start();
        if (Main.drawingPlayerChat)
            PlayerInput.WritingText = true;
        self.HandleIME();
        if (Main.drawingPlayerChat)
            DrawChatBar(self);
        Main.chatMonitor.DrawChat(Main.drawingPlayerChat);
        DrawHistSelection();
        TimeLogger.PlayerChat.AddTime(stamp);
    }

    private static void DrawChatBar(Main self)
    {
        var lines = Editor.Text.Split('\n');
        int shown = lines.Length < 1 ? 1 : lines.Length > 4 ? 4 : lines.Length;
        int lineH = 22;
        int top = Main.screenHeight - 8 - shown * lineH;
        DrawChatBack();
        if (shown > 1)
        {
            var dest = new Rectangle(78, top, Main.screenWidth - 156, shown * lineH + 4);
            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, dest, new Color(0, 0, 0, 160));
        }

        Blink(self);
        var font = FontAssets.MouseText.Value;
        int consumed = 0;
        Vector2 caretPos = new(88f, top);
        for (int i = 0; i < shown; i++)
        {
            string line = i < lines.Length ? lines[i] : "";
            var pos = new Vector2(88f, top + i * lineH);
            int caret = Editor.Caret >= consumed && Editor.Caret <= consumed + line.Length
                ? Editor.Caret - consumed
                : -1;
            DrawField(Main.spriteBatch, font, line, pos, Color.White, caret >= 0 && self.textBlinkerState == 1, Vector2.One, caret, consumed);
            if (caret >= 0)
                caretPos = pos + new Vector2(PrefixWidth(line, caret), 0f);
            consumed += line.Length + 1;
        }

        self.SetIMEPanelAnchor(caretPos + new Vector2(0f, -6f), 0f);
    }

    private static void DrawChatBack()
    {
        var batch = Main.spriteBatch;
        var tex = TextureAssets.TextBack.Value;
        var tint = new Color(100, 100, 100, 100);
        int y = Main.screenHeight - 36;
        if (Main.screenWidth <= 800)
        {
            batch.Draw(tex, new Vector2(78f, y), new Rectangle(0, 0, tex.Width, tex.Height), tint);
            return;
        }

        int remain = Main.screenWidth - 300;
        int x = 78;
        batch.Draw(tex, new Vector2(x, y), new Rectangle(0, 0, tex.Width - 100, tex.Height), tint);
        remain -= 400;
        x += 400;
        while (remain > 0)
        {
            if (remain > 300)
            {
                batch.Draw(tex, new Vector2(x, y), new Rectangle(100, 0, tex.Width - 200, tex.Height), tint);
                remain -= 300;
                x += 300;
            }
            else
            {
                batch.Draw(tex, new Vector2(x, y), new Rectangle(tex.Width - remain, 0, remain, tex.Height), tint);
                remain = 0;
            }
        }
    }

    private static void DrawSignText(Action<NPCChatPanel, Color, Rectangle> orig, NPCChatPanel self, Color color, Rectangle area)
    {
        if (CheatState.ImGuiInput || !Main.editSign)
        {
            orig(self, color, area);
            return;
        }

        Main.editSign = false;
        orig(self, color, area);
        Main.editSign = true;
        DrawWrapped(area.TopLeft() + new Vector2(20f, 20f), Main.npcChatText, color);
    }

    private static void DrawChestName(Action<SpriteBatch> orig, SpriteBatch batch)
    {
        if (CheatState.ImGuiInput || !Main.editChest)
        {
            orig(batch);
            return;
        }

        var color = Color.White * (1f - (255f - Main.mouseTextColor) / 255f * 0.5f);
        color.A = byte.MaxValue;
        var font = FontAssets.MouseText.Value;
        var pos = new Vector2(504f, Main.instance.invBottom);
        Blink(Main.instance);
        DrawField(batch, font, Main.npcChatText, pos, color, Main.instance.textBlinkerState == 1, Vector2.One, Editor.Caret);
        Main.instance.SetIMEPanelAnchor(pos + new Vector2(PrefixWidth(Editor.Text, Editor.Caret), 56f), 0f);
    }

    private static void DrawWrapped(Vector2 origin, string text, Color color)
    {
        var font = FontAssets.MouseText.Value;
        var lines = Utils.WordwrapString(text, font, 460, 10, out _);
        int remaining = Editor.Caret;
        int selA = Editor.SelStart;
        int selB = Editor.SelEnd;
        int consumed = 0;
        for (int i = 0; i < lines.Length; i++)
        {
            string line = lines[i];
            if (line is null)
                continue;
            var pos = origin + new Vector2(0f, i * 30);
            if (Editor.HasSelection)
                DrawLineSelection(pos, line, consumed, selA, selB);
            consumed += line.Length;
            if (remaining > line.Length)
            {
                remaining -= line.Length;
                continue;
            }

            if (remaining >= 0)
            {
                Blink(Main.instance);
                DrawCaretAndComposition(Main.spriteBatch, font, line, pos, color, Main.instance.textBlinkerState == 1, Vector2.One, remaining);
                remaining = int.MinValue;
            }
        }
    }

    private static void DrawField(SpriteBatch batch, ReLogic.Graphics.DynamicSpriteFont font, string text, Vector2 pos, Color color, bool blink, Vector2 scale, int caret = -1, int origin = 0)
    {
        if (Editor.HasSelection)
            DrawLineSelection(pos, text, origin, Editor.SelStart, Editor.SelEnd);
        int hovered = -1;
        var snippets = ChatManager.ParseMessage(text, color).ToArray();
        ChatManager.DrawColorCodedStringWithShadow(batch, font, snippets, pos, 0f, Vector2.Zero, scale, out hovered);
        if (hovered > -1 && !PlayerInput.IgnoreMouseInterface)
        {
            snippets[hovered].OnHover();
            if (Main.mouseLeft && Main.mouseLeftRelease)
                snippets[hovered].OnClick();
        }

        if (caret >= 0)
            DrawCaretAndComposition(batch, font, text, pos, color, blink, scale, caret);
    }

    private static void DrawCaretAndComposition(SpriteBatch batch, ReLogic.Graphics.DynamicSpriteFont font, string text, Vector2 pos, Color color, bool blink, Vector2 scale, int caret = -1)
    {
        int index = caret < 0 ? Editor.Caret : caret;
        index = Clamp(index, 0, text.Length);
        var at = pos + new Vector2(PrefixWidth(text, index), 0f);
        string composition = Ime.CompositionString ?? "";
        if (composition.Length > 0)
        {
            ChatManager.DrawColorCodedStringWithShadow(batch, font, composition, at, Main.imeCompositionStringColor, 0f, Vector2.Zero, scale);
            at.X += font.MeasureString(composition).X * scale.X;
        }

        if (blink)
        {
            var dest = new Rectangle((int)at.X, (int)at.Y + 3, 2, (int)(16 * scale.Y));
            batch.Draw(TextureAssets.MagicPixel.Value, dest, color);
        }
    }

    private static void DrawLineSelection(Vector2 pos, string line, int origin, int selA, int selB)
    {
        int a = Clamp(selA - origin, 0, line.Length);
        int b = Clamp(selB - origin, 0, line.Length);
        if (b <= a)
            return;
        float x0 = PrefixWidth(line, a);
        float x1 = PrefixWidth(line, b);
        var dest = new Rectangle((int)(pos.X + x0), (int)pos.Y, Math.Max(1, (int)(x1 - x0)), 20);
        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, dest, new Color(50, 110, 200, 90));
    }

    private static void Blink(Main self)
    {
        if (++self.textBlinkerCount < 20)
            return;
        self.textBlinkerState = self.textBlinkerState == 0 ? 1 : 0;
        self.textBlinkerCount = 0;
    }

    private static float PrefixWidth(string text, int index)
    {
        index = Clamp(index, 0, text.Length);
        if (index == 0)
            return 0f;
        return ChatManager.GetStringSize(FontAssets.MouseText.Value, text[..index], Vector2.One).X;
    }
}
