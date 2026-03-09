# 敌人 AI 设计说明与实现计划

**文档目的**：记录敌人 AI 的架构思路、设计模式、数据驱动与事件驱动约定、实现计划，以及「受伤/死亡」是否纳入行为树的结论，便于后续实现与维护时回顾。

**版本**：1.3  
**更新日期**：2025-03-06（改用自研行为树 + 数据资产，移除 Behavior Designer 依赖）  

**相关文档**：取消普攻、统一为技能、多种精英/守卫者、技能效果接口与组合的详细设计见 **《敌人与技能系统设计方案-统一技能与效果组合》**（`Docs/敌人与技能系统设计方案-统一技能与效果组合.md`）。该方案为后续迭代目标，当前实现仍为「近战/远程 + 技能槽」并存，待按方案迁移后以技能槽与效果组合为准。

---

## 一、设计目标与约束

- **商业级**：架构清晰、设计模式明确、便于长期维护与扩展。
- **可维护性**：调行为改配置/改树资产，少改 C#；动画过渡在 Animator 中调整。
- **可扩展性**：新敌人类型、新 Boss 变体、新行为（如召唤）主要通过数据与树结构完成。
- **解耦**：决策层（行为树）不直接操作 NavMesh/Animator；执行层不关心决策从何而来；对外通过事件与关卡交互。
- **数据驱动**：敌人「能做什么、参数是什么」由 SO 与行为树资产决定；树结构在 `BehaviorTreeAsset` 中编辑，运行时由代码从资产构建行为树。
- **与现有项目一致**：命名、分层与玩家侧（Controller + StateMachine + Mover/Combat）对称，沿用 ConfigManager、EventCenter、伤害与动画管线。

---

## 二、行为树与引擎工具选择

| 选择 | 说明 |
|------|------|
| **自研行为树（Game.AI.BehaviorTree）** | 轻量级 Selector/Sequence + 条件/动作节点，由 C# 实现；树结构与参数放在 `BehaviorTreeAsset`（ScriptableObject）中，Inspector 可视化编辑，运行时从资产构建树。 |
| **Unity Animator** | 敌人动画状态、过渡、混合、参数条件均在 Animator Controller 中配置；代码只设参数（SetTrigger/SetFloat）。 |
| **NavMesh + NavMeshAgent** | 敌人移动与寻路由 Unity 自带方案承担，执行层只设置目标与速度。 |

不再使用第三方行为树插件（如 Behavior Designer），以减少依赖、便于在仓库内维护全部行为逻辑与数据格式。

---

## 三、架构分层（与玩家侧对称）

```
EnemyController（门面）
├── 持有：EnemyRuntimeStats、EnemyAI、EnemyMover、EnemyCombat
├── 职责：受击/死亡/事件、当前意图(CurrentIntent)的读写、对外唯一入口
├── 不包含：寻路逻辑、攻击动画逻辑、树结构

EnemyAI（决策）
├── 持有：自研行为树根节点（IBehaviorNode），可选 BehaviorTreeAsset（数据驱动）
├── 职责：每帧先执行感知再 Tick 行为树；把玩家/自身/Archetype 注入上下文；树内动作节点写意图到 Controller
├── 不直接：调 NavMesh、调 Animator、发游戏事件

EnemyMover（执行）
├── 职责：读取 CurrentIntent；若为 MoveTo(target) 则设置 NavMeshAgent 目标与速度
├── 不关心：意图从哪来、树结构

EnemyCombat / EnemyAbilityRunner（执行）
├── 职责：读取 CurrentIntent；若为 MeleeAttack/RangedAttack/CastSkill 则设 Animator 参数、播动画；伤害由动画事件 + 现有伤害配置/公式 产生
├── 不关心：为何要攻击、树结构
```

**意图 (Intent)**：建议用 struct/class，例如 `EnemyIntent { Type: Patrol | Chase | MeleeAttack | RangedAttack | CastSkill(slot); Target? }`，由 AI 写入、Mover/Combat 只读，实现决策与执行解耦。

