using UnityEngine;
using Game.Data;

namespace Game.Presentation
{
    /// <summary>
    /// 技能状态基类。OnEnter 扣 MP、TriggerSkill；OnTick 等动画结束 → CompleteWithPending。
    /// 技能效果统一通过技能时间轴驱动。
    /// </summary>
    public abstract class SkillStateBase : PlayerStateBase
    {
        protected abstract string SkillId { get; }

        public override GameAction CurrentActionId => GameAction.Skill;

        protected override void OnEnter()
        {
            SkillConfigEntry entry = ResolveEntry();
            if (entry == null || !entry.IsActiveSkill || !Ctx.PlayerModel.IsSkillAvailable(entry))
            {
                GoTo<IdleState>();
                return;
            }

            if (!Ctx.PlayerModel.CanSpendMp(entry.mpCost))
            {
                GoTo<IdleState>();
                return;
            }
            Ctx.PlayerModel.SpendMp(entry.mpCost);
            Ctx.Mover.SetHorizontalVelocity(Vector3.zero);
            TriggerConfiguredAction(entry.skillId, entry.GetResolvedAnimationTrigger());

            // 启动共享技能时间轴（若有配置）
            TryStartTimeline(entry);
        }

        protected override void OnTick(float dt, in PlayerInputData input)
        {
            if (AnimNearConfiguredEnd())
            {
                CompleteWithPending(() =>
                {
                    if (IsGrounded)
                        GoToConfiguredNaturalExit(Game.Data.PlayerStateNaturalExitTarget.IdleState);
                    else
                        GoTo<FallState>();
                });
            }
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

        private void TryStartTimeline(SkillConfigEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.skillId)) return;

            var sharedDb = ConfigManager.GetInstance()?.GetSkillDatabase();
            if (sharedDb == null) return;

            var def = sharedDb.GetEntry(entry.skillId);
            if (def == null) return;

            StartTimelineSkill(entry.skillId);
        }

        private SkillConfigEntry ResolveEntry()
        {
            var skillDb = ConfigManager.GetInstance()?.GetSkillConfigDatabase();
            return skillDb != null ? skillDb.GetEntry(SkillId) : null;
        }
    }
}
