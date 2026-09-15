using ImGuiNET;
using Terraria;
using Num = System.Numerics;

namespace Hakoniwa.UI.Windows;

public sealed class ItemEditorWindow : IWindow
{
    public string Title => "Hakoniwa - Held Item###HakoniwaItemEdit";
    public string Label => "Gear";
    public bool IsOpen { get; set; }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Num.Vector2(320, 320), ImGuiCond.FirstUseEver);
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            if (Main.gameMenu || !Main.LocalPlayer.active)
                ImGui.TextUnformatted("Enter a world to edit items.");
            else
                DrawItem(Held());
        }

        IsOpen = open;
        ImGui.End();
    }

    private static Item Held()
    {
        if (!Main.mouseItem.IsAir)
            return Main.mouseItem;
        return Main.LocalPlayer.HeldItem;
    }

    private static void DrawItem(Item item)
    {
        if (item.IsAir)
        {
            ImGui.TextUnformatted("Hold or pick up an item.");
            return;
        }

        ImGui.TextUnformatted(item.Name);
        int damage = item.damage;
        if (ImGui.InputInt("Damage", ref damage))
            item.damage = damage;
        int useTime = item.useTime;
        if (ImGui.InputInt("Use time", ref useTime))
            item.useTime = useTime;
        int useAnim = item.useAnimation;
        if (ImGui.InputInt("Use animation", ref useAnim))
            item.useAnimation = useAnim;
        float shoot = item.shootSpeed;
        if (ImGui.InputFloat("Shoot speed", ref shoot))
            item.shootSpeed = shoot;
        float knock = item.knockBack;
        if (ImGui.InputFloat("Knockback", ref knock))
            item.knockBack = knock;
        int crit = item.crit;
        if (ImGui.InputInt("Crit", ref crit))
            item.crit = crit;
        ImGui.Checkbox("Auto reuse", ref item.autoReuse);
    }
}