---

## 四、设计模式

| 模式 | 用法 | 作用 |
|------|------|------|
| 门面 (Facade) | EnemyController 对外统一接口 | 外部只依赖 Controller，内部可替换实现。 |
| 黑板 (Blackboard) | BD Variables + Archetype 引用 | 树只读黑板与配置，不依赖具体 GameObject，易测易换数据源。 |
| 策略 (Strategy) | 不同敌人类型 = 不同 BT 资源 + 不同 ArchetypeSO | 行为差异由数据和树资产决定。 |
| 命令/意图 (Command) | CurrentIntent 由 AI 写入、Mover/Combat 消费 | 决策与执行分离，便于日志与扩展。 |
| 数据驱动 | 能力与参数在 SO 与 BT 资源中 | 加新敌人/新行为主要改配置和树。 |

---

## 五、数据驱动

- **EnemyArchetypeSO**（或按类型/变体一条条）：描述「能做什么」与参数——是否巡逻/追击、近战/远程的伤害名与冷却、技能槽数量及技能 ID、Boss 变体的武器与 4 技能 ID 等；**感知**（扇形角度、扇形距离、丢失目标距离）、**巡逻**（半径内随机）、**Boss 阶段**（50% 血线前后技能解锁）等也在此配置。一个敌人类型/变体 = 一条配置。
- **BehaviorTreeAsset（行为树资产）**：每种行为类型一个或多个 .asset（如 MeleeMinionTree、RangedMinionTree、EliteTree、GuardianTree、BossTree）；**Boss 5 个预制体对应 5 个关卡的不同 Boss**，每关使用对应预制体，可共用一个 Boss 行为树资产，差异在预制体与 ArchetypeSO（武器 + 4 技能 ID，阶段控制技能解锁）。
- **Animator Controller**：按敌人类型/变体设 .controller；状态与过渡、Blend、参数条件均在编辑器中；执行层只根据意图设参数。
- **不在代码里**：不写「根据类型硬编码拼树」，不写「动画状态与过渡」；行为通过 `BehaviorTreeAsset` + `EnemyArchetypeSO` + Animator 资源数据驱动。

---

## 六、事件驱动与意图传递

- **使用事件 (EventCenter) 的场景**：关卡/系统级，如 EnemyDied、GuardianDied、PlayerLevelUp 等；需要解耦的跨系统（UI、关卡逻辑）监听这些事件。
- **敌人内部不用事件总线**：AI 与执行层之间用 **CurrentIntent** 传递（AI 写，Mover/Combat 读），简单、无订阅泄漏、与命令模式一致。若需播放攻击音效等，可在执行层发 EventCenter 事件，而非在敌人内部再建一套事件系统。

---

## 七、动画与行为树的关系

- **行为树不关心动画**：只输出意图（Patrol、Chase、MeleeAttack、RangedAttack、CastSkill 等）。
- **过渡与混合**：全部在 **Unity Animator Controller** 中配置；代码只做「意图 → SetTrigger/SetFloat」。
- **伤害**：沿用现有动画事件 + 伤害配置 + CombatCalculator（敌人对玩家）。

---

## 八、受伤与死亡：是否纳入行为树

### 8.1 死亡：不纳入行为树

- **原因**：死亡是「HP ≤ 0」的结果，不是 AI 的决策。应由游戏逻辑在检测到致死伤害时直接处理。
- **做法**：
  - 现有逻辑保留：`EnemyController.ApplyDamage(amount)` 内若致死则调用 `Die()`（禁用/回收、发 EnemyDied/GuardianDied、给玩家经验/金币/掉落等）。
  - 死亡后不再 Tick 行为树：在 `EnemyController` 中，若已死亡或 `!gameObject.activeSelf` 则不调用 `EnemyAI.Tick()`，或 `EnemyAI` 在 Tick 前检查 `Controller.IsAlive`。
