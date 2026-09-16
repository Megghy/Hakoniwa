using System;
using System.Numerics;
using Hexa.NET.ImGui;
using Terraria;
using Terraria.ID;

namespace Hakoniwa.UI.Windows;

/// <summary>
/// 现代化 ImGui 标牌编辑器 (多行编辑、撤销重做、富文本复制粘贴)
/// </summary>
public sealed class SignEditorWindow : IWindow
{
    public string Title => "标牌编辑 (Sign Editor)###HakoniwaSignEditor";
    public string Label => "标牌";
    public bool IsOpen { get; set; }

    private string _textBuffer = string.Empty;
    private int _lastActiveSign = -1;

    public void UpdateSignState()
    {
        if (Main.gameMenu || !Main.LocalPlayer.active)
        {
            IsOpen = false;
            _lastActiveSign = -1;
            return;
        }

        if (Main.editSign)
        {
            int currentSign = Main.LocalPlayer.sign;
            if (!IsOpen || _lastActiveSign != currentSign)
            {
                IsOpen = true;
                _lastActiveSign = currentSign;
                _textBuffer = GetCurrentSignText(currentSign);
            }
        }
        else if (IsOpen && !Main.editSign)
        {
            IsOpen = false;
            _lastActiveSign = -1;
        }
    }

    public void Draw()
    {
        if (!IsOpen || Main.gameMenu)
            return;

        var io = ImGui.GetIO();
        ImGui.SetNextWindowSize(new Vector2(440f, 320f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowPos(new Vector2(io.DisplaySize.X * 0.5f - 220f, io.DisplaySize.Y * 0.5f - 160f), ImGuiCond.FirstUseEver);

        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            UiIcons.DrawItem(ItemID.Sign, 22f);
            ImGui.SameLine();
            ImGui.TextColored(new Vector4(0.55f, 0.75f, 1f, 1f), "编辑标牌内容 (支持换行与快捷键):");

            ImGui.InputTextMultiline("##sign_text_input", ref _textBuffer, (UIntPtr)2048, new Vector2(-1, 200f));

            ImGui.TextDisabled($"字符计数: {_textBuffer.Length} / 2048");
            ImGui.SameLine(300f);

            if (ImGui.Button("保存", new Vector2(60f, 26f)))
                SaveAndClose();

            ImGui.SameLine();
            if (ImGui.Button("取消", new Vector2(60f, 26f)))
                CancelAndClose();

            if (ImGui.IsKeyPressed(ImGuiKey.Escape))
                CancelAndClose();
        }

        if (!open)
            CancelAndClose();

        ImGui.End();
    }

    private static string GetCurrentSignText(int signId)
    {
        if (signId >= 0 && signId < Main.sign.Length && Main.sign[signId] != null)
            return Main.sign[signId].text ?? string.Empty;

        return Main.npcChatText ?? string.Empty;
    }

    private void SaveAndClose()
    {
        int signId = Main.LocalPlayer.sign;
        if (signId >= 0 && signId < Main.sign.Length && Main.sign[signId] != null)
        {
            Sign.TextSign(signId, _textBuffer);
            if (Main.netMode == 1)
                NetMessage.SendData(47, -1, -1, null, signId, Main.myPlayer);
        }

        Main.editSign = false;
        Main.npcChatText = _textBuffer;
        Main.signBubble = false;
        IsOpen = false;
    }

    private void CancelAndClose()
    {
        Main.editSign = false;
        Main.signBubble = false;
        IsOpen = false;
    }
}
