using System;
using Hakoniwa.Core;
using Hexa.NET.ImGui;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Vector2 = System.Numerics.Vector2;
using Vector3 = System.Numerics.Vector3;
using Vector4 = System.Numerics.Vector4;

namespace Hakoniwa.UI.Windows;

public sealed class ItemEditorWindow
{
    public string Title => "高级物品属性编辑器 (Custom Weapon Editor)###HakoniwaItemEditorWindow";
    public bool IsOpen { get; set; }

    public CustomWeaponData Data { get; private set; } = new();

    private string _cwCommandText = string.Empty;
    private string _parseError = string.Empty;
    private bool _enableColor;
    private Vector3 _colorRgb = new(1f, 1f, 1f);

    public void OpenWith(short netId)
    {
        Data = new CustomWeaponData(netId);
        SyncFromData();
        IsOpen = true;
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    public void OpenWith(Item item)
    {
        Data = CustomWeaponData.FromItem(item);
        SyncFromData();
        IsOpen = true;
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    public void OpenWith(CustomWeaponData data)
    {
        Data = data;
        SyncFromData();
        IsOpen = true;
        SoundEngine.PlaySound(SoundID.MenuOpen);
    }

    public void Close() => IsOpen = false;

    private void SyncFromData()
    {
        _enableColor = Data.Color.HasValue;
        if (Data.Color.HasValue)
        {
            var c = Data.Color.Value;
            _colorRgb = new Vector3(c.R / 255f, c.G / 255f, c.B / 255f);
        }
        else
        {
            _colorRgb = new Vector3(1f, 1f, 1f);
        }
        _cwCommandText = Data.ToCwCommand();
        _parseError = string.Empty;
    }

    public void Draw()
    {
        if (!IsOpen)
            return;

        ImGui.SetNextWindowSize(new Vector2(680f, 600f), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSizeConstraints(new Vector2(540f, 440f), new Vector2(1920f, 1080f));
        bool open = IsOpen;
        if (ImGui.Begin(Title, ref open, ImGuiWindowFlags.NoCollapse))
        {
            DrawContent();
        }
        IsOpen = open;
        ImGui.End();
    }

    private void DrawContent()
    {
        if (Data.ItemNetId <= 0)
        {
            Ui.Heading(Icons.Pencil, "高级物品属性编辑器");
            ImGui.TextDisabled("未选择任何物品。请在物品库或背包中右键/快捷选中一件物品进行高级编辑。");
            return;
        }

        Ui.BeginScroll("editor-scroll");

        // 1. 顶部基础概览卡片
        DrawHeaderCard();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 2. 属性分栏 (双列像素卡片)
        DrawPropertyCards();

        ImGui.Spacing();
        ImGui.Separator();
        ImGui.Spacing();

        // 3. 底部命令与动作工具栏
        DrawCommandAndActions();

        ImGui.EndChild();
    }

    private void DrawHeaderCard()
    {
        ImGui.BeginChild("header-card", new Vector2(0, 72f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        // 左侧大物品槽
        var slotPos = ImGui.GetCursorScreenPos() + new Vector2(4f, 4f);
        var slotSize = new Vector2(56f, 56f);
        Ui.DrawPixelSlot(dl, slotPos, slotPos + slotSize, false, true);

        // 如果设置了颜色则带染色预览
        UiIcons.DrawItemDirect(dl, slotPos + slotSize * 0.5f, Data.ItemNetId, 38f);

        ImGui.SetCursorScreenPos(slotPos + new Vector2(68f, 0f));

        ImGui.BeginGroup();
        string origName = Lang.GetItemNameValue(Data.ItemNetId);
        ImGui.TextColored(Ui.Gold, $"{origName} (ID: #{Data.ItemNetId})");

        ImGui.SameLine(0f, 16f);
        ImGui.TextDisabled($"原版基础伤害: {GetDefaultDamage(Data.ItemNetId)} | 动画: {GetDefaultAnim(Data.ItemNetId)}");

        // 名称输入
        string customName = Data.Name;
        ImGui.SetNextItemWidth(260f);
        if (ImGui.InputTextWithHint("##cwName", "自定义武器名称 (用于 /cwadd)", ref customName, (UIntPtr)64))
        {
            Data.Name = customName;
            _cwCommandText = Data.ToCwCommand();
        }

        ImGui.SameLine(0f, 10f);

        // 前缀选择
        DrawPrefixCombo();

        ImGui.EndGroup();

        ImGui.EndChild();
    }

    private void DrawPrefixCombo()
    {
        int currentPre = Data.Prefix.GetValueOrDefault(0);
        string currentPreLabel = currentPre == 0 ? "无前缀" : GetPrefixLabel((byte)currentPre);

        ImGui.SetNextItemWidth(160f);
        if (ImGui.BeginCombo("##prefixCombo", currentPreLabel))
        {
            if (ImGui.Selectable("无前缀", currentPre == 0))
            {
                Data.Prefix = null;
                _cwCommandText = Data.ToCwCommand();
            }

            // 常用经典极品前缀
            ImGui.Separator();
            int[] popular = [81, 82, 83, 84, 59, 57, 15, 60, 61];
            for (int i = 0; i < popular.Length; i++)
            {
                byte pid = (byte)popular[i];
                if (ImGui.Selectable(GetPrefixLabel(pid), currentPre == pid))
                {
                    Data.Prefix = pid;
                    _cwCommandText = Data.ToCwCommand();
                }
            }

            ImGui.Separator();
            // 全部前缀 1..84
            for (byte pid = 1; pid <= 84; pid++)
            {
                if (ImGui.Selectable(GetPrefixLabel(pid), currentPre == pid))
                {
                    Data.Prefix = pid;
                    _cwCommandText = Data.ToCwCommand();
                }
            }

            ImGui.EndCombo();
        }
    }

    private static string GetPrefixLabel(byte pid)
    {
        if (pid <= 0) return "无前缀";
        string name = pid < Lang.prefix.Length && Lang.prefix[pid] != null ? Lang.prefix[pid].Value : $"前缀 #{pid}";
        return $"{pid}: {name}";
    }

    private void DrawPropertyCards()
    {
        float availW = ImGui.GetContentRegionAvail().X;
        float halfW = (availW - 8f) * 0.5f;

        // 左列卡片: 战斗与弹道
        ImGui.BeginChild("prop-card-left", new Vector2(halfW, 250f), ImGuiChildFlags.Borders);
        var dl = ImGui.GetWindowDrawList();
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        Ui.Heading(Icons.Zap, "战斗与弹道属性 (Combat)");

        // 基础伤害
        int dmg = Data.Damage.GetValueOrDefault(0);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputInt("基础伤害", ref dmg))
        {
            Data.Damage = (ushort)Math.Max(0, Math.Min(65535, dmg));
            _cwCommandText = Data.ToCwCommand();
        }

        // 击退
        float knock = Data.Knockback.GetValueOrDefault(0f);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputFloat("击退强度", ref knock, 0.5f, 1f, "%.2f"))
        {
            Data.Knockback = (float)Math.Max(0, knock);
            _cwCommandText = Data.ToCwCommand();
        }

        // 使用间隔 / 攻速
        int time = Data.UseTime.GetValueOrDefault(0);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputInt("使用间隔 (帧)", ref time))
        {
            Data.UseTime = (ushort)Math.Max(1, Math.Min(65535, time));
            _cwCommandText = Data.ToCwCommand();
        }

        // 动画时长
        int anim = Data.UseAnimation.GetValueOrDefault(0);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputInt("动画时长 (帧)", ref anim))
        {
            Data.UseAnimation = (ushort)Math.Max(1, Math.Min(65535, anim));
            _cwCommandText = Data.ToCwCommand();
        }

        int proj = Data.ShootProjectileId.GetValueOrDefault(0);
        if (IdPicker.Draw("发射弹幕", ref proj, IdPicker.Kind.Projectile))
        {
            Data.ShootProjectileId = (short)Math.Max(0, proj);
            _cwCommandText = Data.ToCwCommand();
        }

        // 弹幕射速
        float speed = Data.ShootSpeed.GetValueOrDefault(0f);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputFloat("弹幕飞行射速", ref speed, 0.5f, 1f, "%.2f"))
        {
            Data.ShootSpeed = (float)Math.Max(0, speed);
            _cwCommandText = Data.ToCwCommand();
        }

        // 武器大小缩放
        float scale = Data.Scale.GetValueOrDefault(1f);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputFloat("贴图缩放倍率", ref scale, 0.1f, 0.5f, "%.2f"))
        {
            Data.Scale = (float)Math.Max(0.1f, Math.Min(50f, scale));
            _cwCommandText = Data.ToCwCommand();
        }