- **结论**：死亡**不**放在行为树里，由 Controller + 现有事件与奖励逻辑负责。

### 8.2 受伤（受击硬直）：建议纳入行为树，作为最高优先级分支

- **原因**：受击是「一段时间内无法执行其它行为」的状态，可以视为一种**最高优先级的行为**——先响应受伤，再做巡逻/追击/攻击。放在行为树里可以统一「所有行为」的优先级与打断关系，便于维护和扩展（例如以后加「霸体」则可在树里或条件中排除受伤分支）。
- **做法**：
  - 在黑板中维护「是否处于受击状态」与「受击剩余时间」（由 `EnemyController.ApplyDamage` 或受击入口在扣血后写入，或由专门组件在动画事件中更新）。
  - 行为树**根节点**使用 **Selector**：
    - **第一子节点**：条件 + 行为「若处于受击状态，则执行受击动画/等待，返回 Running 直到结束」；结束时清除受击状态。
    - **第二子节点**：正常 AI（巡逻、追击、攻击、技能等）。
  - 这样受击时树总是先选第一分支，自然打断其它行为；受击结束后再走正常逻辑。
- **替代方案**：受击也可以**不**进行为树，而在外部处理：受击时设 `isHurt` 并在一段时间内不 Tick 树，或清空 CurrentIntent 并只播受击动画；时间到再恢复 Tick。这样实现更简单，但「谁能打断谁」分散在代码与树两处。若希望所有行为优先级都在树中可见，推荐**纳入行为树**。
- **结论**：**受伤（受击）建议纳入行为树**，作为根 Selector 的第一个子分支，保证最高优先级且逻辑集中；若实现阶段希望极简，可先用「受击时暂停 Tick」的替代方案，后续再迁入树内。

---

## 九、实现计划（分阶段）

### 阶段 0：准备

- 确认现有 **EnemyController**、**EnemyRuntimeStats**、**EnemyStatsDatabaseSO**、伤害与事件流程保持不变；仅在此基础上增加 AI 与执行层。

### 阶段 1：基础框架与一种敌人

- 定义 **EnemyIntent** 结构体/类（Type + 可选 Target）。
- 在 **EnemyController** 上增加 **CurrentIntent** 的读写；增加对 **EnemyAI**、**EnemyMover**、**EnemyCombat** 的引用（或在同一 GameObject 上挂载并获取）。
- 实现 **EnemyAI**：持有自研行为树根节点；可选绑定 `BehaviorTreeAsset`，若无资产则使用代码默认树；每帧先执行感知，再 Tick 行为树，由树内动作节点设置 `Controller.CurrentIntent`。
- 实现 **EnemyMover**：每帧读取 CurrentIntent，若为 MoveTo/Patrol/Chase 且有目标，则设置 NavMeshAgent 的目标与速度（可从 Archetype 或 Stats 取移速）。
- 实现 **EnemyCombat**（或 EnemyAbilityRunner）：读取 CurrentIntent，若为 MeleeAttack/RangedAttack/CastSkill 则设置 Animator 参数（如 SetTrigger），伤害仍由动画事件 + 现有配置驱动。
- 新增 **EnemyArchetypeSO**：先支持一种类型（如近战小怪），配置能否巡逻/追击、近战伤害名等；行为树资产只做「受击 → 巡逻 → 追击 → 近战攻击」的简单树。
- 近战小怪预制体：挂 EnemyController、EnemyAI（可绑定 MeleeMinionTree）、EnemyMover、EnemyCombat，配置 NavMeshAgent、Animator、ArchetypeSO；验证巡逻、追击、拳击连击与伤害。

### 阶段 2：五种行为类型与九种敌人

