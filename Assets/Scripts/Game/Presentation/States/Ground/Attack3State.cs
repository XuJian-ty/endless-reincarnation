namespace Game.Presentation
{
    /// <summary>普攻第四段（末段）。四段用完后不再开启连击窗口。</summary>
    public class Attack3State : AttackStateBase
    {
        protected override int ComboIndex => 3;
    }
}
