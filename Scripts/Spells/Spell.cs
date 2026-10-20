using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Base for all spells. Applies <see cref="ApplyEffect"/> once (duration 0) or every tickInterval
    /// for the spell's duration, then cleans itself up. To add a new spell: derive from this class,
    /// implement ApplyEffect and create a prefab + CardData.
    /// </summary>
    public abstract class Spell : MonoBehaviour
    {
        [Tooltip("Flat disc that visualises the area. It is scaled to the spell radius.")]
        [SerializeField] protected Transform areaVisual;

        [SerializeField] protected Color fxColor = new Color(1f, 0.5f, 0.2f);

        protected Team team;
        Material areaMat;
        protected SpellStats stats;
        bool cosmetic;   // remote-client copy: effects only, no gameplay
        protected Vector3 center;

        public void Initialize(CardData card, Team owner, Vector3 position, bool cosmeticOnly = false)
        {
            cosmetic = cosmeticOnly;
            if (!cosmetic) NetFx.SpellCast(card, owner, position); // server: let remote clients show it
            team = owner;
            stats = card.spell;
            center = position;
            transform.position = position;
            if (areaVisual != null)
            {
                areaVisual.position = position + Vector3.up * 0.06f;
                areaVisual.localScale = new Vector3(stats.radius * 2f, 0.02f, stats.radius * 2f);
            }
            CastFx();
            StartCoroutine(AreaAnimation());
            StartCoroutine(Run());
        }

        /// <summary>Sound / particles played once when the spell lands.</summary>
        protected virtual void CastFx() { }
        /// <summary>Visual-only effect played on every tick, before ApplyEffect.</summary>
        protected virtual void TickFx() { }

        protected Vector3 RandomPointInArea()
        {
            float a = Random.value * 6.283f;
            float r = Mathf.Sqrt(Random.value) * stats.radius;
            return center + new Vector3(Mathf.Cos(a) * r, 0.2f, Mathf.Sin(a) * r);
        }

        // The area disc pops open, gently pulses while the spell is active and fades away at the end.
        IEnumerator AreaAnimation()
        {
            if (areaVisual == null) yield break;
            Renderer r = areaVisual.GetComponent<Renderer>();
            if (r != null) areaMat = r.material;
            Color baseColor = areaMat != null ? areaMat.color : Color.white;
            float total = Mathf.Max(0.4f, stats.duration) + 0.4f;
            float diameter = stats.radius * 2f;
            float t = 0f;
            while (t < total)
            {
                t += Time.deltaTime;
                float grow = Mathf.Clamp01(t / 0.25f);
                float d = diameter * (0.2f + 0.8f * (1f - (1f - grow) * (1f - grow))) * (1f + Mathf.Sin(t * 6f) * 0.02f);
                areaVisual.localScale = new Vector3(d, 0.02f, d);
                if (areaMat != null)
                {
                    Color c = baseColor;
                    c.a = baseColor.a * (t > total - 0.4f ? Mathf.Clamp01((total - t) / 0.4f) : 1f);
                    areaMat.color = c;
                }
                yield return null;
            }
        }

        void OnDestroy() { if (areaMat != null) Destroy(areaMat); }

        IEnumerator Run()
        {
            float interval = Mathf.Max(0.05f, stats.tickInterval);
            int ticks = stats.duration <= 0f ? 1 : Mathf.Max(1, Mathf.RoundToInt(stats.duration / interval));
            var wait = new WaitForSeconds(interval);
            for (int i = 0; i < ticks; i++)
            {
                if (MatchManager.IsRunning) { TickFx(); if (!cosmetic) ApplyEffect(); }
                if (i < ticks - 1) yield return wait;
            }
            yield return new WaitForSeconds(0.4f); // let the visual linger briefly
            Destroy(gameObject);
        }

        protected abstract void ApplyEffect();

        /// <summary>Living damageables inside the spell radius that pass the filter.</summary>
        protected List<Damageable> Targets(System.Func<Damageable, bool> filter)
        {
            return CombatUtil.Collect(center, stats.radius, filter);
        }
    }
}
