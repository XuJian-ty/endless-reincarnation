using System.Collections.Generic;
using Game.Data;

namespace Game.Editor
{
    internal sealed class SkillTimelineEvent
    {
        public string eventId = "";
        public float startTime = 0f;
        public SkillEventTriggerMode triggerMode = SkillEventTriggerMode.Once;
        public float activeDuration = 0f;
        public SkillEventActiveDurationMode activeDurationMode = SkillEventActiveDurationMode.FixedTime;
        public float repeatInterval = 0.1f;

        public List<SkillDamageEffect> damageEffects = new List<SkillDamageEffect>();
        public List<SkillPhysicsEffect> physicsEffects = new List<SkillPhysicsEffect>();
        public List<SkillAttributeEffect> attributeEffects = new List<SkillAttributeEffect>();
        public List<SkillVfxEffect> vfxEffects = new List<SkillVfxEffect>();
        public List<SkillSfxEffect> sfxEffects = new List<SkillSfxEffect>();
    }
}
