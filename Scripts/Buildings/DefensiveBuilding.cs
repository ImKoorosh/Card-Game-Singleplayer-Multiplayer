using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// A building that shoots enemies in range. Towers derive from this class.
    /// Uses a single coroutine loop: scan -> fire -> wait for the cooldown, so it can never attack twice per cooldown.
    /// </summary>
    public class DefensiveBuilding : Building
    {
        [Header("Attack")]
        [SerializeField] protected float damage = 60f;
        [SerializeField] protected float attackRange = 7f;
        [Tooltip("Attacks per second.")]
        [SerializeField] protected float attackSpeed = 1.2f;
        [Tooltip("0 = instant hit.")]
        [SerializeField] protected float projectileSpeed = 14f;
        [SerializeField] protected float splashRadius = 0f;
        [SerializeField] protected bool canTargetAir = true;
        [SerializeField] protected bool canTargetGround = true;
        [SerializeField] protected Projectile projectilePrefab;
        [Tooltip("Where projectiles spawn.")]
        [SerializeField] protected Transform muzzle;
        [Tooltip("Optional part that turns toward the target.")]
        [SerializeField] protected Transform turret;
        [SerializeField] SfxId fireSfx = SfxId.TowerShot;
        [SerializeField, Range(0f, 1f)] float fireSfxVolume = 0.45f;
        [Tooltip("Delay after being placed before it starts shooting.")]
        [SerializeField] float deployDelay = 1f;

        protected Damageable target;
        Vector3 turretBase;
        Coroutine recoil;

        protected override void ApplyStats(CardStats s)
        {
            damage = s.damage;
            attackRange = s.attackRange;
            attackSpeed = s.attackSpeed;
            projectileSpeed = s.projectileSpeed;
            splashRadius = s.splashRadius;
            canTargetAir = s.canTargetAir;
            canTargetGround = s.canTargetGround;
        }

        protected override void Start()
        {
            base.Start();
            if (turret != null) turretBase = turret.localPosition;
            if (NetworkGame.Authority) StartCoroutine(AttackLoop()); // remote clients only replay the server's shots
        }

        IEnumerator AttackLoop()
        {
            if (Data != null) yield return new WaitForSeconds(deployDelay);
            var scan = new WaitForSeconds(0.15f);
            WaitForSeconds cooldown = new WaitForSeconds(1f / Mathf.Max(0.05f, attackSpeed));

            while (!IsDead)
            {
                if (!MatchManager.IsRunning) { yield return scan; continue; }

                if (target == null || target.IsDead || target.EdgeDistance(transform.position) > attackRange)
                    target = TargetFinder.Find(team, transform.position, attackRange, canTargetAir, canTargetGround, TargetPriority.Nearest, false);

                if (target == null) { yield return scan; continue; }

                Fire(target);
                yield return cooldown; // the cooldown is the only thing gating the next shot
            }
        }

        protected virtual void Fire(Damageable t)
        {
            Vector3 origin = PlayFireFx(t);
            NetworkEntity net = GetComponent<NetworkEntity>();
            if (net != null) net.BroadcastAttack(t); // cosmetic copy for remote clients

            if (projectileSpeed > 0.01f)
            {
                Projectile.Spawn(projectilePrefab, origin, t, damage, projectileSpeed, splashRadius, team, this);
            }
            else if (splashRadius > 0.01f)
                CombatUtil.DamageArea(t.transform.position, splashRadius, team, damage, this, canTargetAir, canTargetGround);
            else
                t.TakeDamage(damage, this);
        }

        // Turret turn, muzzle flash, sound and recoil. Shared by the real shot (server/offline) and the remote replay.
        Vector3 PlayFireFx(Damageable t)
        {
            if (turret != null)
            {
                Vector3 flat = t.transform.position - turret.position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.001f) turret.rotation = Quaternion.LookRotation(flat);
            }
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up * 2f;
            Vfx.Burst(origin, new Color(1f, 0.85f, 0.4f), 5, 3f, 0.18f, 0.18f, 0f); // muzzle flash
            AudioManager.Play(fireSfx, fireSfxVolume);
            if (turret != null)
            {
                if (recoil != null) StopCoroutine(recoil);
                recoil = StartCoroutine(Recoil());
            }
            return origin;
        }

        /// <summary>Remote clients: replay a shot the server fired (visuals only, no damage).</summary>
        public void PlayRemoteFire(Damageable t)
        {
            Vector3 origin = PlayFireFx(t);
            if (projectileSpeed > 0.01f) Projectile.Spawn(projectilePrefab, origin, t, 0f, projectileSpeed, splashRadius, team, null, true);
        }

        // The barrel kicks back along its firing direction and settles again.
        IEnumerator Recoil()
        {
            Vector3 worldBack = -turret.forward * 0.28f;
            Vector3 localBack = turret.parent != null ? turret.parent.InverseTransformDirection(worldBack) : worldBack;
            float t = 0f;
            while (t < 0.2f)
            {
                t += Time.deltaTime;
                turret.localPosition = turretBase + localBack * (1f - Mathf.Clamp01(t / 0.2f));
                yield return null;
            }
            turret.localPosition = turretBase;
            recoil = null;
        }
    }
}
