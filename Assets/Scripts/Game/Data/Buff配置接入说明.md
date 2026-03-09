# Buff 配置与特殊效果接入说明

## 统一思路

- **配置**：所有 Buff（属性类 + 特殊效果类）都在 **Buff配置** 里配一条，包含：`buffId`、显示名、描述、`type`、以及可选的属性数值 / 特殊参数。
- **属性类**：`GetModifierForBuff(buffId)` 生成 `StatModifier`，由 `PlayerModel.AddBuff(buffId, modifier)` 挂到 Stats，**无需额外接入**。
- **特殊效果类**：只把 `buffId` 记在 `PlayerModel.BuffIds` 里（`AddBuff(buffId, null)` 或 modifier 全 0），**实际效果在各自系统里用 `PlayerModel.HasBuff(buffId)` 或 `BuffIds.xxx` 判断并实现**。参数从 `BuffConfigSO` 的 `GetMeleeRangeScale`、`GetMultishotDelaySeconds` 等读取。

---

## 已接入

| Buff | buffId 常量 | 效果 | 接入位置 |
|------|-------------|------|----------|
| 全程霸体 | `BuffIds.SuperArmor` | 受击不进入硬直，只扣血 | `PlayerController.OnHit`：若 `HasBuff(BuffIds.SuperArmor)` 则不 `ChangeState<HitStunState>` |

---

## 待接入（在对应系统里查 HasBuff / 配置）

| Buff | buffId 常量 | 预期效果 | 建议接入位置 |
|------|-------------|----------|--------------|
| 召唤分身 | `BuffIds.SummonClone` | 生成友方分身单位 | 关卡/战斗逻辑：进关卡或满足条件时若 `HasBuff(SummonClone)` 则生成分身预制体并挂 AI/跟随逻辑。 |
| 攻击重影 | `BuffIds.Afterimage` | 攻击附带重影与额外伤害 | 攻击/伤害检测（如 `PlayerCombat` 或动画事件）：若 `HasBuff(Afterimage)` 在命中时再跑一次伤害或生成重影特效。 |
| 近战范围扩大 | `BuffIds.MeleeRangeExpand` | 碰撞体长度、伤害检测范围、特效范围 × 倍数 | 武器/伤害检测：读取 `BuffConfigSO.GetMeleeRangeScale(buffId)`（如 2 表示 100% 扩大），在计算碰撞体、DamageDetectionRunner 的 shape 半径/长度、以及特效 scale 时乘上该倍数。 |
| 多重射击 | `BuffIds.Multishot` | 远程多发射一发，间隔可配 | 远程射击逻辑：若 `HasBuff(Multishot)` 在发射一次后，延迟 `BuffConfigSO.GetMultishotDelaySeconds(buffId)`（如 0.2）再触发一次发射。 |

---

## 配置字段说明（BuffEntry）

- **type**：`StatOnly` / `SuperArmor` / `SummonClone` / `Afterimage` / `MeleeRangeExpand` / `Multishot`。决定是否只做属性加成、以及特殊逻辑用哪个 id 判断。
- **meleeRangeScale**：仅当 type = `MeleeRangeExpand` 时有效，相对基础值的倍数（2 = 扩大 100%）。
- **multishotDelaySeconds**：仅当 type = `Multishot` 时有效，额外一发子弹的延迟（秒）。
- 下方属性字段：对 `StatOnly` 或「属性+特殊」都生效；纯特殊效果可全 0。

新增 Buff 时：在 Buff 配置里加一条，选好 `type`，填好显示与参数；若为特殊效果，再在表中所列位置用 `HasBuff(BuffIds.xxx)` 或配置 API 实现逻辑即可。
