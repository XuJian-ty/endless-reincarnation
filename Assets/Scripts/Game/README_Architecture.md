# 架构、设计模式与技术栈说明

## 一、整体架构：分层 + 数据驱动

项目采用 **分层架构**，各层单向依赖，避免循环引用和「到处乱调」：

```
┌─────────────────────────────────────────────────────────┐
│  UI 层 (Game.UI)                                        │  ← 面板、按钮、HUD，只调流程层/领域层接口
├─────────────────────────────────────────────────────────┤
│  表现层 (Game.Presentation)                              │  ← 角色控制、怪物表现、特效，响应输入与事件
├─────────────────────────────────────────────────────────┤
│  流程层 (Game.GameFlow)                                  │  ← 状态机、关卡导演、存档编排、Boss 进度
├─────────────────────────────────────────────────────────┤
│  领域层 (Game.Domain)                                    │  ← 纯 C# 模型：Stats、Player、背包、技能树、战斗公式
├─────────────────────────────────────────────────────────┤
│  数据层 (Game.Data)                                      │  ← ScriptableObject 配置、可序列化 DTO（如 WeaponInstance）
├─────────────────────────────────────────────────────────┤
│  基础设施层 (ProjectBase / Game.Core)                     │  ← 单例、事件、对象池、资源/场景加载、UI 管理、Mono 桥接
└─────────────────────────────────────────────────────────┘
```

- **依赖方向**：上层只依赖下层，不反向依赖。例如 UI 调 GameFlow/PlayerModel，不会直接调 ResMgr 内部。
- **数据驱动**：数值、表（武器、怪物、关卡、Buff、技能）用 **ScriptableObject** 配置，逻辑用 C#；改数值尽量不动代码。
- **运行时与配置分离**：SO 只做「定义」；运行时数量、随机结果、存档里的状态放在 **普通类 / 可序列化 DTO** 里（如 WeaponInstance、SaveData）。

这样做的原因：Unity 项目容易变成「脚本互相挂引用、改一处牵全身」；分层 + 数据驱动能控制复杂度，方便你一个人长期迭代和排查问题。

---

## 二、已用 / 将用的设计模式

| 模式 | 在哪里用 | 作用 |
|------|----------|------|
| **单例 (Singleton)** | BaseManager\<T\>、SingletonAutoMono\<T\>、EventCenter、PoolMgr、ResMgr、ScenesMgr、UIManager、MusicMgr | 全局只存在一份的管理类，到处都能访问，避免重复 new 或找引用。 |
| **对象池 (Object Pool)** | PoolMgr | 子弹、怪物、掉落物、特效等频繁创建/销毁的对象复用，减轻 GC 和实例化开销。 |
| **观察者 / 事件总线 (Observer / Event Bus)** | EventCenter | 解耦「谁触发了什么事」和「谁要响应」：怪物死亡、Boss 击杀、升级、UI 刷新等用事件通信，不直接互相引用。 |
| **状态机 (State Machine)** | GameStateMachine（将做）、PlayerController 状态（闲置/移动/攻击/闪避/受击） | 游戏大状态（主菜单/关卡中/暂停）和角色小状态有明确切换条件，避免一堆 bool 和 if 混在一起。 |
| **策略 / 数据驱动 (Strategy via SO)** | WeaponSO、EnemyStatsSO、LevelConfigSO、BuffSO、SkillSO | 同一种「行为类型」用不同配置（SO）区分，逻辑写一份，数值和表现由 SO 决定。 |
| **模板方法 (Template Method)** | BasePanel（Awake 里 FindChildrenControl，子类重写 OnClick/ShowMe/HideMe） | UI 面板共用「查找子控件、注册按钮」的流程，子类只填具体逻辑。 |
| **组合优于继承 (Composition)** | PlayerModel = Stats + ExpModel + Inventory + Equipment + BuffContainer | 玩家能力由多个组件组合而成，而不是一个巨大的 Player 继承树；方便单测和替换（如换一套背包实现）。 |
| **DTO / 纯数据结构 (Data Transfer Object)** | SaveData、RunData、WeaponInstance、LevelSnapshot | 只负责「长什么样」的数据结构，用于存档、网络、层与层之间传参；不写业务逻辑。 |
| **服务定位 (Service Locator)** | 通过 BaseManager.GetInstance() 取各种 Manager | 需要某能力时向「全局管理器」取，而不是层层传引用；代价是隐藏了依赖，适合 Unity 这种场景化项目。 |

