using System.Collections.Generic;

namespace CardBattle
{
    /// <summary>Heals friendly units and buildings (not towers) every tick.</summary>
    public class HealSpell : Spell
    {
        protected override void CastFx()
        {
            AudioManager.Play(SfxId.Heal, 0.9f);
            Vfx.Ring(center, stats.radius, fxColor, 0.6f);
        }

        protected override void TickFx()
        {
            for (int i = 0; i < 3; i++) Vfx.Burst(RandomPointInArea(), fxColor, 3, 0.6f, 0.2f, 0.9f, -0.8f); // rising sparkles
        }

        protected override void ApplyEffect()
        {
            List<Damageable> allies = Targets(d => d.Team == team && d.Kind != EntityKind.Tower);
            for (int i = 0; i < allies.Count; i++) allies[i].Heal(stats.amount);
        }
    }
}
