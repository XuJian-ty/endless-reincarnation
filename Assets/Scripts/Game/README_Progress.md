# 项目进度与后续计划

## 一、目前已完成的工作

### 1. 基础设施层（你之前写的 ProjectBase）

| 模块 | 作用 |
|------|------|
| **BaseManager / SingletonAutoMono** | 单例基类，全局只存在一份的管理类用这个 |
| **MonoMgr + MonoController** | 让不继承 MonoBehaviour 的类也能用 Update、协程 |
| **EventCenter** | 事件中心，用字符串发/监听事件（如怪物死亡、Boss 击杀） |
| **PoolMgr** | 对象池，重复生成/回收子弹、怪物、掉落物等 |
| **ResMgr** | 同步/异步加载 Resources 下的资源 |
| **ScenesMgr** | 同步/异步切换场景，带进度回调 |
| **UIManager + BasePanel** | 动态加载 UI 预制、分层（Bot/Mid/Top/System）、自动找子控件 |
| **MusicMgr** | 背景音乐、音效、音量调节 |
| **AStarMgr** | A* 寻路（后续怪物 AI 可用） |

这些是「地基」：后面所有系统都会依赖它们，不再重复造轮子。

---

### 2. 领域层：属性与战斗公式（Game/Domain）

| 文件 | 内容 |
|------|------|
| **Stats.cs** | **StatModifier**：加成结构（生命/法力/攻击/防御/暴击/攻速/移速/回复/吸血/增伤等）。**Stats**：11 项属性，支持多个 Modifier 叠加；攻速/移速按**比例**计算（0.2 = +20%）。**CombatCalculator**：伤害公式 `(攻击×技能倍率)×(0.1+270/(防御+300))×增伤×暴击`，吸血按最终伤害。 |

原因：战斗和成长都围绕「属性」转，先定好公式和叠加方式，后面装备、Buff、技能都往这里挂。

---

### 3. 数据层：配置与实例（Game/Data）

| 文件 | 内容 |
|------|------|
| **LevelGrowthSO** | 玩家 1～10 级成长配置：HP/MP/ATK/DEF/HpRegen/MpRegen 每级 +10%，可在编辑器里调曲线。 |
| **WeaponSO** | 武器配置：剑/枪，四种品质各一套**独立**的 [min,max] 范围；**Roll(品质, Random)** 在范围内随机生成一条实例。 |
| **WeaponInstance** | **FloatRange**（min/max + RandomValue）；**WeaponInstance**：获得武器时随机后的数据，可序列化进存档，**ToModifier()** 给玩家加属性。 |

原因：用 ScriptableObject 做「配置」，用普通类做「运行时实例」；装备属性有范围、获得时随机，符合你要的刷装体验。

---

### 4. 编辑器小工具（Game/Editor）

| 功能 | 作用 |
|------|------|
| **WeaponSOEditor** | 按品质折叠显示，只显示当前品质的 4/6/8/10 项范围。 |
| **Game → 从当前剑配置创建枪配置** | 选中剑 SO 后一键复制出枪 SO（数值一致，类型改为枪）。 |
| **Game → 修正武器暴击/暴击伤害为小数** | 把 40～60、80～120 这类百分比配置改成 0.4～0.6、0.8～1.2。 |

原因：减少重复配置和填错，提高迭代速度。

---

## 二、已完成（本轮实现）

- **存档系统**：SaveData / RunData / PlayerSaveData / InventorySaveData / LevelSnapshot 等 DTO；SaveSystem 6 槽、Newtonsoft.Json、版本号；已加 Newtonsoft 包依赖。
- **玩家模型**：ExpModel（1～10 级、经验溢出）；InventoryModel（20 格、堆叠 999、武器不堆叠）；EquipmentModel（装备武器并挂/摘 Stats Modifier）；PlayerModel（Stats + Exp + Inventory + Equipment + 血蓝金币天赋 Buff/技能占位，LoadFrom/SaveTo RunData）。
- **游戏流程**：GameStateMachine（StartNewGame、LoadGame、SaveCurrent、Pause、Resume、SaveAndQuit），与 ScenesMgr 联动进关卡场景。

## 三、还没做的（按设计文档）

- 关卡与刷怪（关卡配置、守卫者、Boss 降临、难度放大）
- 掉落与背包（拾取、堆叠、满包不拾取）
- Buff 选择（进关卡 3 选 1、叠加）
- 技能树与技能配置
- 各类 UI 面板（主菜单、HUD、背包、商店、技能树、战斗回忆等）
- 角色控制与输入（新输入系统、状态机、闪避/受击优先级）

---

## 三、接下来建议的推进顺序与原因

整体思路：**先打通「能存能读、能进关卡、有一个会动会成长的玩家」的最小闭环**，再往上加怪、加 UI、加 Buff/技能。这样你随时能跑起来玩一下，不会做了很久还看不到结果。

### 阶段 A：存档与数据结构（优先做）

