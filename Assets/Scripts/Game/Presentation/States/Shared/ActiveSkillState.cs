using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 通用主动技能状态。通过技能槽位索引读取当前装备在该槽位中的主动技能 actionId。
    /// </summary>
    public sealed class ActiveSkillState : SkillStateBase
    {
        private int _skillIndex;

        protected override int SkillSlotIndex => _skillIndex;

        public void Configure(int skillIndex)
        {
            _skillIndex = Mathf.Max(0, skillIndex);
        }
    }
}
