namespace Game.Presentation
{
    /// <summary>普攻第二段。连击窗口期内再按攻击 → Attack3。</summary>
    public class Attack2State : AttackStateBase
    {
        protected override int ComboIndex => 1;
    }
}