- 扩展 **EnemyArchetypeSO**：支持远程小怪、精英、守卫者、Boss 所需字段；**感知**（sectorAngle、sectorRange、chaseBreakDistance）、**巡逻**（patrolRadius）、**Boss 阶段**（50% 血线、阶段 0 可用技能 [0,1]、阶段 1 可用 [0,1,2,3]）。
- 实现**扇形感知**：每帧 Tick 前判断玩家是否在敌人前方扇形内；追击中若玩家距离 > chaseBreakDistance 则清空目标回巡逻。
- 为每种类型创建 **BehaviorTreeAsset** 资源；**Boss 5 个预制体**对应 5 关，每关 Boss 使用对应预制体，可共用 BossTree 资产，技能可用性按阶段与 Archetype 配置。
- 实现远程普攻（法球/箭）、技能槽与 **Boss 阶段**（行为树或条件节点按当前血量阶段只允许 2 或 4 个技能）。
- 实现**战斗回忆**：独立场景，根据玩家选择生成对应 Boss 预制体。
- 敌人与玩家**物理碰撞**、NavMeshAgent 与碰撞层协调。
- 完成 4 种普通敌人 + 5 个 Boss 预制体与配置，验证与需求说明书「五（补充）」一致。

### 阶段 3：受伤与死亡策略落地

- **死亡**：保持现有 ApplyDamage/Die 逻辑；在 EnemyController 或 EnemyAI 中确保死亡后不再 Tick 树（或树内不访问已销毁对象）。
- **受伤**：在黑板中增加「受击状态/剩余时间」；BT 根为 Selector，第一子为「若受击则执行受击并等待」任务，第二子为原有正常 AI；受击状态由 ApplyDamage（非致死）或受击动画事件写入，由树内任务或计时清除。
- 可选：受击时清空或锁定 CurrentIntent，避免执行层继续执行上一帧的攻击/移动。

### 阶段 4：打磨与扩展

- 巡逻半径、扇形参数、chaseBreakDistance、攻击范围、技能冷却等全部从 EnemyArchetypeSO 或黑板读取。
- **存档/读档**：扩展 **EnemySnapshot** 存当前意图、HasTarget、LastKnownTargetPosition 等；若后续为行为树增加状态序列化则一并存储；读档时恢复意图与关键状态，使敌人从「追击中/巡逻中」等正确恢复。
- 需要时增加新行为（如召唤、格挡）：新增行为树节点类 + 在 `BehaviorTreeFromDataBuilder` 中注册 + 在对应树资产中挂接 + 在 ArchetypeSO 中增加能力与参数。
- 对象池（若用）：敌人禁用/回收时停止 Tick；复用前重置意图、黑板、BT、NavMeshAgent。
- 可选：调试用显示当前敌人意图（HUD/Gizmos）。

---

## 十、文件与资源组织建议

- **脚本**：`Game.Presentation`（或 `Game.AI`）下放置 EnemyController、EnemyAI、EnemyMover、EnemyCombat；行为树运行时与节点类放在 `Game.AI.BehaviorTree`。
- **数据**：`Game.Data` 下新增 EnemyArchetypeSO（或 EnemyBehaviorConfigSO）；EnemyStatsDatabaseSO 保持现有，与 Archetype 并列（一个管数值，一个管行为）。
- **资源**：行为树资产按类型命名（如 `MeleeMinionTree`、`BossTree`），与预制体分离；Animator Controller 按敌人类型/变体命名，挂于对应预制体。

---

## 十一、小结

