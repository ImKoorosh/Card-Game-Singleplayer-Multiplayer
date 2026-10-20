using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>Lightning strike: damages and stuns enemy units in the area. (Example of extending the spell system.)</summary>
    public class StunSpell : Spell
    {
        protected override void CastFx()
        {
            AudioManager.Play(SfxId.Zap, 0.9f);
            Vfx.Ring(center, stats.radius, fxColor, 0.5f);
        }

        protected override void TickFx()
        {
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = RandomPointInArea();
                Vfx.Bolt(p + Vector3.up * 9f, p, fxColor);
                Vfx.Sparks(p, fxColor);
            }
            if (BattleCameraController.Instance != null) BattleCameraController.Instance.Shake(0.15f, 0.2f);
        }

        protected override void ApplyEffect()
        {
            List<Damageable> enemies = Targets(d => d.Team != team && d.Kind == EntityKind.Unit);
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit u = enemies[i] as Unit;
                if (u == null) continue;
                if (stats.amount > 0f) u.TakeDamage(stats.amount);
                if (!u.IsDead) u.ApplyStun(stats.stunSeconds);
            }
        }
    }
}
