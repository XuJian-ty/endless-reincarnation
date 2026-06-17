# UI 面板优化建议（架构 / 设计模式 / 数据驱动）

基于当前所有 Panel 与 BasePanel、UIManager 的阅读，从**架构**、**设计模式**、**数据驱动**三方面给出结论与可落地建议。按优先级分为：高（建议尽快做）、中（结构清晰后再做）、低（可选）。

---

## 一、架构层面

### 1.1 高：减少对单例的直接依赖，引入“数据源/上下文”注入

**现状**：各面板在 Awake/OnEnable/ShowMe/Update 里直接调用：
- `UIManager.GetInstance()`
- `GameStateMachine.GetInstance()`（取 Player、CurrentRun、CurrentSaveId 等）
- `ConfigManager.GetInstance()`
- `EventCenter.GetInstance()`
- `SaveSystem.GetInstance()`

导致：面板与全局单例强耦合，单元测试难、替换实现难、流程变更时改动面大。

**建议**：
- 为**关卡内 UI** 定义一个轻量 `ILevelUIModel`（或沿用/扩展 IPlayerContext），由 LevelBootstrapper/GameEntry 在进入关卡时构造并注入到需要数据的面板（如通过 `ShowPanel<T>(..., model => panel.Bind(model))` 或面板从 UIManager 取一次“当前关卡 UI 模型”）。
- 模型接口只暴露“只读数据 + 事件”，例如：`PlayerModel Player { get; }`、`RunData CurrentRun { get; }`、`event Action RunChanged`、`event Action InventoryChanged`。面板只依赖接口，不直接拿 GameStateMachine。
- 主菜单/加载流程可保留对 GameStateMachine、SaveSystem 的少量入口调用，或同样收敛到一个 `IMainMenuUIModel`。

这样“谁提供数据”集中在少数地方，面板变为“视图 + 绑定逻辑”，便于后续做数据驱动和测试。

### 1.2 高：PlayerInfoHUDPanel 不要每帧全量刷新

**现状**：`Update()` 里每帧调用 `RefreshAll()`，用 `GetControl<Text>/GetControl<Slider>` 拉取玩家/关卡数据并写 UI。

问题：每帧大量 GetControl 与字符串查找、重复赋值，浪费 CPU；且与“数据驱动”相悖（应由数据变化驱动刷新）。

**建议**：
- **事件驱动 + 脏标记**：
  - 在 PlayerModel / GameStateMachine 或上面说的 ILevelUIModel 上暴露事件（如 `StatsChanged`、`ExpChanged`、`RunChanged`），或继续使用 `GameEvents.InventoryChanged` 等。
  - HUD 在 OnEnable 时订阅这些事件，在回调里只刷新受影响的部分（例如只更新血条、只更新经验条），或设置 `_dirty = true`，在下一帧或 LateUpdate 里做一次 `RefreshAll()` 再清空脏标记。
- **缓存控件引用**：在 Awake 或首次 ShowMe 时用 GetControl 把 Text/Slider 引用缓存到字段，避免每帧按名字查找。
- 若暂时不改事件系统，至少做：**缓存控件引用 + 脏标记**：仅当 `_player`、`_run` 或关键数值变化时再调用 RefreshAll，避免无变化时每帧刷新。

### 1.3 中：BasePanel 控件查找的健壮性与成本

**现状**：
- `FindChildrenControl<T>` 在 Awake 时对 Button、Image、Text、Toggle、Slider 等做 `GetComponentsInChildren<T>`，按 **gameObject.name** 塞进 `controlDic`；重名会进入同一 List，`GetControl<T>` 只取列表中第一个匹配类型。
- 控件完全依赖“命名约定”（如 Btn_Close、Text_PlayerName），易拼写错误、重构时易漏。

