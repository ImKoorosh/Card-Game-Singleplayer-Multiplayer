using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Fires projectiles (archer-style). With splashRadius &gt; 0 the projectile explodes (magic-style).
    /// If projectileSpeed is 0 the hit is instant.
    /// </summary>
    public class RangedUnit : Unit
    {
        [SerializeField] Projectile projectilePrefab;
        [Tooltip("Optional spawn point for projectiles.")]
        [SerializeField] Transform muzzle;

        // Remote clients: show the shot without any damage (the server already resolved it).
        protected override void PlayRemoteAttackFx(Damageable t)
        {
            if (projectileSpeed <= 0.01f) return;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1f;
            Projectile.Spawn(projectilePrefab, origin, t, 0f, projectileSpeed, splashRadius, team, null, true);
        }

        protected override void PerformAttack(Damageable t)
        {
            if (projectileSpeed <= 0.01f) { DealDirectDamage(t); return; }
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 1f;
            Projectile.Spawn(projectilePrefab, origin, t, attackDamage, projectileSpeed, splashRadius, team, this);
        }
    }
}