| 主题 | 结论 |
|------|------|
| 行为树实现 | 自研行为树（Selector/Sequence + 条件/动作节点），从 BehaviorTreeAsset 资产构建运行时树。 |
| 架构 | EnemyController（门面）+ EnemyAI（行为树 + 上下文）+ EnemyMover + EnemyCombat，意图连接 AI 与执行。 |
| 数据驱动 | EnemyArchetypeSO + BehaviorTreeAsset + Animator 资源；不在代码里硬编码树结构。 |
| 事件 | 仅 EventCenter 做关卡/系统级；敌人内部用意图传递。 |
| 动画 | Animator Controller 负责过渡与混合，执行层只设参数。 |
| **死亡** | **不**纳入行为树；由 ApplyDamage/Die 与现有事件/奖励逻辑处理；死亡后不再 Tick 树。 |
| **受伤** | **建议**纳入行为树，作为根 Selector 的第一分支（最高优先级）。 |
| **感知** | 前方扇形视线发现玩家；进入视线后追击；**按距离**丢失目标（超出 chaseBreakDistance 则回巡逻）。§十二。 |
| **Boss** | 5 个预制体对应 5 关；50% 血前 2 技能、50% 血后 4 技能；战斗回忆为独立场景 + 所选 Boss。§十二。 |
| **存档** | 存 AI 状态（意图、关键黑板、BT 状态若支持）；EnemySnapshot 扩展。§十二。 |
| **巡逻** | 半径内随机（patrolRadius）。§十二。 |
| **碰撞** | 敌人与玩家均有物理碰撞。§十二。 |
| **性能** | 不降频，每帧 Tick 所有敌人。§十二。 |

后续实现时以此文档为准，有变更时更新本文档版本与日期。

---

## 十二、决策汇总（已拍板）

以下为**已确定的决策**，实现时必须按此执行；未列出的细节由实现时在架构约束下选择最优做法。

### 12.1 感知

| 项目 | 决策 |
|------|------|
| **发现玩家** | 敌人在**前方扇形范围**内能看到玩家（扇形视线）。参数：扇形角度、扇形距离（检测范围），进 EnemyArchetypeSO。 |
| **开始追击** | 玩家一旦进入扇形视线，敌人即开始追击。 |
| **丢失目标** | **按距离**：玩家超出设定距离后，敌人丢失目标，停止追击，**回到巡逻**。该距离在 ArchetypeSO 中配置（如 `chaseBreakDistance`）。 |
| **实现要点** | 每帧在 Tick 前由感知逻辑：① 计算玩家是否在敌人前方扇形内（角度 + 距离）；② 若当前正在追击，则判断玩家是否超过 chaseBreakDistance，超过则清空目标并回到巡逻。黑板写入：HasTarget、TargetTransform、DistanceToTarget、IsInSector 等。 |

### 12.2 Boss 与关卡

| 项目 | 决策 |
|------|------|
| **Boss 预制体** | **5 个 Boss = 5 个预制体**，分别对应 **5 个关卡的不同 Boss**（关卡 1～5 各一个 Boss 预制体）。 |
| **Boss 降临** | 本关所有守卫者击杀后，提示 5 秒，在玩家附近 40m 内随机位置生成**当前关卡对应的 Boss 预制体**（Level 1 → Boss1 预制体，Level 2 → Boss2 预制体，以此类推）。若需求书中的「从未战胜过的 Boss 集合中随机」与「每关固定 Boss」并存，以本条为准：**每关固定对应一个 Boss 预制体**。 |
| **Boss 阶段** | **有阶段**：**50% 血量前** Boss 只解锁 **2 个技能**；**50% 血量后**解锁剩余 **2 个技能**，共 **4 个技能**。实现：黑板或 Archetype 中维护「当前阶段」（如 Phase 0 / Phase 1），BT 中技能节点或条件按阶段判断是否可用；或 Archetype 中配置「阶段 0 可用技能槽位 [0,1]、阶段 1 可用 [0,1,2,3]」。 |
| **战斗回忆** | 玩家进入一个**战斗回忆场景**，场景内包含**玩家所选择的那个 Boss**（对应预制体）。即：战斗回忆 = 独立场景 + 根据选择生成对应 Boss 预制体，AI 与正常 Boss 一致。 |

### 12.3 存档

| 项目 | 决策 |
|------|------|
| **AI 状态** | **存档需要存 AI 状态**。EnemySnapshot 需扩展，除 id、enemyType、位置、currentHp 外，增加：当前意图（或意图类型枚举）、是否正在追击、目标位置或 null、以及 Behavior Designer 若支持序列化则存 BT 状态，否则至少存意图与关键黑板变量（如 HasTarget、LastKnownTargetPosition），以便读档后敌人从「追击中/巡逻中」等正确恢复。 |

