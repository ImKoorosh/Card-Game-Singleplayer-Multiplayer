using UnityEngine;

namespace CardBattle
{
    /// <summary>Heals the unit for a fraction of the damage it deals.</summary>
    public class LifeStealAbility : UnitAbility
    {
        [SerializeField, Range(0f, 1f)] float fraction = 0.5f;

        public override void OnAttack(Damageable target, float damage)
        {
            if (damage <= 0f) return;
            Owner.Heal(damage * fraction);
            Vfx.Burst(Owner.AimPoint, new Color(0.4f, 1f, 0.5f), 3, 0.8f, 0.18f, 0.6f, -0.8f);
        }
    }
}
