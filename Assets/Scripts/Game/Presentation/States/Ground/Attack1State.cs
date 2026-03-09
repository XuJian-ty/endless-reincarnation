namespace Game.Presentation
{
    /// <summary>普攻第一段。连击窗口期内再按攻击 → Attack2。</summary>
    public class Attack1State : AttackStateBase
    {
        protected override int ComboIndex => 0;
    }
}
