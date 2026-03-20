using UnityEngine;

namespace Game.Presentation
{
    /// <summary>
    /// 通用主动技能状态。按技能槽位索引拼出对应 ActionId，例如 Skill0、Skill4。
    /// </summary>
    public sealed class ActiveSkillState : SkillStateBase
    {
        private int _skillIndex;

        protected override string SkillActionId => $"Skill{_skillIndex}";

        public void Configure(int skillIndex)
        {
            _skillIndex = Mathf.Max(0, skillIndex);
        }
    }
}