**建议**：
- 保留现有机制以兼容现有面板，但**新面板或大改的面板**优先使用 **SerializeField 显式拖拽** 关键控件（如 BackpackPanel 已部分采用），减少对名字的依赖。
- 在 BasePanel 或文档中约定：同名同类型只保证返回“第一个”，避免同一名字下挂多个同类型控件造成歧义。
- 若后续引入 ViewModel/数据绑定层，可考虑“按数据项生成 UI”的列表（如 SaveListPanel 的 row prefab）而不是大量按名字 GetControl。

### 1.4 中：UIManager 与面板生命周期的清晰度

**现状**：ShowPanel 异步加载时先 `_panelDic[panelName] = panel` 再执行 callback 和 ShowMe，逻辑正确；HidePanel 会 Destroy 面板物体。部分面板在 HideMe 里只 SetActive(false)，由 UIManager.HidePanel 统一 Destroy，行为一致。

**建议**：
- 在 UIManager 或 BasePanel 上简短注释：**哪些面板是“常驻缓存只 SetActive”**（如关卡内 C/V/X/Esc 打开的四个）、**哪些是“每次关闭即 Destroy”**（如 HidePanel 当前行为），避免后续加面板时混用两种预期。
- 若将来有“预加载但不显示”的需求，再考虑在 UIManager 增加 PreloadPanel/UnloadPanel，当前可维持现状。

---

## 二、设计模式层面

### 2.1 高：BackpackPanel 职责过重，建议拆分为“视图 + 逻辑”

**现状**：BackpackPanel 约 470+ 行，同时负责：
- 布局解析（ResolveReferences、Slot0..Slot49、StatsRoot）
- 排序逻辑（RecomputeDisplayOrder、SortMode）
- 槽位刷新、左侧属性与装备图标刷新
- 悬停 Tooltip、右键 ContextMenu、装备/出售
- 与 PlayerModel、Config、EventCenter 的交互

**建议**：
- **Presenter / 用例类**：抽出一个 `BackpackPresenter`（或 `BackpackUseCases`），负责：从 GameStateMachine/Player 取数据、排序、装备/出售/排序变更等“业务逻辑”，并暴露：当前显示顺序、当前槽位数据列表、选中槽位操作等。BackpackPanel 只负责：接收 Presenter 的数据、绑定到 Slot 与左侧 UI、把用户操作（点击、右键、排序下拉）转交给 Presenter。
- **列表与槽位**：可进一步把“单格槽位”做成 `InventorySlotView` 组件，接收一个 DTO（图标、数量、品质背景等），只负责显示和点击/悬停事件上报，BackpackPanel 只做列表级的绑定和事件转发。
- 这样 BackpackPanel 瘦身为“视图层”，逻辑与数据集中在 Presenter，便于单测和后续做数据驱动（例如从配置驱动排序模式、槽位数量等）。

### 2.2 中：LevelSoundPanel 与 MainMenuSoundPanel 的重复结构

**现状**：两者都是“BGM 开关 + 音量滑块 + 曲目下拉 + 关闭”，Level 多“音效开关 + 音效音量”，数据源不同（CurrentRun vs PlayerPrefs），但 UI 结构和监听逻辑高度相似。

**建议**：
- 抽一个 **BgmBlockView**（或同名 Component）：只负责 Toggle_Bgm、Slider_BgMusic、Dropdown_BgmTrack 的显示与变更回调（如 `Action<bool> onBgmToggle`、`Action<float> onVolume`、`Action<int> onTrackChanged`）。LevelSoundPanel 与 MainMenuSoundPanel 各持有一个 BgmBlockView，在 ShowMe/Awake 里注入当前数据并订阅回调，在回调里写存档/PlayerPrefs 与 MusicMgr 的交互。这样“BGM 一块 UI”的复用和测试都更简单。
- 音效块若将来在其它面板复用，也可用同样方式抽出 SfxBlockView。

### 2.3 中：面板导航与“返回栈”的显式化

**现状**：A 面板里 RegisterClick 打开 B，B 里关闭自己再 ShowPanel(A)，或通过 Init(callback) 把“返回后要显示的面板”传进去（如 SaveListPanel 关闭后 ShowPanel(MainMenuPanel, panel => panel.Init(...))）。逻辑分散在各面板内部，没有统一的“返回栈”或“流程”。

