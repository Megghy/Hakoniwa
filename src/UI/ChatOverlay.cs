using System;
using System.Collections.Generic;
using System.Numerics;
using Hexa.NET.ImGui;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.UI.Chat;

namespace Hakoniwa.UI;

/// <summary>
/// 现代化 ImGui 聊天输入框 (支持光标移动、撤销重做、历史记录与 TShock 命令补全)
/// </summary>
public sealed class ChatOverlay
{
    private record struct CommandHint(string Command, string Syntax, string Description);

    private static readonly CommandHint[] Commands =
    [
        new("/help", "/help [page/command]", "查看帮助或指令说明"),
        new("/item", "/item <name/id> [amount] [prefix]", "给予自己指定物品"),
        new("/give", "/give <item> <player> [amount] [prefix]", "给予玩家指定物品"),
        new("/tp", "/tp <player>", "传送到指定玩家"),
        new("/tphere", "/tphere <player>", "将玩家传送到自己身边"),
        new("/warp", "/warp <name>", "传送到指定地标"),
        new("/setwarp", "/setwarp <name>", "设置地标"),
        new("/home", "/home", "传送回出生点或床"),
        new("/spawn", "/spawn", "传送回世界初始出生点"),
        new("/god", "/god", "切换上帝无敌模式"),
        new("/heal", "/heal [player]", "恢复生命与魔力"),
        new("/buff", "/buff <name/id> [duration]", "给予增益 Buff"),
        new("/clear", "/clear [items/projectiles/mobs]", "清理地面掉落物或弹幕"),
        new("/time", "/time <day/night/noon/midnight/12:00>", "调整世界时间"),
        new("/wind", "/wind <speed>", "调整风速"),
        new("/rain", "/rain <start/stop>", "控制降雨天气"),
        new("/world", "/world", "查看或调整世界状态"),
        new("/spawnmob", "/spawnmob <name/id> [amount]", "生成指定怪物或生物"),
        new("/butcher", "/butcher [friendly/npc]", "清除所有怪物"),
        new("/kill", "/kill <player>", "击杀指定玩家"),
        new("/slap", "/slap <player> [damage]", "拍击玩家"),
        new("/who", "/who", "查看当前在线玩家列表"),
        new("/me", "/me <action>", "发送动作信息"),
        new("/motd", "/motd", "查看服务器今日公告"),
        new("/group", "/group <list/add/del/perm>", "管理权限组"),
        new("/user", "/user <list/add/del/group>", "管理注册用户"),
        new("/auth", "/auth <code>", "验证为服务器管理员"),
        new("/login", "/login <password>", "登录 TShock 账号"),
        new("/register", "/register <password>", "注册 TShock 账号"),
        new("/ban", "/ban <add/del/list> <player>", "封禁玩家"),
        new("/kick", "/kick <player> [reason]", "踢出玩家"),
        new("/mute", "/mute <player>", "禁言玩家"),
        new("/region", "/region <define/protect/name>", "区域保护与管理"),
        new("/invsee", "/invsee <player>", "查看指定玩家背包"),
    ];

    public bool IsOpen { get; private set; }

    private string _inputBuffer = string.Empty;
    private readonly List<string> _history = [];
    private int _historyIndex = -1;
    private bool _focusRequested;
    private bool _ignoreEnter;
    private readonly List<CommandHint> _matchedCandidates = [];
    private int _selectedCandidateIndex;

    public void Open()
    {
        IsOpen = true;
        _focusRequested = true;
        _ignoreEnter = true;
        _historyIndex = -1;
        Main.drawingPlayerChat = false;
        Main.clrInput();
    }

    public void Close()
    {
        IsOpen = false;
        _inputBuffer = string.Empty;
        _historyIndex = -1;
    }

    public unsafe void Draw()
    {
        if (!IsOpen || Main.gameMenu)
            return;

        var io = ImGui.GetIO();
        float width = Math.Min(io.DisplaySize.X - 40f, 720f);
        float posX = 20f;
        float posY = io.DisplaySize.Y - 56f;

        UpdateCandidateMatches();

        // 绘制命令补全浮窗
        if (_matchedCandidates.Count > 0 && _inputBuffer.StartsWith("/"))
            DrawAutocompletePopup(new Vector2(posX, posY), width);

        // 绘制底部主聊天条
        ImGui.SetNextWindowPos(new Vector2(posX, posY));
        ImGui.SetNextWindowSize(new Vector2(width, 42f));
        if (ImGui.Begin("##HakoniwaChatInputBar", Ui.Overlay | ImGuiWindowFlags.NoMove))
        {
            ImGui.AlignTextToFramePadding();
            ImGui.TextColored(new Vector4(0.35f, 0.78f, 0.98f, 1f), "说:");
            ImGui.SameLine();

            if (_ignoreEnter && !ImGui.IsKeyDown(ImGuiKey.Enter))
                _ignoreEnter = false;

            if (_focusRequested || !ImGui.IsWindowFocused())
                ImGui.SetKeyboardFocusHere();

            ImGui.SetNextItemWidth(width - 90f);
            var inputFlags = ImGuiInputTextFlags.EnterReturnsTrue |
                             ImGuiInputTextFlags.CallbackHistory |
                             ImGuiInputTextFlags.CallbackCompletion;

            bool submitted = ImGui.InputText("##chat_text_box", ref _inputBuffer, (UIntPtr)512, inputFlags, TextCallback);
            bool focused = ImGui.IsItemActive() || ImGui.IsItemFocused();
            _focusRequested = !focused;

            ImGui.SameLine();
            bool sendClicked = ImGui.Button("发送", new Vector2(46f, 0f));
            if (sendClicked || (submitted && !_ignoreEnter))
                SubmitMessage();

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                Close();
        }

        ImGui.End();
    }

