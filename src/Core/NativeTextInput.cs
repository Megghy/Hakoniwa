using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Localization.IME;
using ReLogic.OS;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.GameContent.UI;
using Terraria.GameContent.UI.States;
using Terraria.GameInput;
using Terraria.Testing;
using Terraria.UI;
using Terraria.UI.Chat;

namespace Hakoniwa.Core;

internal static partial class NativeTextInput
{
    private static readonly TextEditor Editor = new();
    private static readonly List<string> History = [];
    private static int _historyIndex = -1;
    private static bool _multiline;

    public static bool Busy =>
        !CheatState.ImGuiInput && (Main.drawingPlayerChat || Main.editSign || Main.editChest);

    public static void Install(HookManager hooks)
    {
        hooks.RegisterDetour(Need(typeof(Main), nameof(Main.GetInputText), typeof(string), typeof(bool)), GetInputText);
        hooks.RegisterDetour(Need(typeof(Main), "DoUpdate_HandleChat"), HandleChat);
        hooks.RegisterDetour(Need(typeof(Main), nameof(Main.InputTextSign)), InputTextSign);
        hooks.RegisterDetour(Need(typeof(Main), nameof(Main.DrawPlayerChat)), DrawPlayerChat);
        hooks.RegisterDetour(Need(typeof(NPCChatPanel), "DrawText", typeof(Color), typeof(Rectangle)), DrawSignText);
        hooks.RegisterDetour(Need(typeof(ChestUI), "DrawName", typeof(SpriteBatch)), DrawChestName);
    }

    private static MethodInfo Need(Type type, string name, params Type[] args)
    {
        const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        var method = type.GetMethod(name, flags | BindingFlags.DeclaredOnly, null, args, null)
                     ?? type.GetMethod(name, flags, null, args, null);
        if (method is null)
            throw new MissingMethodException(type.FullName, name);
        return method;
    }

    private static string GetInputText(Func<string, bool, string> orig, string old, bool multiline)
    {
        if (CheatState.ImGuiInput)
            return orig(old, multiline);
        return Edit(old, multiline);
    }

    private static string Edit(string old, bool multiline)
    {
        if (Main.dedServ || !FocusHelper.AllowInputProcessing)
            return old ?? "";

        Main.inputTextEnter = false;
        Main.inputTextEscape = false;
        _multiline = multiline;
        Editor.Bind(old ?? "");

        var kb = Main.inputText;
        var prev = Main.oldInputText;
        bool composing = !string.IsNullOrEmpty(Ime.CompositionString);
        if (kb.PressingControl() && !kb.PressingAlt())
            HandleCtrl(kb, prev);
        else if (!composing)
            HandleNav(kb, prev, kb.PressingShift());

        ReadChars(kb.PressingControl());
        if (!composing && !kb.PressingControl())
        {
            HandleBackspace(kb, prev);
            if (Hit(kb, prev, Keys.Delete))
            {
                if (kb.PressingShift())
                    Cut();
                else
                    Editor.DeleteForward();
            }
        }

        HandlePointer();
        Main.oldInputText = Main.inputText;
        Main.inputText = Keyboard.GetState();
        Main.keyCount = 0;
        Main.imeCompositionActive = !string.IsNullOrEmpty(Ime.CompositionString);
        return Editor.Text;
    }

    private static void HandleCtrl(KeyboardState kb, KeyboardState prev)
    {
        if (Hit(kb, prev, Keys.Z))
            Editor.Undo();
        else if (Hit(kb, prev, Keys.Y))
            Editor.Redo();
        else if (Hit(kb, prev, Keys.A))
        {
            if (_histFocus)
                SelectAllHist();
            else
                Editor.SelectAll();
        }
        else if (Hit(kb, prev, Keys.C) || Hit(kb, prev, Keys.Insert))
            Copy();
        else if (Hit(kb, prev, Keys.X))
            Cut();
        else if (Hit(kb, prev, Keys.V))
            Paste();
        else if (Hit(kb, prev, Keys.Left))
            Editor.Move(-1, kb.PressingShift(), word: true);
        else if (Hit(kb, prev, Keys.Right))
            Editor.Move(1, kb.PressingShift(), word: true);
        else if (Hit(kb, prev, Keys.Home))
        {
            Editor.Click(0, kb.PressingShift());
        }
        else if (Hit(kb, prev, Keys.End))
            Editor.Click(Editor.Text.Length, kb.PressingShift());
        else if (Hit(kb, prev, Keys.Back))
            Editor.DeleteWord(-1);
        else if (Hit(kb, prev, Keys.Delete))
            Editor.DeleteWord(1);
        else if (Hit(kb, prev, Keys.Enter) && (Main.drawingPlayerChat || _multiline))
            Editor.Insert("\n");
    }

    private static void HandleNav(KeyboardState kb, KeyboardState prev, bool shift)
    {
        if (Hit(kb, prev, Keys.Left))
            Editor.Move(-1, shift, word: false);
        else if (Hit(kb, prev, Keys.Right))
            Editor.Move(1, shift, word: false);
        else if (Hit(kb, prev, Keys.Home))
            Editor.Home(shift);
        else if (Hit(kb, prev, Keys.End))
            Editor.End(shift);
        else if (Main.drawingPlayerChat && Hit(kb, prev, Keys.Up))
            Recall(-1);
        else if (Main.drawingPlayerChat && Hit(kb, prev, Keys.Down))
            Recall(1);
        if (Hit(kb, prev, Keys.Insert) && kb.PressingShift())
            Paste();
    }

    private static void ReadChars(bool ctrl)
    {
        if (ctrl)
            return;
        for (int i = 0; i < Main.keyCount; i++)
        {
            int code = Main.keyInt[i];
            if (code == 13)
                Main.inputTextEnter = true;
            else if (code == 27)
                Main.inputTextEscape = true;
            else if (code >= 32 && code != 127)
                Insert(Main.keyString[i]);
        }
    }

