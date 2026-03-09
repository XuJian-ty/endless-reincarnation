namespace Game.Presentation
{
    /// <summary>技能 0（默认按键 Q）。覆写 MpCost / HandleHit / OnTick 实现独特技能逻辑。</summary>
    public class Skill0State : SkillStateBase
    {
        protected override int   SkillIndex => 0;
        protected override float MpCost     => 20f;
    }
}
