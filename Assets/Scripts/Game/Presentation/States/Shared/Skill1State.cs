namespace Game.Presentation
{
    /// <summary>技能 1（默认按键 E）。</summary>
    public class Skill1State : SkillStateBase
    {
        protected override int   SkillIndex => 1;
        protected override float MpCost     => 20f;
    }
}
