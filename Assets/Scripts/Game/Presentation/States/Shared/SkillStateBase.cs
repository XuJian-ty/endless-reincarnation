using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 技能状态基类。OnEnter 扣 MP、TriggerSkill；OnTick 等动画结束 → CompleteWithPending。
    /// 技能伤害由动画事件触发（AnimEvent_DealDamage），或通过共享技能时间轴驱动（若配置了 sharedSkillId）。
    /// </summary>
    public abstract class SkillStateBase : PlayerStateBase
    {
        protected abstract int   SkillIndex { get; }
        protected abstract float MpCost     { get; }

        public override GameAction CurrentActionId => GameAction.Skill;

        private SkillTimelineRunner _timelineRunner;

        protected override void OnEnter()
        {
            if (!Ctx.PlayerModel.CanSpendMp(MpCost))
            {
                GoTo<IdleState>();
                return;
            }
            Ctx.PlayerModel.SpendMp(MpCost);
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            Ctx.Anim.TriggerSkill(SkillIndex);

            // 启动共享技能时间轴（若有配置）
            TryStartTimeline();
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            _timelineRunner?.Tick(dt);
            if (AnimNearEnd()) CompleteWithPending(() => { if (IsGrounded) GoTo<IdleState>(); else GoTo<FallState>(); });
        }

        protected override void OnExit()
        {
            _timelineRunner?.Stop();
            _timelineRunner = null;
        }

        public override TransitionPolicy GetPolicyFor(GameAction action) => action switch
        {
            GameAction.Dodge         => TransitionPolicy.Interrupt,
            GameAction.Skill         => TransitionPolicy.Interrupt,
            GameAction.ChargeStart   => TransitionPolicy.Interrupt,
            GameAction.NormalAttack  => TransitionPolicy.Ignore,
            GameAction.ChargeRelease => TransitionPolicy.Ignore,
            _                        => TransitionPolicy.Buffer,
        };

        // ── 时间轴初始化 ─────────────────────────────────────────────────────

        private void TryStartTimeline()
        {
            var skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            if (skillDb == null) return;

            // 通过动画状态索引匹配当前技能条目
            SkillConfigEntry entry = FindEntryByStateIndex(skillDb, SkillIndex);
            if (entry == null || string.IsNullOrEmpty(entry.sharedSkillId)) return;

            var sharedDb = ConfigManager.GetInstance()?.GetSharedSkillDatabase();
            if (sharedDb == null) return;

            var def = sharedDb.GetEntry(entry.sharedSkillId);
            if (def == null) return;

            var player = Ctx.Transform?.GetComponent<PlayerController>();
            if (player == null) return;

            var ctx = new PlayerSkillExecutionContext(player);
            _timelineRunner = new SkillTimelineRunner();
            _timelineRunner.Begin(def, ctx);
        }

        private static SkillConfigEntry FindEntryByStateIndex(SkillConfigDatabaseSO db, int stateIndex)
        {
            if (db?.entries == null) return null;
            for (int i = 0; i < db.entries.Count; i++)
            {
                var e = db.entries[i];
                if (e != null && !e.isPassive && e.animationStateIndex == stateIndex)
                    return e;
            }
            return null;
        }
    }
}