**建议**：
- 若后续主菜单/关卡内子面板增多，可引入一个轻量 **UINavigation**（或挂在 UIManager 上）：PushPanel(name)、PopPanel()、PopToRoot()，由它负责 HidePanel 当前、ShowPanel 上一个或根面板。当前面板只调用 Navigation.Push/Pop，不直接知道“返回谁”。
- 现阶段若面板数量有限，可以保持现状，仅在文档中画一张“主菜单 → 起名/存档列表/设置”和“关卡内菜单 → 设置/战斗回忆”的导航图，避免新人改错返回目标。

### 2.4 低：BasePanel 的 ShowMe/HideMe 与“进入/退出”生命周期

**现状**：ShowMe/HideMe 多为 `gameObject.SetActive(true/false)`，部分面板在 ShowMe 里做 Refresh（如 MenuPanel.RefreshBattleMemoryButton、LevelSoundPanel 从 Run 同步到控件）。没有统一的 OnShow/OnHide 或“可见性变化”回调。

**建议**：在 BasePanel 中增加 `protected virtual void OnShow()` / `protected virtual void OnHide()`，在 ShowMe/HideMe 里在 SetActive 之后调用（或仅在 SetActive 为 true/false 时各调用一次）。需要“每次打开时刷新”的面板重写 OnShow，需要“关闭时释放/清理”的重写 OnHide，便于统一约定和后续扩展（如埋点、音效）。

---

## 三、数据驱动层面

### 3.1 高：KeyConfigPanel 的 16 行配置改为 ScriptableObject 或配置资源

**现状**：`RebindEntries` 为静态数组，写死在代码里（Action 名、复合绑定子名、显示名），共 16 条；行数与顺序强依赖预制体 KeyList 下子物体数量与顺序。

**建议**：
- 定义 **KeyRebindEntrySO** 或 **KeyRebindConfigSO**（ScriptableObject 列表）：每条包含 actionName、bindingPart（可选）、displayName。KeyConfigPanel 在 Awake 或 Init 时从 ConfigManager 或 Resources 读取该 SO，按列表长度和顺序生成/绑定 16 行。这样增删改按键项只需改配置，不必改代码。
- 若仍希望部分按键“仅在某些平台显示”，可在 SO 里加 optional 字段（如 platformMask），BuildRows 时过滤。

### 3.2 高：BackpackPanel 的 StatNames、排序模式文案与选项

**现状**：`StatNames` 为私有静态数组；排序下拉的选项列表在 OnEnable 里用 `AddOptions(new List<string>{ ... })` 写死。

**建议**：
- **属性名与顺序**：若 Stats 的结构稳定，可把“显示名”放到一个 **StatsDisplayConfigSO**（或现有 Config 中的一张表），按顺序列出 12 项的显示名与可选格式（如“生命”“法力”用 F0，暴击率用 F1），BackpackPanel 从配置读取并用于左侧 12 个 Text。这样策划改文案或顺序只需改配置。
- **排序模式**：将 SortMode 枚举与下拉选项的对应关系放到一个小 SO 或 Config 列表（例如 `List<(SortMode mode, string label)>`），BackpackPanel 用该列表填充 Dropdown 并绑定 value 到 SortMode，避免在 C# 里写死“品质低→高”等字符串。

### 3.3 中：Buff 显示名称与描述

**现状**：BuffCardUI 内用两个 Dictionary 写死 buffId → 显示名、buffId → 描述，注释为“TODO: 从配置读取”。

**建议**：与需求说明书中的配置体系对齐，用 **BuffConfigSO** 或现有 Buff/被动配置表，每条包含 buffId、displayName、description。BuffCardUI 或 BuffSelectPanel 通过 ConfigManager 取配置并显示，新增 Buff 只改配置不改代码。

