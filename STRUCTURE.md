# Codebase Structure

## Directory Layout

```
Hakoniwa/
├── src/                    # 唯一应用工程
│   ├── Program.cs          # 入口：AppUpdate 后交给 GameHost
│   ├── Hakoniwa.csproj     # WinExe / net48 / x86
│   ├── set-laa.ps1         # 构建后 Large Address Aware
│   ├── Assets/Fonts/       # 拷贝到输出目录的字体
│   ├── Core/               # Hook、主机、更新、原生文本、物品/作弊
│   ├── Engine/             # 选区、工具、变换、蓝图 IO
│   │   ├── Data/           # Schematic / TileDataBlock
│   │   ├── IO/             # SchematicSerializer
│   │   └── Tools/          # ToolEngine / TransformEngine / HistoryStack
│   ├── UI/                 # ImGui 后端、叠加层、工坊窗口
│   │   ├── Themes/         # HakoniwaTheme
│   │   └── Windows/        # Studio / Item / Sign / World / Character / Schematic / VanillaDebug
│   └── Properties/         # launchSettings
├── tests/Hakoniwa.Tests/   # xUnit 风格测试工程
├── libs/                   # ReLogic.dll 引用
├── .github/workflows/      # release.yml tag 构建
├── Directory.Build.props   # 共享 TFM / LangVersion / PolySharp / Version
├── Hakoniwa.sln            # 解决方案
├── ARCHITECTURE.md
└── AGENTS.md
```

## Directory Purposes

**`src/Core/`:**
- Purpose: 进程引导、MonoMod 生命周期、对 Terraria 世界与规则的读写突破。
- Contains: `GameHost`、`AppUpdate`、`HookManager`、`CheatHooks`、`MapReveal`、原生文本编辑、格子访问、物品目录、背包与角色外观套装。
- Key files: `GameHost.cs`, `AppUpdate.cs`, `HookManager.cs`, `CheatHooks.cs`, `MapReveal.cs`, `CharacterPacks.cs`, `TextEditor.cs`, `NativeTextInput.cs`, `TileAccessor.cs`, `SchematicWorld.cs`

**`src/Engine/`:**
- Purpose: 与游戏循环解耦的编辑核心：快照、选区、笔刷、历史、蓝图格式。
- Contains: 值类型数据、序列化、工具算法。
- Key files: `EditorSession.cs`, `TileStrokeRecorder.cs`, `Tools/ToolEngine.cs`, `Tools/HistoryStack.cs`, `IO/SchematicSerializer.cs`

**`src/UI/`:**
- Purpose: Dear ImGui 交互与世界空间投影。
- Contains: 后端、主题、叠加层、工坊窗口。
- Key files: `HakoniwaUi.cs`, `ImGuiBackend.cs`, `SchematicDrawer.cs`, `Windows/StudioWindow.cs`, `Windows/SchematicLibrary.cs`, `Windows/CharacterTab.cs`

**`tests/Hakoniwa.Tests/`:**
- Purpose: 引擎与 Hook 管理器的纯逻辑测试。
- Key files: `ToolEngineTests.cs`, `SchematicTests.cs`, `TextEditorTests.cs`, `HookManagerTests.cs`

**`libs/`:**
- Purpose: 游戏侧非 NuGet 程序集（`ReLogic.dll`）。不要把密钥或 `.env` 放这里。

## Key File Locations

**Entry Points:** `src/Program.cs` → `AppUpdate.TryApply` → `Core/GameHost.Run` → `Terraria.Program.LaunchGame`
**UI install:** `src/UI/HakoniwaUi.cs`（`Main.OnEngineLoad` / `OnPostDraw`）
**Configuration:** `Directory.Build.props`、`src/Hakoniwa.csproj`、`src/Properties/launchSettings.json`
**Core Logic:** `src/Core/`、`src/Engine/`
**Tests:** `tests/Hakoniwa.Tests/`

## Naming Conventions

**Files:** 类型名即文件名，PascalCase（`CheatHooks.cs`、`StudioWindow.cs`）
**Directories:** 职责分层 `Core` / `Engine` / `UI`；引擎子目录 `Data` / `IO` / `Tools`
**Namespaces:** 与目录对齐：`Hakoniwa.Core`、`Hakoniwa.Engine`、`Hakoniwa.UI.Windows`

## Where to Add New Code

**新规则突破 / Hook：** `src/Core/`，在 `CheatHooks.Install` 或独立 `*.Install(HookManager)` 注册，经 `GameHost` 安装。
**新编辑工具：** `src/Engine/Tools/`，接到 `ToolEngine` / `EditorSession`，不要在 UI 里复制格子算法。
**新蓝图字段：** `src/Engine/Data/` + `src/Engine/IO/SchematicSerializer.cs`，并补 `tests/Hakoniwa.Tests/SchematicTests.cs`。
**新 ImGui 窗口：** `src/UI/Windows/`，在 `HakoniwaUi` 持有实例并在 `Render` 中绘制。
**新世界叠加：** `src/UI/` 的 Overlay 类型，挂在 `OnPostDraw` 路径，对齐物块网格。
**共享主题/图标：** `src/UI/Themes/`、`Icons.cs` / `UiIcons.cs`。
**测试：** `tests/Hakoniwa.Tests/`，与被测类型同名 `*Tests.cs`。