    private void DrawAutocompletePopup(Vector2 anchor, float width)
    {
        int maxShow = Math.Min(6, _matchedCandidates.Count);
        float popupHeight = maxShow * 26f + 16f;
        ImGui.SetNextWindowPos(new Vector2(anchor.X, anchor.Y - popupHeight - 6f));
        ImGui.SetNextWindowSize(new Vector2(width, popupHeight));

        if (ImGui.Begin("##ChatAutocompletePopup", Ui.Overlay | ImGuiWindowFlags.NoMove | ImGuiWindowFlags.NoFocusOnAppearing))
        {
            for (int i = 0; i < _matchedCandidates.Count; i++)
            {
                var candidate = _matchedCandidates[i];
                bool isSelected = i == _selectedCandidateIndex;

                if (isSelected)
                    ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.35f, 0.24f, 0.58f, 0.95f));

                if (ImGui.Selectable($"{candidate.Syntax} - {candidate.Description}##c_{i}", isSelected))
                {
                    _inputBuffer = candidate.Command + " ";
                    _focusRequested = true;
                }

                if (isSelected)
                    ImGui.PopStyleColor();
            }
        }

        ImGui.End();
    }

    private void UpdateCandidateMatches()
    {
        _matchedCandidates.Clear();
        if (!_inputBuffer.StartsWith("/"))
            return;

        string query = _inputBuffer.Split(' ')[0];
        foreach (var cmd in Commands)
        {
            if (cmd.Command.StartsWith(query, StringComparison.OrdinalIgnoreCase))
                _matchedCandidates.Add(cmd);
        }

        if (_selectedCandidateIndex >= _matchedCandidates.Count)
            _selectedCandidateIndex = 0;
    }

    private unsafe int TextCallback(IntPtr data)
    {
        var ptr = new ImGuiInputTextCallbackDataPtr((ImGuiInputTextCallbackData*)data);
        if (ptr.EventFlag == ImGuiInputTextFlags.CallbackHistory)
        {
            if (ptr.EventKey == ImGuiKey.UpArrow)
            {
                if (_history.Count > 0)
                {
                    if (_historyIndex == -1)
                        _historyIndex = _history.Count - 1;
                    else if (_historyIndex > 0)
                        _historyIndex--;

                    SetInputBuffer(ptr, _history[_historyIndex]);
                }
            }
            else if (ptr.EventKey == ImGuiKey.DownArrow)
            {
                if (_historyIndex != -1)
                {
                    if (_historyIndex < _history.Count - 1)
                    {
                        _historyIndex++;
                        SetInputBuffer(ptr, _history[_historyIndex]);
                    }
                    else
                    {
                        _historyIndex = -1;
                        SetInputBuffer(ptr, string.Empty);
                    }
                }
            }
        }
        else if (ptr.EventFlag == ImGuiInputTextFlags.CallbackCompletion)
        {
            if (_matchedCandidates.Count > 0)
            {
                var target = _matchedCandidates[_selectedCandidateIndex].Command + " ";
                SetInputBuffer(ptr, target);
            }
        }

        return 0;
    }

    private static void SetInputBuffer(ImGuiInputTextCallbackDataPtr data, string text)
    {
        data.DeleteChars(0, data.BufTextLen);
        data.InsertChars(0, text);
    }

    private void SubmitMessage()
    {
        string text = _inputBuffer.Trim();
        if (text.Length > 0)
        {
            if (_history.Count == 0 || _history[^1] != text)
                _history.Add(text);

            SendChatMessage(text);
            SoundEngine.PlaySound(11);
        }

        Close();
    }

    private static void SendChatMessage(string text)
    {
        if (string.IsNullOrEmpty(text) || Main.gameMenu)
            return;

        if (!ChatManager.DebugCommands.Process((byte)Main.myPlayer, text))
        {
            var message = ChatManager.Commands.CreateOutgoingMessage(text);
            if (Main.netMode == 1)
                ChatHelper.SendChatMessageFromClient(message);
            else if (Main.netMode == 0)
                ChatManager.Commands.ProcessIncomingMessage(message, Main.myPlayer);
        }
    }
}