### 3.4 中：LevelSoundPanel / MainMenuSoundPanel 的曲目列表

**现状**：已使用 LevelBgmTrackListSO / MainMenuBgmTrackListSO 提供曲目列表与 Clip，属于数据驱动，良好。

**建议**：保持现状；若将来有“按场景或模式显示不同曲目子集”的需求，可在 SO 中增加分组或标签，由面板按当前场景过滤选项。

### 3.5 低：SaveListPanel 的列表项与数据源

**现状**：用 rowPrefab 动态生成行，每行用 Button + Text 显示玩家名和关卡；数据来自 `SaveSystem.GetInstance().GetAllSaveEntries()`。

**建议**：若后续有“存档列表需要更多字段或样式差异”（如显示难度、时间），可定义 **SaveEntryViewModel** 或 DTO，由 SaveListPanel 或一个小的 SaveListPresenter 将 SaveEntry 转为 ViewModel 列表，行预制体绑定 ViewModel 的若干字段。这样“行显示什么”由数据驱动，而不是在 Panel 里写死 `text.text = $"{entry.playerName}\n关卡 {entry.levelIndex}"`。当前规模下可维持现状，作为后续扩展方向。

---

## 四、按面板的简要清单

| 面板 | 架构 | 设计模式 | 数据驱动 | 优先建议 |
|------|------|----------|----------|----------|
| PlayerInfoHUDPanel | 每帧 RefreshAll + 单例 | 无 | 无 | 事件/脏标记刷新 + 缓存控件引用 |
| BackpackPanel | 单例 + EventCenter | 单类职责过重 | StatNames/排序写死 | 拆 Presenter + 槽位视图；配置化 StatNames/排序 |
| KeyConfigPanel | 单例 | 行构建与逻辑混合 | RebindEntries 写死 | RebindEntries → SO/配置 |
| LevelSoundPanel | 直接读 CurrentRun | 与 MainMenuSound 重复 | 曲目已 SO | 抽 BgmBlockView；保持 Run 数据源 |
| MainMenuSoundPanel | PlayerPrefs | 同上 | 曲目已 SO | 抽 BgmBlockView |
| MenuPanel | GameStateMachine 单例 | 简单 | 无 | 可保持；若有更多子面板再考虑 Navigation |
| BuffSelectPanel | 回调 + FindObject | 清晰 | Buff 名/描述在代码 | Buff 名/描述 → 配置 |
| BuffCardUI | - | 清晰 | 同上 | 同上 |
| SaveListPanel | SaveSystem + UIManager | 清晰 | 行内容写死 | 低优先级：ViewModel 驱动行内容 |
| NamePanel | 多单例 + Init 传参 | 清晰 | 无 | 可保持 |
| MainMenuPanel | UIManager 导航 | 清晰 | 无 | 可保持 |
| SkillTreePanel / BattleMemoryPanel | 占位 | 占位 | - | 实现时直接采用 Presenter + 数据驱动 |
| LoadingPanel | EventCenter 进度 | 清晰 | 无 | 可保持 |
| BasePanel | - | 名字驱动控件 | - | 新面板多用 SerializeField；可选 OnShow/OnHide |

---

## 五、建议实施顺序（不强制）

1. **短期**：PlayerInfoHUDPanel 改为事件/脏标记 + 缓存控件；KeyConfigPanel 的 RebindEntries 改为 SO。
2. **中期**：BackpackPanel 拆 Presenter + 槽位视图；StatNames 与排序选项配置化；LevelSoundPanel / MainMenuSoundPanel 抽 BgmBlockView。
3. **长期**：关卡内 UI 统一走 ILevelUIModel（或等价）注入，减少对 GameStateMachine 的直接依赖；Buff 名/描述与 KeyRebind 等全面配置化；如需再引入 UINavigation 与 OnShow/OnHide 规范。

以上建议在不大改现有玩法的前提下，优先提升可维护性、可测试性和数据驱动程度，便于后续加新面板和改需求。
