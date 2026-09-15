# Hakoniwa Agent Notes

箱庭工坊：挂进 Terraria 1.4.5.8 的内置箱庭构筑 / 地图编辑工具。唯一运行时产物 `Hakoniwa.exe`（`net48` + C# latest + PolySharp）。交流用中文。

## 原版源码（只读）

- 反编译目录：`G:\Terraria1458`（ILSpy 工程导出，按命名空间分文件）
- 对应程序：`D:\SteamLibrary\steamapps\common\Terraria\Terraria.exe`（FileVersion **1.4.5.8**）
- **只读对照用**。禁止改、格式化、重编译、当工作区提交。查 `Main` / `Player` / `WorldGen` / `Tile` / `Lighting` 等原版逻辑时读这里。
- 旧版对照：`G:\Terraria1457`（1.4.5.7）

常用入口（1458）：

- `G:\Terraria1458\Terraria\Main.cs`
- `G:\Terraria1458\Terraria\Player.cs`
- `G:\Terraria1458\Terraria\WorldGen.cs`
- `G:\Terraria1458\Terraria\Tile.cs`

## Publicizer

编译期用 `BepInEx.AssemblyPublicizer.MSBuild` 把 `Terraria.exe` **全部成员公开**，直接写 `Terraria.Player.tileRangeX` 这类原版成员，不要用反射摸私有 API。

- 改的是 `obj/` 里的引用副本，运行时仍加载玩家原版 exe
- `$(TerrariaDir)` 默认 Steam 安装目录，不要把游戏 exe 拷进输出目录（`Private=false`）

## 架构

单项目高内聚，禁止再拆运行时 DLL。测试项目可以独立存在。

| 命名空间 | 职责 |
|---|---|
| `Hakoniwa.Core` | MonoMod Hook 生命周期、`TileAccessor`、规则突破 |
| `Hakoniwa.Engine` | 选区 / 笔刷 / 变换 / `.schem` |
| `Hakoniwa.UI` | ImGui 面板 + 世界投影状态 |

分层：`UI → Engine → Core`。Core 不要反向依赖 UI。

## 构建 / 运行

工程是 x86（对齐原版 Terraria.exe）。输出直接进 Steam 目录：`D:\SteamLibrary\steamapps\common\Terraria\Hakoniwa.exe`。启动 **Hakoniwa.exe**，不要启动 `Terraria.exe`。

```text
dotnet build Hakoniwa.slnx
dotnet test  Hakoniwa.slnx
```

游戏内 `Insert` 显隐面板。地图右键 / 世界中键传送。

`dev-docs/` 是 gitignore 的内部规格，改行为前先读：

- `dev-docs/technical-spec.md`
- `dev-docs/development-roadmap.md`
- `dev-docs/hook-and-tools-decision.md`

## 约束

- KISS / YAGNI / 行数负增长；函数超过约 80 行或两处以上重复立刻收。
- 只在用户输入和外部 IO 做校验，内部直接 throw。
- `TileDataBlock.TileFrameX/Y` 必须是 `short`（原版 `Tile.frameX/Y` 是 short，压成 byte 会裁掉家具帧）。
- 不要在 UI 里用 emoji。
- 不要把 `G:\Terraria1458` 当可编译依赖；编译只引用 Steam 的 `Terraria.exe`。