### 12.4 巡逻与移动

| 项目 | 决策 |
|------|------|
| **巡逻方式** | **在半径内随机**：以出生点或当前格中心为圆心，在 ArchetypeSO 配置的 **patrolRadius** 内随机选点作为巡逻目标，到达后再随机选下一点。 |
| **物理碰撞** | **敌人与玩家都有物理碰撞**（使用 Collider，可设置层级使敌人与玩家、敌人与敌人可配置为碰撞或忽略）。NavMeshAgent 与物理碰撞需协调（如 Agent 的 avoidance 与 Rigidbody 是否 kinematic）。 |

### 12.5 性能与其余

| 项目 | 决策 |
|------|------|
| **同屏多敌人** | **不**做 Tick 降频与按距离 LOD；所有敌人每帧正常 Tick 行为树。 |
| **敌人对玩家伤害** | 采用**与玩家伤害管线对称**的方式：敌人攻击动画在命中帧触发检测，使用 **DamageDetectionRunner**（或同一套几何检测），**hitLayerName = "Player"**；伤害名与配置可放在现有 **AnimationFrameDamageDatabaseSO** 中（同一库内不同条目，部分 hitLayer=Enemy、部分 hitLayer=Player），由 EnemyCombat 或敌人动画事件调用，再调 **CombatCalculator.CalculateDamageFromEnemy** 对玩家扣血。 |
| **玩家引用** | 由 **GameStateMachine** 或 **LevelBootstrapper** 提供「当前关卡玩家 Transform」的访问点，EnemyAI 在 Init 或每帧从该处取并写入黑板。 |
| **音效与特效** | **动画事件**触发攻击命中、受击、死亡时的表现；可选在执行层或动画事件中发 **EventCenter** 事件（如 EnemyAttackHit、EnemyDied）供全局音效/镜头等订阅，便于解耦。 |
| **调试** | 可选：开发阶段在 HUD 或 Gizmos 中显示当前敌人意图（如「追击/巡逻/攻击」），按需在阶段 4 实现。 |
| **敌人生成** | 由 **LevelDirector**（或 LevelBootstrapper 调用的生成器）按 LevelConfig 与分格规则生成敌人；预制体与 ArchetypeSO/BT 的绑定方式为：预制体上挂对应 ArchetypeSO 与 External Behavior Tree 引用，或按 EnemyType 从 ConfigManager 取对应 Archetype 与 BT 资源。Boss 由关卡逻辑在「守卫者全灭后 5 秒」生成当前关卡对应 Boss 预制体。 |

---

## 十三、尚未问到的问题（保留作参考）

以下问题在讨论中未 explicitly 问到，但实现前或实现中需要答案，避免返工。