    private static void HandleBackspace(KeyboardState kb, KeyboardState prev)
    {
        bool down = kb.IsKeyDown(Keys.Back);
        bool held = down && prev.IsKeyDown(Keys.Back);
        bool fire = Hit(kb, prev, Keys.Back);
        if (held)
        {
            Main.backSpaceRate -= 0.05f;
            if (Main.backSpaceRate < 0f)
                Main.backSpaceRate = 0f;
            if (Main.backSpaceCount <= 0)
            {
                Main.backSpaceCount = (int)Math.Round(Main.backSpaceRate);
                fire = true;
            }

            Main.backSpaceCount--;
        }
        else
        {
            Main.backSpaceRate = 7f;
            Main.backSpaceCount = 15;
        }

        if (fire)
            BackspaceSmart();
    }

    private static void BackspaceSmart()
    {
        if (Editor.HasSelection || Editor.Caret == 0)
        {
            Editor.Backspace();
            return;
        }

        var snippets = ChatManager.ParseMessage(Editor.Text[..Editor.Caret], Color.White);
        if (snippets.Count > 0 && snippets[^1].DeleteWhole)
            Editor.DeleteRange(Editor.Caret - snippets[^1].TextOriginal.Length, Editor.Caret);
        else
            Editor.Backspace();
    }

    private static void Insert(string value)
    {
        value = value.Replace("\r\n", "\n").Replace('\r', '\n');
        if (!_multiline && !Main.drawingPlayerChat)
            value = value.Replace("\n", "");
        if (value.Length > 0)
            Editor.Insert(value);
    }

    private static void Copy()
    {
        string text = _histFocus ? HistSelected() : Editor.HasSelection ? Editor.Selected : Editor.Text;
        if (text.Length > 0)
            Platform.Get<IClipboard>().Value = text;
    }

    private static void Cut()
    {
        Copy();
        if (Editor.HasSelection)
            Editor.Insert("");
        else
            Editor.DeleteRange(0, Editor.Text.Length);
    }

    private static void Paste()
    {
        string clip = _multiline ? Platform.Get<IClipboard>().MultiLineValue : Platform.Get<IClipboard>().Value;
        Insert(clip ?? "");
    }

    private static void Recall(int dir)
    {
        if (History.Count == 0)
            return;
        if (_historyIndex < 0)
            _historyIndex = History.Count;
        _historyIndex = Clamp(_historyIndex + dir, 0, History.Count);
        Editor.Bind(_historyIndex == History.Count ? "" : History[_historyIndex]);
    }

    private static void HandleChat(Action orig)
    {
        if (CheatState.ImGuiInput)
        {
            orig();
            return;
        }

        if (Main.CurrentInputTextTakerOverride != null)
        {
            Main.drawingPlayerChat = false;
            return;
        }

        if (Main.editSign || PlayerInput.UsingGamepad && !DebugOptions.ForceGamepad)
            Main.drawingPlayerChat = false;
        if (!Main.drawingPlayerChat)
        {
            Main.chatMonitor.ResetOffset();
            return;
        }

        if (Main.inputTextEscape && !Main.imeCompositionActive)
            Main.drawingPlayerChat = false;

        string before = Main.chatText;
        Main.chatText = Main.GetInputText(Main.chatText);
        if (before != Main.chatText)
            SoundEngine.PlaySound(12);
        if (!Main.inputTextEnter || !Main.chatRelease)
            return;

        SubmitChat();
        Main.chatText = "";
        Main.ClosePlayerChat();
        Main.chatRelease = false;
        SoundEngine.PlaySound(11);
        Editor.Bind("");
        _historyIndex = -1;
    }

    private static void SubmitChat()
    {
        string text = Main.chatText;
        if (text.Length == 0)
            return;
        if (History.Count == 0 || History[^1] != text)
            History.Add(text);
        if (ChatManager.DebugCommands.Process((byte)Main.myPlayer, text))
            return;
        var message = ChatManager.Commands.CreateOutgoingMessage(text);
        if (Main.netMode == 1)
            ChatHelper.SendChatMessageFromClient(message);
        else if (Main.netMode == 0)
            ChatManager.Commands.ProcessIncomingMessage(message, Main.myPlayer);
    }

    private static void InputTextSign(Action orig)
    {
        if (CheatState.ImGuiInput)
        {
            orig();
            return;
        }

        if (IngameFancyUI.CanShowVirtualKeyboard(1) && UIVirtualKeyboard.KeyboardContext == 1)
            return;
        PlayerInput.WritingText = true;
        Main.instance.HandleIME();
        Main.npcChatText = Main.GetInputText(Main.npcChatText, allowMultiLine: true);
        if (Main.inputTextEnter)
        {
            Editor.Insert("\n");
            Main.npcChatText = Editor.Text;
        }
        else if (Main.inputTextEscape)
            Main.InputTextSignCancel();
    }

    private static int IndexAt(string text, float x)
    {
        if (x <= 0f)
            return 0;
        float prev = 0f;
        for (int i = 1; i <= text.Length; i++)
        {
            float width = PrefixWidth(text, i);
            if (width >= x)
                return x - prev < width - x ? i - 1 : i;
            prev = width;
        }

        return text.Length;
    }

    private static bool Hit(KeyboardState kb, KeyboardState prev, Keys key) =>
        kb.IsKeyDown(key) && !prev.IsKeyDown(key);

    private static int Clamp(int value, int min, int max) =>
        value < min ? min : value > max ? max : value;
 
    private static IImeService Ime => Platform.Get<IImeService>();
}
