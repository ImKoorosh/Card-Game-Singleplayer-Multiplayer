using UnityEngine;

namespace CardBattle
{
    /// <summary>A protective bubble that absorbs a fixed amount of damage before the unit's health is touched.</summary>
    public class ShieldAbility : UnitAbility
    {
        [SerializeField] float shieldHealth = 150f;
        [SerializeField] Color color = new Color(0.5f, 0.8f, 1f, 0.3f);

        float remaining;
        GameObject bubble;
        Material bubbleMat;

        public float Remaining { get { return remaining; } }

        protected override void OnBind()
        {
            remaining = shieldHealth;
            bubbleMat = PlaceholderUtil.MakeOverlay(color);
            float d = Owner.BodyRadius * 2.7f;
            // Named "Fx_*" so HitFlash ignores it.
            bubble = PlaceholderUtil.Part(PrimitiveType.Sphere, "Fx_Shield", Owner.transform, new Vector3(0f, 0.9f, 0f), new Vector3(d, d * 1.25f, d), bubbleMat);
        }

        public override float ModifyIncomingDamage(float damage)
        {
            if (remaining <= 0f) return damage;
            float absorbed = Mathf.Min(remaining, damage);
            remaining -= absorbed;
            AudioManager.Play(SfxId.Shield, 0.5f);
            Vfx.Sparks(Owner.AimPoint, new Color(color.r, color.g, color.b, 1f));
            if (remaining <= 0f && bubble != null)
            {
                Vfx.Burst(Owner.AimPoint, new Color(color.r, color.g, color.b, 1f), 14, 3.5f, 0.25f, 0.5f, 0.4f);
                Destroy(bubble);
            }
            return damage - absorbed;
        }

        void OnDestroy() { if (bubbleMat != null) Destroy(bubbleMat); }
    }
}
