using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public static class CombatUtil
    {
        /// <summary>All living damageables whose body touches the circle and pass the filter.</summary>
        public static List<Damageable> Collect(Vector3 center, float radius, Func<Damageable, bool> filter)
        {
            var result = new List<Damageable>();
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable d = all[i];
                if (d == null || d.IsDead) continue;
                if (filter != null && !filter(d)) continue;
                if (d.EdgeDistance(center) <= radius) result.Add(d);
            }
            return result;
        }

        /// <summary>Damages every enemy of <paramref name="attackerTeam"/> inside the circle.</summary>
        public static void DamageArea(Vector3 center, float radius, Team attackerTeam, float amount, Damageable source,
                                      bool hitAir, bool hitGround, float towerMultiplier = 1f)
        {
            // Collect first, then damage: killing something mutates Damageable.All.
            List<Damageable> victims = Collect(center, radius,
                d => d.Team != attackerTeam && (d.IsFlying ? hitAir : hitGround));
            for (int i = 0; i < victims.Count; i++)
            {
                float a = victims[i].Kind == EntityKind.Tower ? amount * towerMultiplier : amount;
                victims[i].TakeDamage(a, source);
            }
        }
    }
}
