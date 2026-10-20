using UnityEngine;

namespace CardBattle
{
    /// <summary>Homing projectile. Damages one target, or an area if splash &gt; 0. Self-destructs safely.</summary>
    public class Projectile : MonoBehaviour
    {
        Damageable target, source;
        Team team;
        float damage, speed, splash, life = 6f;
        bool cosmetic;   // remote-client copy: visuals only, never damages anything
        Vector3 lastAim;

        public static Projectile Spawn(Projectile prefab, Vector3 origin, Damageable target, float damage,
                                       float speed, float splash, Team team, Damageable source, bool cosmetic = false)
        {
            if (target == null) return null;
            Projectile p;
            if (prefab != null)
            {
                p = Instantiate(prefab, origin, Quaternion.identity);
            }
            else
            {
                // Fallback so ranged units work even when no projectile prefab is assigned.
                GameObject go = PlaceholderUtil.Part(PrimitiveType.Sphere, "Tint_Orb", null, origin, Vector3.one * 0.3f, Color.white);
                p = go.AddComponent<Projectile>();
            }
            p.target = target; p.source = source; p.team = team; p.cosmetic = cosmetic;
            p.damage = damage; p.speed = Mathf.Max(0.1f, speed); p.splash = splash;
            p.lastAim = target.AimPoint;
            TeamUtil.Tint(p.transform, team);

            // Glowing trail in the team colour.
            TrailRenderer tr = p.GetComponent<TrailRenderer>();
            if (tr == null) tr = p.gameObject.AddComponent<TrailRenderer>();
            Color c = team.GetColor();
            tr.time = 0.22f;
            tr.startWidth = 0.16f;
            tr.endWidth = 0f;
            tr.sharedMaterial = Vfx.SharedOverlay;
            tr.startColor = c;
            tr.endColor = new Color(c.r, c.g, c.b, 0f);
            return p;
        }

        void Update()
        {
            life -= Time.deltaTime;
            if (life <= 0f) { Destroy(gameObject); return; }

            if (target != null && !target.IsDead) lastAim = target.AimPoint;
            Vector3 to = lastAim - transform.position;
            float dist = to.magnitude;
            float step = speed * Time.deltaTime;
            if (dist <= step + 0.05f) { Hit(); return; }
            Vector3 dir = to / dist;
            transform.position += dir * step;
            transform.forward = dir;
        }

        void Hit()
        {
            if (splash > 0.01f)
            {
                Vector3 ground = new Vector3(lastAim.x, 0f, lastAim.z);
                if (!cosmetic) CombatUtil.DamageArea(ground, splash, team, damage, source, true, true);
                Vfx.Explosion(ground, splash, team.GetColor());
                AudioManager.Play(SfxId.Explosion, 0.45f);
            }
            else
            {
                if (!cosmetic && target != null && !target.IsDead) target.TakeDamage(damage, source);
                Vfx.Sparks(lastAim, team.GetColor());
            }
            Destroy(gameObject);
        }
    }
}