        ImGui.EndChild();

        ImGui.SameLine(0f, 8f);

        // 右列卡片: 弹药、染色与物理
        ImGui.BeginChild("prop-card-right", new Vector2(halfW, 250f), ImGuiChildFlags.Borders);
        Ui.DrawPixelPanel(dl, ImGui.GetWindowPos(), ImGui.GetWindowPos() + ImGui.GetWindowSize());

        Ui.Heading(Icons.Colors, "染色、弹药与物理 (Physics)");

        // 物品染色
        if (ImGui.Checkbox("启用自定义染色", ref _enableColor))
        {
            if (_enableColor)
            {
                Data.Color = new Color(_colorRgb.X, _colorRgb.Y, _colorRgb.Z);
            }
            else
            {
                Data.Color = null;
            }
            _cwCommandText = Data.ToCwCommand();
        }

        if (_enableColor)
        {
            ImGui.SetNextItemWidth(halfW - 90f);
            if (ImGui.ColorEdit3("染色色值", ref _colorRgb, ImGuiColorEditFlags.NoInputs))
            {
                Data.Color = new Color(_colorRgb.X, _colorRgb.Y, _colorRgb.Z);
                _cwCommandText = Data.ToCwCommand();
            }
        }

        // 堆叠数量
        int stack = Data.Stack.GetValueOrDefault(1);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputInt("堆叠数量", ref stack))
        {
            Data.Stack = (short)Math.Max(1, Math.Min(9999, stack));
            _cwCommandText = Data.ToCwCommand();
        }

        int ammo = Data.AmmoIdentifier.GetValueOrDefault(0);
        if (IdPicker.Draw("弹药类别", ref ammo, IdPicker.Kind.Ammo))
        {
            Data.AmmoIdentifier = ammo > 0 ? (short)ammo : null;
            _cwCommandText = Data.ToCwCommand();
        }

        int useAmmo = Data.UseAmmoIdentifier.GetValueOrDefault(0);
        if (IdPicker.Draw("消耗弹药", ref useAmmo, IdPicker.Kind.Ammo))
        {
            Data.UseAmmoIdentifier = useAmmo > 0 ? (short)useAmmo : null;
            _cwCommandText = Data.ToCwCommand();
        }

        // 不消耗弹药
        bool notAmmo = Data.NotAmmo.GetValueOrDefault(false);
        if (ImGui.Checkbox("无限弹药 / 不消耗 (notammo)", ref notAmmo))
        {
            Data.NotAmmo = notAmmo;
            _cwCommandText = Data.ToCwCommand();
        }

        // 掉落物宽/高
        int width = Data.DropAreaWidth.GetValueOrDefault(0);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputInt("掉落碰撞箱宽", ref width))
        {
            Data.DropAreaWidth = width > 0 ? (short)width : null;
            _cwCommandText = Data.ToCwCommand();
        }

        int height = Data.DropAreaHeight.GetValueOrDefault(0);
        ImGui.SetNextItemWidth(halfW - 90f);
        if (ImGui.InputInt("掉落碰撞箱高", ref height))
        {
            Data.DropAreaHeight = height > 0 ? (short)height : null;
            _cwCommandText = Data.ToCwCommand();
        }

        ImGui.EndChild();
    }

    private void DrawCommandAndActions()
    {
        Ui.Heading(Icons.Script, "CustomWeapon (/cw) 命令与快捷应用");

        // /cw 命令框
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X - 100f);
        ImGui.InputText("##cwCmdLine", ref _cwCommandText, (UIntPtr)1024);

        ImGui.SameLine(0f, 6f);
        if (ImGui.Button("解析命令##parseBtn", new Vector2(94f, 22f)))
        {
            if (CustomWeaponData.TryParseCwCommand(_cwCommandText, out var parsed, out var err))
            {
                Data = parsed;
                SyncFromData();
                Notices.Post("已成功解析并载入 /cw 命令属性");
            }
            else
            {
                _parseError = err ?? "解析失败";
                Notices.Post($"解析失败: {_parseError}");
            }
        }

        if (!string.IsNullOrEmpty(_parseError))
        {
            ImGui.TextColored(new Vector4(1f, 0.4f, 0.4f, 1f), $"错误: {_parseError}");
        }

        ImGui.Spacing();

        // 操作按钮行
        float availW = ImGui.GetContentRegionAvail().X;
        float btnW = (availW - 16f) / 5f;
        float btnH = 28f;

        // 1. 复制 /cw 命令
        if (ImGui.Button("复制 /cw 命令", new Vector2(btnW, btnH)))
        {
            string cmd = Data.ToCwCommand();
            ImGui.SetClipboardText(cmd);
            Notices.Post("已复制 /cwadd 命令到剪贴板");
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("将当前自定义属性生成为 BossProject CustomWeapon 兼容的 /cwadd 命令");

        ImGui.SameLine(0f, 4f);

        // 2. 粘贴并导入
        if (ImGui.Button("粘贴并导入", new Vector2(btnW, btnH)))
        {
            string clip = Ui.GetClipboardText();
            if (CustomWeaponData.TryParseCwCommand(clip, out var parsed, out var err))
            {
                Data = parsed;
                SyncFromData();
                Notices.Post("已成功从剪贴板导入 /cw 武器");
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else
            {
                Notices.Post($"导入失败: {err}");
            }
        }

        ImGui.SameLine(0f, 4f);

        // 3. 放入背包
        if (ImGui.Button("放入背包", new Vector2(btnW, btnH)))
        {
            Data.GiveToLocalPlayer(fullStack: true);
            SoundEngine.PlaySound(SoundID.Grab);
        }

        ImGui.SameLine(0f, 4f);

        // 4. 应用到手持物品
        if (ImGui.Button("应用到手持", new Vector2(btnW, btnH)))
        {
            if (Main.LocalPlayer.active && !Main.LocalPlayer.HeldItem.IsAir)
            {
                Data.ApplyToItem(Main.LocalPlayer.HeldItem);
                Notices.Post($"已将属性直接应用到当前手持物品: {Main.LocalPlayer.HeldItem.Name}");
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else if (!Main.mouseItem.IsAir)
            {
                Data.ApplyToItem(Main.mouseItem);
                Notices.Post($"已将属性直接应用到鼠标抓取物品: {Main.mouseItem.Name}");
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            else
            {
                Notices.Post("当前未手持或抓取任何物品");
            }
        }

        ImGui.SameLine(0f, 4f);

        // 5. 还原默认
        if (ImGui.Button("还原原版", new Vector2(btnW, btnH)))
        {
            Data.LoadFromDefault(Data.ItemNetId);
            SyncFromData();
            Notices.Post("已恢复为原版默认属性");
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    private static int GetDefaultDamage(short netId)
    {
        var item = new Item();
        item.SetDefaults(netId);
        return item.damage;
    }

    private static int GetDefaultAnim(short netId)
    {
        var item = new Item();
        item.SetDefaults(netId);
        return item.useAnimation;
    }
}
