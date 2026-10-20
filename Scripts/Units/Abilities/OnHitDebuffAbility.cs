using UnityEngine;

namespace CardBattle
{
    /// <summary>Attacks slow the target; optionally every Nth hit also stuns it.</summary>
    public class OnHitDebuffAbility : UnitAbility
    {
        [SerializeField, Range(0.05f, 1f)] float slowMultiplier = 0.6f;
        [SerializeField] float slowSeconds = 1.5f;
        [Tooltip("0 = never stun. 3 = every third hit stuns.")]
        [SerializeField] int stunEvery = 0;
        [SerializeField] float stunSeconds = 0.6f;

        int hits;

        public override void OnAttack(Damageable target, float damage)
        {
            Unit u = target as Unit;
            if (u == null) return;
            if (slowSeconds > 0f) u.ApplySlow(slowMultiplier, slowSeconds);
            Vfx.Sparks(target.AimPoint, new Color(0.6f, 0.9f, 1f));
            hits++;
            if (stunEvery > 0 && hits % stunEvery == 0) u.ApplyStun(stunSeconds);
        }
    }
}
