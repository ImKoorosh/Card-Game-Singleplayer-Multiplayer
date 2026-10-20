using System.Collections.Generic;

namespace CardBattle
{
    /// <summary>
    /// Slows enemy units (movement and attack speed). The slow is refreshed every tick and expires
    /// shortly after a unit leaves the area or the spell ends. 'amount' optionally adds damage per tick.
    /// </summary>
    public class SlowSpell : Spell
    {
        protected override void CastFx()
        {
            AudioManager.Play(SfxId.Freeze, 0.9f);
            Vfx.Ring(center, stats.radius, fxColor, 0.5f);
        }

        protected override void TickFx()
        {
            for (int i = 0; i < 2; i++) Vfx.Burst(RandomPointInArea(), fxColor, 4, 0.8f, 0.3f, 0.7f, -0.1f); // icy mist
        }

        protected override void ApplyEffect()
        {
            List<Damageable> enemies = Targets(d => d.Team != team && d.Kind == EntityKind.Unit);
            for (int i = 0; i < enemies.Count; i++)
            {
                Unit u = enemies[i] as Unit;
                if (u == null) continue;
                u.ApplySlow(stats.slowMultiplier, stats.tickInterval + 0.25f);
                if (stats.amount > 0f) u.TakeDamage(stats.amount);
            }
        }
    }
}