**不会刻意用的**：  
- **完整 MVC/MVVM**：UI 会直接调 PlayerModel 或 GameFlow，不做严格的 ViewModel 绑定（减少前期复杂度）。  
- **ECS / DOTS**：当前体量用传统「MonoBehaviour + 管理器」足够，后续若性能瓶颈再考虑局部 ECS。  
- **依赖注入框架**：暂不引入 Zenject/VContainer，用单例 + 事件 + 显式传参即可。

---

## 三、技术栈清单

### 3.1 引擎与运行时

| 技术 | 用途 |
|------|------|
| **Unity** | 引擎、场景、预制体、物理、动画、渲染。 |
| **C#** | 全部游戏逻辑与工具脚本；目标 .NET Standard 2.1 / Unity 2021+ 兼容。 |
| **URP** | 渲染管线（你项目已用）。 |
| **新输入系统 (Input System)** | PlayerInput、InputAction、ActionMap；按键重绑定、Gameplay/UI 切换。 |

### 3.2 数据与配置

| 技术 | 用途 |
|------|------|
| **ScriptableObject** | 武器、怪物、关卡、Buff、技能、掉落表、等级成长等「配置表」；只读定义，不存运行时可变状态。 |
| **JSON** | 存档持久化（6 槽）；可用 Unity 自带 JsonUtility 或 Newtonsoft.Json，SaveData 用可序列化 DTO。 |
| **可序列化 DTO** | SaveData、RunData、WeaponInstance、LevelSnapshot 等；用于 JSON 读写和层间传递。 |

### 3.3 基础设施（你已有 + 可能补充）

| 模块 | 实现方式 |
|------|----------|
| 单例 | BaseManager\<T\>（纯 C#）、SingletonAutoMono\<T\>（挂场景常驻物体）。 |
| 事件 | EventCenter，string key + UnityAction / UnityAction\<T\>。 |
| 对象池 | PoolMgr，按 prefab 名 Get/Push，缺则 ResMgr 异步加载。 |
| 资源加载 | ResMgr，Resources 同步/异步；后续可换成 Addressables。 |
| 场景 | ScenesMgr，同步/异步 LoadScene，进度走事件。 |
| UI | UIManager 按名 ShowPanel/HidePanel，BasePanel 自动查找子控件并注册点击。 |
| 非 Mono 用协程/Update | MonoMgr 持有 MonoController，对外暴露 AddUpdateListener、StartCoroutine。 |

### 3.4 游戏逻辑分层（目录与职责）

| 层 | 目录/命名空间 | 职责 |
|----|----------------|------|
| 领域层 | Game/Domain | Stats、StatModifier、CombatCalculator、ExpModel、PlayerModel、Inventory、Equipment、BuffContainer、SkillTree 等纯逻辑与数据模型。 |
| 数据层 | Game/Data | WeaponSO、WeaponInstance、LevelGrowthSO、EnemyStatsSO、LevelConfigSO、BuffSO、SkillSO 等 SO 与可序列化结构。 |
| 流程层 | Game/GameFlow | GameStateMachine、LevelDirector、BossProgressService、CheckpointService、SaveSystem。 |
| 表现层 | Game.Presentation | PlayerController、EnemyController、BossController、攻击/技能表现、特效驱动。 |
| UI 层 | Game.UI | 主菜单、HUD、背包、技能树、商店、Buff 选择、战斗回忆等面板逻辑。 |
| 输入 | Game.Inputs | 新输入系统封装、按键重绑定存储。 |
| 存档 | Game.Saving | SaveData 结构、JSON 序列化、版本兼容。 |

### 3.5 设计原则（写代码时会遵守的）

- **单一职责**：一个类只做一类事（如 SaveSystem 只负责读写，不负责「什么时候该存」）。  
- **依赖方向**：上层依赖下层；领域层不依赖 Unity 引擎 API（除少量如 Mathf）。  
- **用事件解耦**：跨层、跨系统用 EventCenter 发事件，而不是 A 直接持 B 引用。  
- **SO 只读**：运行时可变状态不进 SO，只在普通类或 DTO 里。  
- **先接口/数据后实现**：先定 SaveData 长什么样、PlayerModel 暴露什么接口，再写具体实现和 UI 绑定。

---

## 四、小结

- **架构**：分层（UI → 表现 → 流程 → 领域 → 数据 → 基础设施）+ 数据驱动（SO 配置 + JSON 存档）。  
- **设计模式**：单例、对象池、观察者/事件、状态机、策略（SO）、模板方法（BasePanel）、组合、DTO、服务定位。  
- **技术栈**：Unity + C# + URP + 新输入系统 + ScriptableObject + JSON + 你现有的 ProjectBase；后续按需加 Addressables、更严格的 State 封装等。

这样你心里有一张「完整技术栈」的图，看代码或自己加功能时，能对得上「这一块属于哪一层、用的是哪种模式」。
