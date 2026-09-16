using System;
using System.Runtime.InteropServices;
using Hexa.NET.ImGui;
using Xunit;
using Xunit.Abstractions;

namespace Hakoniwa.Tests;

public sealed class ImGuiPlatformTests(ITestOutputHelper output)
{
    private delegate void PlatformSetImeDataDelegate(IntPtr ctx, IntPtr viewport, IntPtr data);

    [Fact]
    public unsafe void Context_Initializes_AndSupportsImeDelegates()
    {
        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);
        var platform = ImGui.GetPlatformIO();
        
        output.WriteLine($"PlatformSetImeDataFn default: {(IntPtr)platform.PlatformSetImeDataFn}");

        // 检查 ImGuiPlatformImeData 结构大小与字段
        output.WriteLine($"ImGuiPlatformImeData size: {sizeof(ImGuiPlatformImeData)}");
        foreach (var field in typeof(ImGuiPlatformImeData).GetFields())
        {
            output.WriteLine($"  Field: {field.FieldType.Name} {field.Name}");
        }

        ImGui.DestroyContext(ctx);
    }
}
