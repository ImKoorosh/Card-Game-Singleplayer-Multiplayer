using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum EntityKind { Unit, Building, Tower }

    /// <summary>
    /// Base class for everything that has health and can be attacked (units, buildings, towers).
    /// Keeps a static registry (<see cref="All"/>) so targeting and spells never need physics queries.
    /// </summary>
    public abstract class Damageable : MonoBehaviour
    {
        public static readonly List<Damageable> All = new List<Damageable>(64);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { All.Clear(); }

        [Header("Identity / Health")]
        [SerializeField] protected Team team = Team.Player;
        [SerializeField] protected float maxHealth = 100f;
        [Tooltip("Used for target distance (edge-to-edge) and unit separation.")]
        [SerializeField] protected float bodyRadius = 0.5f;

        [Header("Feedback")]
        [SerializeField] protected float aimHeight = 0.9f;
        [SerializeField] protected float healthBarHeight = 2f;
        [SerializeField] protected float healthBarWidth = 1.2f;

        public Team Team { get { return team; } }
        public float Health { get; private set; }
        public float MaxHealth { get { return maxHealth; } }
        public float BodyRadius { get { return bodyRadius; } }
        public float HealthPercent { get { return maxHealth > 0f ? Health / maxHealth : 0f; } }
        public bool IsDead { get; private set; }
        public Vector3 AimPoint { get { return transform.position + Vector3.up * aimHeight; } }
        public abstract EntityKind Kind { get; }
        public virtual bool IsFlying { get { return false; } }

        public event Action<Damageable> HealthChanged;
        public event Action<Damageable> Died;
        /// <summary>Raised with the amount of health actually lost (used by hit flash, animation, sounds).</summary>
        public event Action<float> Damaged;

        protected virtual void Awake() { Health = maxHealth; }
        protected virtual void OnEnable() { if (!IsDead && !All.Contains(this)) All.Add(this); }
        protected virtual void OnDisable() { All.Remove(this); }

        protected virtual void Start()
        {
            // Health bars are event driven: they only redraw when HealthChanged fires.
            HealthBarUI.Create(this, healthBarHeight, healthBarWidth, Kind == EntityKind.Tower);
            HitFlash.Attach(this);
        }

        /// <summary>Sets max health and refills. Used when stats are applied from a CardData.</summary>
        protected void SetMaxHealth(float value)
        {
            maxHealth = Mathf.Max(1f, value);
            Health = maxHealth;
            if (HealthChanged != null) HealthChanged(this);
        }

        public virtual void TakeDamage(float amount, Damageable source = null)
        {
            if (IsDead || amount <= 0f) return;
            float before = Health;
            Health = Mathf.Max(0f, Health - amount);
            if (HealthChanged != null) HealthChanged(this);
            if (Damaged != null) Damaged(before - Health);
            if (Health <= 0f) Kill();
        }

        public virtual void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Health = Mathf.Min(maxHealth, Health + amount);
            if (HealthChanged != null) HealthChanged(this);
        }

        /// <summary>Distance from a point to the edge of this object's body on the XZ plane.</summary>
        public float EdgeDistance(Vector3 from)
        {
            Vector3 d = transform.position - from;
            d.y = 0f;
            return Mathf.Max(0f, d.magnitude - bodyRadius);
        }

        protected void Kill()
        {
            if (IsDead) return;
            IsDead = true;
            All.Remove(this); // leave the registry immediately so nothing targets a corpse
            if (Died != null) Died(this);
            OnDeath();
        }

        /// <summary>Override to play a death effect. Must eventually destroy the GameObject.</summary>
        protected virtual void OnDeath() { NetworkGame.DestroyEntity(gameObject); }

        /// <summary>
        /// Remote clients only: applies the server's health value. Fires the same events as local damage
        /// (health bar, hit flash, death animation). Never used by the server or offline.
        /// </summary>
        public void ApplyNetworkHealth(float value, bool silent)
        {
            if (IsDead) return;
            float before = Health;
            Health = Mathf.Clamp(value, 0f, maxHealth);
            if (HealthChanged != null) HealthChanged(this);
            if (!silent && Health < before - 0.001f && Damaged != null) Damaged(before - Health);
            if (Health <= 0f) Kill();
        }
    }
}
