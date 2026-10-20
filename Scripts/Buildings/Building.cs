using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>A placeable structure with health that can decay over time and be destroyed.</summary>
    public class Building : Damageable
    {
        [Tooltip("Model root used for the collapse animation. Falls back to this transform.")]
        [SerializeField] protected Transform visual;

        public CardData Data { get; private set; }
        public override EntityKind Kind { get { return EntityKind.Building; } }

        /// <summary>Called by CardManager right after Instantiate.</summary>
        public virtual void Initialize(CardData card, Team owner)
        {
            ApplyCard(card, owner);
            if (card.stats.lifetime > 0f) StartCoroutine(DecayRoutine(card.stats.lifetime));
        }

        /// <summary>Remote clients: visuals and stats only. Decay, attacks and spawning run on the server.</summary>
        public virtual void InitializeRemote(CardData card, Team owner) { ApplyCard(card, owner); }

        void ApplyCard(CardData card, Team owner)
        {
            Data = card;
            team = owner;
            SetMaxHealth(card.stats.health);
            bodyRadius = card.stats.bodyRadius;
            ApplyStats(card.stats);
            TeamUtil.Tint(transform, owner);
            StartCoroutine(PopIn());
        }

        // Buildings spring up out of the ground with a small overshoot.
        IEnumerator PopIn()
        {
            Transform v = visual != null ? visual : transform;
            Vector3 full = v.localScale;
            float t = 0f;
            while (t < 0.4f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.4f);
                v.localScale = new Vector3(full.x, full.y * (Mathf.SmoothStep(0f, 1f, k) + Mathf.Sin(k * Mathf.PI) * 0.2f), full.z);
                yield return null;
            }
            v.localScale = full;
        }

        protected virtual void ApplyStats(CardStats stats) { }

        // Buildings lose health steadily so they always expire after 'lifetime' seconds.
        IEnumerator DecayRoutine(float lifetime)
        {
            var wait = new WaitForSeconds(0.5f);
            float perTick = maxHealth / lifetime * 0.5f;
            while (!IsDead)
            {
                yield return wait;
                if (MatchManager.IsRunning) TakeDamage(perTick);
            }
        }

        protected override void OnDeath()
        {
            Vfx.Burst(transform.position + new Vector3(0f, 1f, 0f), new Color(0.6f, 0.55f, 0.5f), 18, 3.5f, 0.35f, 0.7f, 1.2f);
            Vfx.Dust(transform.position);
            AudioManager.Play(SfxId.Explosion, 0.5f);
            StopAllCoroutines();
            StartCoroutine(CollapseRoutine());
        }

        IEnumerator CollapseRoutine()
        {
            Transform v = visual != null ? visual : transform;
            Vector3 start = v.localScale;
            float t = 0f;
            while (t < 0.5f)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / 0.5f);
                v.localScale = new Vector3(start.x, start.y * (1f - k * 0.95f), start.z);
                yield return null;
            }
            NetworkGame.DestroyEntity(gameObject);
        }
    }
}
