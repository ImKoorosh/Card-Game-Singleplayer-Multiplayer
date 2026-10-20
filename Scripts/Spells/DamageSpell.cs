using System.Collections.Generic;

namespace CardBattle
{
    /// <summary>Damages every enemy in the area. Towers take reduced damage (towerDamageMultiplier).</summary>
    public class DamageSpell : Spell
    {
        protected override void CastFx()
        {
            Vfx.Explosion(center, stats.radius, fxColor);
            AudioManager.Play(SfxId.Explosion, 1f);
            if (BattleCameraController.Instance != null) BattleCameraController.Instance.Shake(0.25f, 0.3f);
        }

        protected override void ApplyEffect()
        {
            List<Damageable> victims = Targets(d => d.Team != team);
            for (int i = 0; i < victims.Count; i++)
            {
                float a = victims[i].Kind == EntityKind.Tower ? stats.amount * stats.towerDamageMultiplier : stats.amount;
                victims[i].TakeDamage(a);
            }
        }
    }
}
