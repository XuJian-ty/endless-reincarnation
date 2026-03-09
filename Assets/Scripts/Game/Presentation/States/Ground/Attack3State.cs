namespace Game.Presentation
{
    /// <summary>普攻第三段。连击窗口期内再按攻击 → Attack4。</summary>
    public class Attack3State : AttackStateBase
    {
        protected override int ComboIndex => 2;
    }
}