| 类别 | 问题 | 说明 |
|------|------|------|
| **感知** | 敌人如何「看到」玩家？仅距离，还是需要视线（射线）或扇形视野？ | 若只按距离，黑板里写「与玩家距离」即可；若要做「躲掩体后丢失目标」，需要视线检测与「丢失目标超时」逻辑。 |
| **感知** | 发现/丢失目标的规则？追击距离、攻击距离、脱离追击的超时或距离？ | 需在 Archetype 或黑板中约定：detectionRange、chaseRange、meleeRange、rangedRange、lostTargetTime 等，并由谁每帧写入黑板。 |
| **生成** | 谁生成敌人？LevelBootstrapper 还是 LevelDirector / LevelSpawner？ | 项目已有 LevelDirector「具体刷怪由后续实现」；需定：按 LevelConfig 每格数量/位置生成时，敌人预制体从哪来、ArchetypeSO 与 BT 如何绑定（按预制体上的枚举/引用 vs 按生成时注入）。 |
| **生成** | Boss 5 变体：5 个预制体 + 5 份 ArchetypeSO，还是 1 个 Boss 预制体 + 运行时根据「本关选中的 Boss」注入对应 Archetype？ | 影响资源组织与「Boss 降临」时生成逻辑（从未战胜集合随机 → 选哪个预制体或哪份 Archetype）。 |
| **存档** | 读档时敌人 AI 状态（当前意图、黑板、BT 运行到哪）要不要存？ | EnemySnapshot 目前只有 id、enemyType、位置、currentHp。若只存这些，读档后敌人从「待机」重新开始，一般可接受；若需「读档后继续播放攻击动画」则要存更多状态，复杂度高。 |
| **性能** | 同屏敌人多时，是否每帧都 Tick 所有敌人的 BT？ | 可考虑：按距离降低 Tick 频率（远处每 N 帧 Tick）、或超出视距/一定范围暂停 AI，避免性能尖峰。 |
| **Boss 特殊** | Boss 是否有阶段（如血线 50% 换阶段、换技能或行为）？ | 需求书未写；若有，需在黑板或 Archetype 中增加「当前阶段」，BT 或条件任务按阶段选不同子分支。 |
| **碰撞与物理** | 敌人之间会互相挤开吗？敌人与玩家是否物理碰撞？ | 若用 NavMeshAgent，可设 avoidance 或不用；与玩家是仅距离/范围检测还是还有物理碰撞，影响 Mover 与碰撞层设置。 |
| **音效与特效** | 敌人攻击/受击/死亡音效与特效由谁触发？ | 动画事件、执行层、还是 EventCenter 事件？需统一约定，便于策划挂接。 |
| **调试** | 是否需要在游戏中显示当前敌人意图或 BT 当前节点（Debug 用）？ | Behavior Designer 自带调试；若需要 HUD 或 Gizmos 显示「正在追击/攻击」，需在执行层或 Controller 暴露当前意图供调试绘制。 |

---

## 十四、讨论中尚未完全明确的重要细节（部分已由 §十二 定死，其余实现时补齐）

以下细节在架构层面已部分涉及，但尚未落到「谁写、谁读、放哪」的级别，实现时需补齐。

### 14.1 感知与黑板写入（§十二已定：扇形视线 + 距离丢失目标）

- **谁在何时写入黑板？** 每帧在 `EnemyAI.Tick()` 之前，由 **EnemyAI** 或 **EnemyPerception** 执行感知：① 玩家是否在**前方扇形**内（sectorAngle、sectorRange）；② 若正在追击则玩家是否超过 **chaseBreakDistance**，超过则清空目标；③ 距离、是否在近战/远程范围内、受击状态等。写入 BD Variables 或自有 Blackboard。树内节点只读不写（除受击由 ApplyDamage 写入）。
- **玩家引用从哪来？** 由 **GameStateMachine** 或 **LevelBootstrapper** 提供当前关卡玩家 Transform；EnemyAI 在 Init 或每帧从该处取并写入黑板。

### 14.2 敌人对玩家的伤害检测（§十二已定：复用 DamageDetectionRunner，hitLayer=Player）

- **现有管线**：玩家打敌人由 `PlayerCombat` + 动画事件 + `DamageDetectionRunner` + `EnemyController.ApplyDamage` 完成；`hitLayerName = "Enemy"`。
- **敌人打玩家**：敌人攻击动画在命中帧触发检测；调用 **DamageDetectionRunner**，配置中敌人伤害条目 **hitLayerName = "Player"**；可由 EnemyCombat 或动画事件发起，再调 **CombatCalculator.CalculateDamageFromEnemy** 扣玩家血。配置可与玩家共用 **AnimationFrameDamageDatabaseSO**（不同 damageName、不同 hitLayer），或单独敌人伤害库。

### 14.3 巡逻的具体形态（§十二已定：半径内随机）

- **巡逻方式**：**在半径内随机**。EnemyArchetypeSO 中配置 **patrolRadius**（以出生点或当前格中心为圆心）；BT 中 Patrol 任务在半径内随机选点作为目标，到达后再随机选下一点；Mover 只负责「设目标点」。

