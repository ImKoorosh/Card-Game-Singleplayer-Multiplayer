using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>Each attack arcs to additional nearby enemies with reduced damage (chain lightning).</summary>
    public class ChainAttackAbility : UnitAbility
    {
        [SerializeField] int jumps = 2;
        [SerializeField] float jumpRadius = 3.2f;
        [SerializeField, Range(0.1f, 1f)] float falloff = 0.6f;
        [SerializeField] Color color = new Color(0.6f, 0.85f, 1f);

        public override void OnAttack(Damageable target, float damage)
        {
            NetFx.Bolt(Owner.transform.position + Vector3.up * 1.6f, target.AimPoint, color);
            var hit = new HashSet<Damageable> { target };
            Damageable current = target;
            float d = damage;
            for (int i = 0; i < jumps; i++)
            {
                Damageable next = Nearest(current.transform.position, hit);
                if (next == null) break;
                d *= falloff;
                NetFx.Bolt(current.AimPoint, next.AimPoint, color);
                next.TakeDamage(d, Owner);
                hit.Add(next);
                current = next;
            }
        }

        Damageable Nearest(Vector3 from, HashSet<Damageable> exclude)
        {
            Damageable best = null;
            float bestDist = jumpRadius;
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == null || d.IsDead || d.Team == Owner.Team || exclude.Contains(d) || !Owner.CanTarget(d)) continue;
                float e = d.EdgeDistance(from);
                if (e <= bestDist) { bestDist = e; best = d; }
            }
            return best;
        }
    }
}
