namespace CardBattle
{
    /// <summary>Hits the target instantly when within reach. Set splashRadius &gt; 0 for area melee.</summary>
    public class MeleeUnit : Unit
    {
        protected override void PerformAttack(Damageable t) { DealDirectDamage(t); }
    }
}