### 14.4 冷却与连击

- **攻击/技能冷却**：近战连击、远程普攻、技能的冷却在 Archetype 中配数值；BT 内用 **Cooldown** 装饰节点或「上次攻击时间」黑板变量 + 条件任务控制「能否再次选攻击/技能」，避免节点层写死冷却逻辑。
- **敌人近战连击**：段数、窗口时间（类似玩家的 0.3s）若与玩家不同，需在 Archetype 或动画配置中可配；执行层根据意图 MeleeAttack 与当前段数设 Animator 参数（如 AttackIndex）。

### 14.5 远程弹道

- **法球/箭**：预制体、速度、是否追踪、碰撞层、伤害来源（哪个敌人、用哪条伤害配置）——建议在 Archetype 或单独 ProjectileConfig 中配置；执行层生成弹道时带上传入「攻击者 EnemyController」与「伤害名」，命中时用 `CombatCalculator.CalculateDamageFromEnemy` 对玩家结算。若弹道需要池化，需约定生成与回收入口。

### 14.6 Boss 降临与关卡流程（§十二已定：5 预制体对应 5 关，战斗回忆独立场景）

- **Boss 降临**：本关所有守卫者击杀后，提示 5 秒，在玩家附近 40m 内随机位置生成**当前关卡对应的 Boss 预制体**（Level 1→Boss1 预制体，…，Level 5→Boss5 预制体）。关卡逻辑由 LevelDirector 或监听 GuardianDied 的服务负责。
- **战斗回忆**：玩家选择要挑战的 Boss 后，进入**战斗回忆场景**，场景内生成**玩家所选的那个 Boss 预制体**；AI 与正常 Boss 一致。

### 14.7 对象池与复用

- **敌人禁用/回收**：若使用对象池，在 **复用前** 必须重置：CurrentIntent 清空、黑板清空或重置、BD 树若支持 Reset 则调用、NavMeshAgent 停用/清目标；**死亡时** 只做禁用或回收到池，不 Tick 树（与第八章一致）。

### 14.8 朝向与根运动

- **追击时面向玩家**：建议由 **EnemyMover** 在「当前意图为 Chase 且目标有效」时，每帧或每 N 帧设置敌人朝向目标（或 NavMeshAgent 的 rotation 设置），与 Animator 的 Root Motion 若冲突需关闭 Agent 的 auto rotation 或由 Animator 驱动旋转，二选一在 Mover 中统一。

---

## 十五、决策清单（已拍板项见 §十二，以下为历史清单供核对）

- [x] 感知：**前方扇形视线**；进入视线后追击；**按距离丢失目标**（超出 chaseBreakDistance 则停止追击回巡逻）。
- [x] 感知/追击/攻击的数值：sectorAngle、sectorRange、chaseBreakDistance、meleeRange、rangedRange 等进 ArchetypeSO。
- [x] 敌人生成：由 LevelDirector（或生成器）按 LevelConfig 与格子生成；Boss 为 5 个预制体对应 5 关，降临时生成当前关对应 Boss 预制体。
- [x] Boss 5 变体：**5 个预制体**，对应 5 个关卡的不同 Boss。
- [x] 敌人对玩家伤害：复用 DamageDetectionRunner，hitLayer=Player，配置在同一或独立库；EnemyCombat/动画事件调用。
- [x] 巡逻：**半径内随机**（patrolRadius），Archetype 配置。
- [x] 读档：**存 AI 状态**；EnemySnapshot 扩展存意图与关键黑板变量（及 BT 状态若支持）。
- [x] 音效/特效：动画事件触发；可选 EventCenter 供全局订阅。
- [x] Boss 阶段：**50% 血前 2 技能、50% 血后 4 技能**；战斗回忆为**独立场景 + 玩家所选 Boss 预制体**。
- [x] 同屏多敌人：不降频，每帧 Tick。
- [ ] （可选）调试显示当前意图，按需在阶段 4 实现。
