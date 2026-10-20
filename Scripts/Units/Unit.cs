using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Reusable troop base: stats, target detection/selection, movement along the arena path, cooldown-gated
    /// attacks, slow / stun / buff effects, ability hooks, separation and death. Subclasses only decide HOW an
    /// attack lands (<see cref="PerformAttack"/>): MeleeUnit, RangedUnit, FlyingUnit.
    /// Special behaviour comes from UnitAbility components on the prefab (charge, shield, aura, chain lightning...).
    ///
    /// Prefab layout: root (this component, never rotated) -> "Visual" child (rotated toward movement, holds
    /// the UnitAnimator). Keeping the root unrotated keeps the world-space health bar upright.
    /// </summary>
    public abstract class Unit : Damageable
    {
        [Header("References")]
        [Tooltip("Child transform holding the model. It is rotated to face movement and scaled during spawn/death.")]
        [SerializeField] protected Transform visual;

        [Header("Stats (overwritten from CardData when spawned by a card)")]
        [SerializeField] protected float damage = 50f;
        [SerializeField] protected float attackRange = 0.5f;
        [Tooltip("Attacks per second.")]
        [SerializeField] protected float attackSpeed = 1f;
        [SerializeField] protected float moveSpeed = 2.5f;
        [SerializeField] protected float detectionRange = 5f;
        [SerializeField] protected TargetPriority targetPriority = TargetPriority.Nearest;
        [SerializeField] protected bool canTargetAir = false;
        [SerializeField] protected bool canTargetGround = true;
        [SerializeField] protected float splashRadius = 0f;
        [SerializeField] protected float projectileSpeed = 0f;

        [Header("Audio")]
        [SerializeField] SfxId attackSfx = SfxId.SwordHit;
        [SerializeField, Range(0f, 1f)] float attackSfxVolume = 0.8f;

        [Header("Behaviour")]
        [SerializeField] float turnSpeed = 720f;
        [Tooltip("Seconds after deployment before the unit starts acting.")]
        [SerializeField] float spawnDelay = 0.6f;
        [Tooltip("How often the unit re-evaluates its target (seconds).")]
        [SerializeField] float retargetInterval = 0.25f;

        protected Damageable target;
        protected float attackDamage; // damage of the attack currently being delivered (after buffs/abilities)

        float nextAttackTime, activeAt;
        float slowMultiplier = 1f, slowUntil, stunUntil;
        float buffSpeed = 1f, buffDamage = 1f, buffUntil;
        float stuckTimer;
        Vector3 lastCheckPos;
        bool wantsToMove, begun;
        bool hasAuthority = true;      // false on a remote client: this copy only displays what the server simulates
        NetworkEntity netEntity;
        UnitAbility[] abilities = new UnitAbility[0];
        UnitAnimator unitAnimator;

        public CardData Data { get; private set; }
        public override EntityKind Kind { get { return EntityKind.Unit; } }
        public float Damage { get { return damage; } }
        public float AttackCooldown { get { return 1f / Mathf.Max(0.05f, attackSpeed); } }
        /// <summary>Rough combat value used by the AI to rank threats.</summary>
        public float Power { get { return Health * 0.4f + damage * attackSpeed * 4f; } }
        /// <summary>Remote clients: set from the replicated stun flag (the server owns the real timer).</summary>
        public bool NetStunned { get; set; }
        public bool IsStunned { get { return hasAuthority ? Time.time < stunUntil : NetStunned; } }
        public Transform VisualRoot { get { return visual != null ? visual : transform; } }
        public bool CanTarget(Damageable d) { return d != null && (d.IsFlying ? canTargetAir : canTargetGround); }
        public event System.Action<Damageable> AttackPerformed;

        float SlowFactor { get { return Time.time < slowUntil ? slowMultiplier : 1f; } }
        float BuffSpeed { get { return Time.time < buffUntil ? buffSpeed : 1f; } }
        float BuffDamage { get { return Time.time < buffUntil ? buffDamage : 1f; } }

        public bool IsAbilityActive
        {
            get
            {
                for (int i = 0; i < abilities.Length; i++) if (abilities[i].IsActive) return true;
                return false;
            }
        }

        float AbilitySpeed
        {
            get
            {
                float m = 1f;
                for (int i = 0; i < abilities.Length; i++) m *= abilities[i].SpeedMultiplier;
                return m;
            }
        }

        /// <summary>Called by CardManager right after Instantiate.</summary>
        public virtual void Initialize(CardData card, Team owner)
        {
            ApplyCard(card, owner);
            BeginLife();
        }

        /// <summary>
        /// Remote clients: same stats, tint, spawn animation and animator, but NO targeting, movement, abilities or damage.
        /// Position comes from NetworkTransform, health from NetworkEntity.
        /// </summary>
        public virtual void InitializeRemote(CardData card, Team owner)
        {
            hasAuthority = false;
            ApplyCard(card, owner);
            begun = true;
            TeamUtil.Tint(transform, team);
            VisualRoot.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, team.ForwardZ()));
            unitAnimator = GetComponentInChildren<UnitAnimator>();
            StartCoroutine(SpawnAnimation());
        }

        void ApplyCard(CardData card, Team owner)
        {
            Data = card;
            team = owner;
            CardStats s = card.stats;
            SetMaxHealth(s.health);
            bodyRadius = s.bodyRadius;
            damage = s.damage;
            attackRange = s.attackRange;
            attackSpeed = s.attackSpeed;
            moveSpeed = s.moveSpeed;
            detectionRange = s.detectionRange;
            targetPriority = s.targetPriority;
            canTargetAir = s.canTargetAir;
            canTargetGround = s.canTargetGround;
            splashRadius = s.splashRadius;
            projectileSpeed = s.projectileSpeed;
        }

        protected override void Start()
        {
            base.Start();
            if (!begun) BeginLife(); // unit placed in the scene by hand
        }

        void BeginLife()
        {
            if (begun) return;
            begun = true;
            hasAuthority = NetworkGame.Authority;
            netEntity = GetComponent<NetworkEntity>();
            TeamUtil.Tint(transform, team);
            activeAt = Time.time + spawnDelay;
            nextAttackTime = activeAt;
            lastCheckPos = transform.position;
            VisualRoot.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, team.ForwardZ()));

            unitAnimator = GetComponentInChildren<UnitAnimator>();
            abilities = GetComponents<UnitAbility>();
            for (int i = 0; i < abilities.Length; i++) abilities[i].Bind(this);

            StartCoroutine(SpawnAnimation());
            StartCoroutine(TargetingRoutine());
        }

        // ---------- Targeting (coroutine, not per-frame) ----------

        IEnumerator TargetingRoutine()
        {
            yield return new WaitForSeconds(Random.Range(0f, retargetInterval)); // de-synchronise units
            var wait = new WaitForSeconds(retargetInterval);
            while (!IsDead)
            {
                if (MatchManager.IsRunning && Time.time >= activeAt)
                {
                    AcquireTarget();
                    WatchForStuck();
                }
                yield return wait;
            }
        }

        protected void AcquireTarget()
        {
            // Stay on the current target while it is alive and in reach: avoids flip-flopping mid-fight.
            if (target != null && !target.IsDead && target.EdgeDistance(transform.position) <= attackRange) return;
            target = TargetFinder.Find(team, transform.position, detectionRange, canTargetAir, canTargetGround, targetPriority, true);
        }

        // If a unit wanted to move for ~1.5s but barely did, nudge it sideways and re-pick a target.
        void WatchForStuck()
        {
            bool moved = (transform.position - lastCheckPos).sqrMagnitude > 0.01f;
            if (wantsToMove && !moved && !IsStunned) stuckTimer += retargetInterval; else stuckTimer = 0f;
            lastCheckPos = transform.position;
            wantsToMove = false;
            if (stuckTimer > 1.5f)
            {
                stuckTimer = 0f;
                Vector3 side = new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized * 0.6f;
                transform.position = ClampPosition(transform.position + side, transform.position.z);
                target = null;
            }
        }

        // ---------- Per-frame movement / attack ----------

        void Update()
        {
            if (!hasAuthority) return; // remote client: nothing to simulate
            if (IsDead || !MatchManager.IsRunning || Time.time < activeAt) return;
            if (IsStunned) return;

            if (target == null || target.IsDead)
            {
                target = null;
                AcquireTarget();
                if (target == null) return;
            }

            Vector3 pos = transform.position;
            float edge = target.EdgeDistance(pos);
            float prevZ = pos.z;

            if (edge <= attackRange + 0.05f)
            {
                Vector3 to = target.transform.position - pos;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f) FaceDirection(to.normalized);
                TryAttack();
            }
            else
            {
                MoveToward(target.transform.position);
            }

            if (!IsDead) ResolveOverlaps(prevZ);
        }

        void MoveToward(Vector3 dest)
        {
            wantsToMove = true;
            ArenaLayout arena = ArenaLayout.Instance;
            Vector3 waypoint = arena != null ? arena.GetNextWaypoint(transform.position, dest, IsFlying) : dest;
            Vector3 dir = waypoint - transform.position;
            dir.y = 0f;
            float dist = dir.magnitude;
            if (dist < 0.02f) return;
            dir /= dist;
            float step = Mathf.Min(dist, moveSpeed * SlowFactor * BuffSpeed * AbilitySpeed * Time.deltaTime);
            transform.position += dir * step;
            FaceDirection(dir);
            for (int i = 0; i < abilities.Length; i++) abilities[i].OnMoved(step);
        }

        void FaceDirection(Vector3 dir)
        {
            Transform v = VisualRoot;
            Quaternion goal = Quaternion.LookRotation(dir);
            v.rotation = Quaternion.RotateTowards(v.rotation, goal, turnSpeed * Time.deltaTime);
        }

        /// <summary>
        /// Soft collision: push out of overlapping bodies (same layer: ground vs ground, air vs air),
        /// then clamp to the arena/bridges. Push speed is capped so nothing ever "teleports".
        /// </summary>
        void ResolveOverlaps(float previousZ)
        {
            Vector3 p = transform.position;
            var all = All;
            float maxPush = 6f * Time.deltaTime;
            for (int i = 0; i < all.Count; i++)
            {
                Damageable o = all[i];
                if (o == this || o == null || o.IsFlying != IsFlying) continue;
                Vector3 d = p - o.transform.position;
                d.y = 0f;
                float min = bodyRadius + o.BodyRadius;
                float sq = d.sqrMagnitude;
                if (sq >= min * min) continue;
                float dist = Mathf.Sqrt(sq);
                Vector3 dir = dist > 0.001f ? d / dist : new Vector3(Random.value - 0.5f, 0f, Random.value - 0.5f).normalized;
                float overlap = min - dist;
                // Structures never move, so we take the whole push; against units both share it by size.
                float share = o.Kind == EntityKind.Unit ? o.BodyRadius / (bodyRadius + o.BodyRadius) : 1f;
                p += dir * Mathf.Min(overlap * share, maxPush);
            }
            transform.position = ClampPosition(p, previousZ);
        }

        Vector3 ClampPosition(Vector3 p, float previousZ)
        {
            ArenaLayout arena = ArenaLayout.Instance;
            if (arena == null) return p;
            float y = p.y;
            Vector3 c = IsFlying ? arena.ClampToArena(p, bodyRadius) : arena.ConstrainGround(p, bodyRadius, previousZ);
            c.y = y;
            return c;
        }

        // ---------- Attacking ----------

        void TryAttack()
        {
            // The timestamp is written BEFORE the attack resolves, so an attack can never fire twice per cooldown.
            if (Time.time < nextAttackTime) return;
            Damageable t = target;
            if (t == null || t.IsDead) return;

            nextAttackTime = Time.time + AttackCooldown / (SlowFactor * BuffSpeed);
            attackDamage = CurrentDamage(t);
            PerformAttack(t);
            AudioManager.Play(attackSfx, attackSfxVolume);
            if (AttackPerformed != null) AttackPerformed(t);
            if (netEntity != null) netEntity.BroadcastAttack(t); // cosmetic copy for remote clients
            for (int i = 0; i < abilities.Length; i++) abilities[i].OnAttack(t, attackDamage);
            if (!IsDead && unitAnimator == null) StartCoroutine(AttackPunch()); // fallback lunge for un-rigged prefabs
        }

        float CurrentDamage(Damageable t)
        {
            float d = damage * BuffDamage;
            for (int i = 0; i < abilities.Length; i++) d = abilities[i].ModifyOutgoingDamage(t, d);
            return d;
        }

        /// <summary>Remote clients: replay an attack the server performed (animation, sound, projectile visual). No damage.</summary>
        public void PlayRemoteAttack(Damageable t)
        {
            if (IsDead) return;
            AudioManager.Play(attackSfx, attackSfxVolume);
            if (AttackPerformed != null) AttackPerformed(t);
            PlayRemoteAttackFx(t);
        }

        protected virtual void PlayRemoteAttackFx(Damageable t) { }

        /// <summary>Deliver one attack to the target (instant damage, projectile, ...). Use <see cref="attackDamage"/>.</summary>
        protected abstract void PerformAttack(Damageable t);

        /// <summary>Instant damage, optionally in an area around the target.</summary>
        protected void DealDirectDamage(Damageable t)
        {
            if (splashRadius > 0.01f)
                CombatUtil.DamageArea(t.transform.position, splashRadius, team, attackDamage, this, canTargetAir, canTargetGround);
            else
                t.TakeDamage(attackDamage, this);
        }

        public override void TakeDamage(float amount, Damageable source = null)
        {
            if (IsDead) return;
            for (int i = 0; i < abilities.Length; i++) amount = abilities[i].ModifyIncomingDamage(amount);
            base.TakeDamage(amount, source);
        }

        // ---------- Status effects ----------

        /// <summary>Slows movement AND attack speed. The strongest active slow wins; call again to refresh.</summary>
        public void ApplySlow(float multiplier, float seconds)
        {
            multiplier = Mathf.Clamp(multiplier, 0.05f, 1f);
            slowMultiplier = Time.time < slowUntil ? Mathf.Min(slowMultiplier, multiplier) : multiplier;
            slowUntil = Mathf.Max(slowUntil, Time.time + seconds);
        }

        /// <summary>The unit can neither move nor attack until the stun ends.</summary>
        public void ApplyStun(float seconds)
        {
            if (IsDead || seconds <= 0f) return;
            stunUntil = Mathf.Max(stunUntil, Time.time + seconds);
            nextAttackTime = Mathf.Max(nextAttackTime, stunUntil);
            if (netEntity != null) netEntity.ServerStun(seconds); // so remote clients can wobble the head
            AudioManager.Play(SfxId.Stun, 0.5f);
            Vfx.Sparks(AimPoint + new Vector3(0f, 0.6f, 0f), new Color(1f, 0.9f, 0.3f));
        }

        /// <summary>Temporary speed / damage boost (used by aura abilities). Also speeds up attacks.</summary>
        public void ApplyBuff(float speedMultiplier, float damageMultiplier, float seconds)
        {
            buffSpeed = speedMultiplier;
            buffDamage = damageMultiplier;
            buffUntil = Mathf.Max(buffUntil, Time.time + seconds);
        }

        /// <summary>Kills this unit immediately (suicide attackers).</summary>
        public void SelfDestruct() { Kill(); }

        // ---------- Feedback / death ----------

        IEnumerator SpawnAnimation()
        {
            Transform v = VisualRoot;
            Vector3 full = v.localScale;
            Vfx.Dust(new Vector3(transform.position.x, 0f, transform.position.z)); // on the ground even for flyers
            AudioManager.Play(SfxId.Spawn, 0.35f);
            float t = 0f;
            while (t < spawnDelay && !IsDead)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / Mathf.Max(0.01f, spawnDelay));
                v.localScale = full * (Mathf.SmoothStep(0f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.15f); // pop with a little overshoot
                yield return null;
            }
            v.localScale = full;
        }

        IEnumerator AttackPunch()
        {
            if (visual == null) yield break;
            Vector3 basePos = visual.localPosition;
            visual.position += visual.forward * 0.25f;
            yield return new WaitForSeconds(0.08f);
            if (visual != null) visual.localPosition = new Vector3(basePos.x, visual.localPosition.y, basePos.z);
        }

        protected override void OnDeath()
        {
            for (int i = 0; i < abilities.Length; i++) abilities[i].OnOwnerDeath();
            StopAllCoroutines();
            AudioManager.Play(SfxId.UnitDeath, 0.6f);
            Vfx.Burst(transform.position + new Vector3(0f, 0.8f, 0f), team.GetColor(), 10, 3f, 0.25f, 0.5f, 0.8f);
            StartCoroutine(DeathRoutine());
        }

        IEnumerator DeathRoutine()
        {
            Transform v = VisualRoot;
            if (unitAnimator != null)
            {
                yield return unitAnimator.PlayDeath(v);
            }
            else
            {
                Vector3 start = v.localScale;
                float t = 0f;
                while (t < 0.25f)
                {
                    t += Time.deltaTime;
                    v.localScale = start * (1f - Mathf.Clamp01(t / 0.25f));
                    yield return null;
                }
            }
            NetworkGame.DestroyEntity(gameObject); // offline: Destroy, server: Despawn, remote client: wait for the despawn
        }
    }
}