**要做：**  
- 定义 **SaveData / RunData**（当前槽位、关卡号、难度、玩家等级/经验/金币、背包里的物品、装备中的武器、已选 Buff、天赋点、技能树解锁状态、Checkpoint、LevelSnapshot 等）。  
- 实现 **SaveSystem**：按槽位（1～6）读写 JSON，带版本号，方便以后兼容旧存档。

**原因：**  
- 存档结构一旦定好，后面「玩家模型、背包、关卡进度」都会按这个结构来写，避免做到一半大改。  
- 先做存档，再做「新游戏 / 读档」，逻辑清晰；而且你之前要求「退出时保存、读档继续」，没有 SaveSystem 后面没法接。

---

### 阶段 B：玩家模型（等级、经验、背包、装备）

**要做：**  
- **ExpModel**：等级 1～10、当前经验、升级所需经验、**AddExp(数值)**（溢出保留到下一级）。  
- **PlayerModel**：持有 **Stats**、**ExpModel**、**LevelGrowthSO**；初始化时用 LevelGrowthSO 填 1 级属性，升级时用成长表重算并加 Modifier。  
- **Inventory**：20 格（4×5）、堆叠 999、武器不堆叠；添加/移除/查找；满包不拾取。  
- **Equipment**：当前装备的 **WeaponInstance**；装备/卸下时从 Stats 上挂/摘该武器的 **ToModifier()**。  
- PlayerModel 再持有 Inventory、Equipment、天赋点数（简单 int）、Buff 列表（先占位）、技能树解锁状态（先占位）。

**原因：**  
- 战斗、掉落、UI 都要读「当前血量、等级、背包、装备」，先把这一块聚在一个 PlayerModel 里，后面改数值和存档都只动这一处。  
- 经验溢出、升级加属性、装备切武器，都是你需求里明确写的，先实现再接关卡和战斗。

---

### 阶段 C：游戏流程与主菜单

**要做：**  
- **GameStateMachine**（或类似命名）：状态有「主菜单 / 关卡中 / 暂停」等；提供 **StartNewGame(slot)、LoadGame(slot)、EnterLevel()、Pause()、Resume()、SaveAndQuit()**。  
- 主菜单场景：按钮「新的游戏、加载存档、声音设置、退出」；新的游戏 = 选一个空槽或覆盖槽，构造全新 RunData 并 Save，再 EnterLevel(1)。加载存档 = 选槽，Load，再 EnterLevel(当前关卡)。  
- 进入关卡时：ScenesMgr 异步加载关卡场景；场景加载完后根据 RunData 决定是「新关卡」还是「读档继续」（是否有 LevelSnapshot）。

**原因：**  
- 没有流程，就没办法从菜单「进游戏」或「读档」，前面的 Save 和 PlayerModel 也用不起来。  
- 先做「新游戏 → 进第 1 关」和「读档 → 进上次关卡」两条线，再慢慢加 Buff 选择、Boss 结算等分支。

---

### 阶段 D：关卡内最小可玩（角色移动、简单怪、伤害、掉落）

**要做：**  
- 一个简单的 **PlayerController**（或用新输入系统）：移动、朝向；从 PlayerModel.Stats 读 MoveSpeed。  
- 简单的 **Enemy**：血量、防御、死亡时发事件；受击时用 **CombatCalculator** 算伤害并扣血。  
- 玩家攻击（近战/远程先做一个）：触发伤害计算、吸血回血、经验增加（调用 ExpModel.AddExp）、小怪掉金币、精英按表 Roll 武器实例进背包。  
- 关卡内「当前关卡难度」从 RunData 读，怪物属性按难度放大（你之前定的系数）。

**原因：**  
- 先能「动起来、打怪、掉东西、升级」，再扩展技能、Buff、Boss、守卫者。  
- 这样你可以很快玩到「选新游戏 → 进图 → 打怪 → 升级掉装」，再迭代手感与数值。

---

### 阶段 E：再往后（按需排期）

- Buff 选择（进关卡 3 选 1、叠加、死亡复活重选）。  
- 技能树与技能配置、主动技能蓝耗。  
- 守卫者数量与位置、Boss 降临与通关判定、战斗回忆。  
- 宝箱、商店、Checkpoint 与死亡回滚。  
- 完整 UI（HUD、背包、技能树、设置、主菜单 6 槽等）。  

---

## 四、小结

- **已完成**：基础设施 + 属性/战斗公式 + 武器配置与实例（含范围随机）+ 等级成长配置 + 编辑器小工具。  
- **建议顺序**：**A 存档 → B 玩家模型 → C 流程与主菜单 → D 关卡内最小可玩**，再按需求做 E。  
- **原因**：先定「数据怎么存、人怎么成长、怎么进关卡」，再做「在关卡里能动、能打、能掉装」，这样每一步都有可运行的结果，你也容易从流程里学「先搭骨架再填肉」的做法。

如果你愿意，下一步我就从 **阶段 A（SaveData 结构 + SaveSystem）** 开始写具体类和接口，你跟着看或自己试着接 B 的 PlayerModel 也可以。
