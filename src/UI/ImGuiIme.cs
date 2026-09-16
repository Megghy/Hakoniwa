using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using ReLogic.Localization.IME;
using ReLogic.OS;
using Num = System.Numerics;

namespace Hakoniwa.UI;

internal sealed unsafe class ImGuiIme : IDisposable
{
    private readonly List<char> _chars = [];
    private readonly Action<char> _onChar;
    private readonly PlatformSetImeDataFn _imeFn;
    private Num.Vector2 _caret;
    private float _lineH = 16f;

    public bool Composing { get; private set; }

    public ImGuiIme()
    {
        _onChar = OnChar;
        Ime.AddKeyListener(_onChar);
        _imeFn = OnSetImeData;
        var platform = ImGui.GetPlatformIO();
        platform.PlatformSetImeDataFn = (void*)Marshal.GetFunctionPointerForDelegate(_imeFn);
    }

    public void Flush(ImGuiIOPtr io)
    {
        foreach (char ch in _chars)
            io.AddInputCharacter(ch);
        _chars.Clear();

        var ime = Ime;
        string composition = ime.CompositionString ?? "";
        Composing = composition.Length > 0 || ime.IsCandidateListVisible;
    }

    public void Draw()
    {
        var ime = Ime;
        string composition = ime.CompositionString ?? "";
        bool list = ime.IsCandidateListVisible && ime.CandidateCount > 0;
        if (composition.Length == 0 && !list)
            return;

        ImGui.SetNextWindowPos(_caret + new Num.Vector2(0f, _lineH), ImGuiCond.Always);
        ImGui.SetNextWindowBgAlpha(0.94f);
        ImGui.Begin("##hakoniwa-ime", Ui.Toast);
        if (composition.Length > 0)
            ImGui.TextColored(Ui.Gold, composition);
        if (list)
        {
            uint? selected = ime.SelectedCandidate;
            for (uint i = 0; i < ime.CandidateCount; i++)
            {
                string line = $"{i + 1}. {ime.GetCandidate(i) ?? ""}";
                if (selected == i)
                    ImGui.TextColored(Ui.White, line);
                else
                    ImGui.TextDisabled(line);
            }
        }

        ImGui.End();
    }

    public void Dispose()
    {
        Ime.RemoveKeyListener(_onChar);
        var platform = ImGui.GetPlatformIO();
        platform.PlatformSetImeDataFn = null;
    }

    private static IImeService Ime => Platform.Get<IImeService>();

    private void OnChar(char ch)
    {
        if (ch >= 32)
            _chars.Add(ch);
    }

    private void OnSetImeData(IntPtr ctx, IntPtr vp, IntPtr data)
    {
        var ime = new ImGuiPlatformImeDataPtr((ImGuiPlatformImeData*)data);
        _caret = ime.InputPos;
        _lineH = Math.Max(1f, ime.InputLineHeight);
    }
}
