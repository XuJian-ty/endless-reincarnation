# Buff 配置与特殊效果接入说明

## 统一思路

- **配置**：所有 Buff（属性类 + 特殊效果类）都在 **Buff配置** 里配一条，包含：`buffId`、显示名、描述、`type`、以及可选的属性数值 / 特殊参数。
- **属性类**：`GetModifierForBuff(buffId)` 生成 `StatModifier`，由 `PlayerModel.AddBuff(buffId, modifier)` 挂到 Stats，**无需额外接入**。
- **特殊效果类**：只把 `buffId` 记在 `PlayerModel.BuffIds` 里（`AddBuff(buffId, null)` 或 modifier 全 0），**实际效果在各自系统里用 `PlayerModel.HasBuff(buffId)` 或 `BuffIds.xxx` 判断并实现**。参数从 `BuffConfigSO` 的 `GetDamageRangeScale`、`GetAfterimageDelaySeconds`、`GetSummonCloneSettings` 等读取。

---

## 已接入

| Buff | buffId 常量 | 效果 | 接入位置 |
|------|-------------|------|----------|
| 霸体加减伤 | `BuffIds.SuperArmor` | 受击不进入硬直，并通过减伤乘区降低受到的伤害 | `PlayerController.OnHit` + `CombatCalculator.CalculateDamageFromEnemy` + `PlayerModel.Stats.DamageReduce` |
| 召唤分身 | `BuffIds.SummonClone` | 生成半透明分身跟随玩家，分身 AI 会搜索目标、追击、普攻并共享玩家主动技能解锁状态与冷却 | `PlayerCloneManager` + `PlayerCloneActor` |
| 攻击附带重影 | `BuffIds.Afterimage` | 玩家攻击/技能会在延迟后再复制一份同源的伤害、特效与音效事件，并按配置附加重影伤害倍率 | `PlayerStateBase.StartTimelineSkill` + `PlayerBuffRuntimeUtility.BuildRuntimeSkillDefinition` |
| 伤害范围扩大 | `BuffIds.DamageRangeExpand` | 特效、命中特效、伤害范围、碰撞体和射线距离按倍率扩大 | `DamageDetectionRunner` + `SkillCollisionHitbox` + `SkillEffectExecutor` |

---

## 配置字段说明（BuffEntry）

- **type**：`StatOnly` / `SuperArmorDamageReduce` / `SummonClone` / `Afterimage` / `DamageRangeExpand`。决定显示哪些字段以及运行时逻辑。
- **afterimageDelaySeconds**：仅当 type = `Afterimage` 时有效，重影事件相对原事件的延迟（秒）。
- **damageReduceAdd**：仅当 type = `SuperArmorDamageReduce` 时有效，提供减伤乘区中的减伤值。
- **damageRangeScale**：仅当 type = `DamageRangeExpand` 时有效，相对基础值的倍数（2 = 扩大 100%）。
- **cloneFollowRadius / cloneOpacity**：仅当 type = `SummonClone` 时有效，分别控制分身环绕半径和透明度。
- 下方属性字段：对 `StatOnly` 或「属性+特殊」都生效；纯特殊效果可全 0。

新增 Buff 时：在 Buff 配置里加一条，选好 `type`，填好显示与参数；若为特殊效果，再在表中所列位置用 `HasBuff(BuffIds.xxx)` 或配置 API 实现逻辑即可。
