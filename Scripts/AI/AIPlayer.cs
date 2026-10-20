using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum AIDifficulty { Easy, Normal, Hard }

    /// <summary>Tunable behaviour for one difficulty level. Edit these in the Inspector.</summary>
    [Serializable]
    public class AIDifficultySettings
    {
        [Tooltip("Random delay range (seconds) between decisions.")]
        public float minThinkTime = 2f;
        public float maxThinkTime = 4f;
        [Tooltip("Seconds the AI waits at the start before doing anything.")]
        public float openingDelay = 4f;
        [Tooltip("The AI banks at least this much mana before starting an attack.")]
        public float minResourceToAttack = 7f;
        [Range(0f, 1f), Tooltip("Chance to ignore a threat on a given decision.")]
        public float mistakeChance = 0.2f;
        [Tooltip("Random placement offset in world units.")]
        public float placementNoise = 1.5f;
        [Range(0f, 1f)] public float spellUsage = 0.5f;
        [Tooltip("Enemy units that must be clustered before an offensive spell is cast.")]
        public int minSpellTargets = 3;
    }

    /// <summary>
    /// Basic opponent. Every few seconds it "thinks": defend against the biggest threat on its half,
    /// cast a spell on a worthwhile cluster, or build an attack in a lane. It plays through
    /// CardManager.TryDeploy with its own resource pool, so it obeys exactly the same rules as the player.
    /// </summary>
    public class AIPlayer : MonoBehaviour
    {
        const Team Me = Team.Enemy;

        [SerializeField] AIDifficulty difficulty = AIDifficulty.Normal;
        [SerializeField] AIDifficultySettings easy = new AIDifficultySettings
            { minThinkTime = 3f, maxThinkTime = 5.5f, openingDelay = 6f, minResourceToAttack = 9f, mistakeChance = 0.45f, placementNoise = 3f, spellUsage = 0.2f, minSpellTargets = 4 };
        [SerializeField] AIDifficultySettings normal = new AIDifficultySettings
            { minThinkTime = 2f, maxThinkTime = 4f, openingDelay = 4f, minResourceToAttack = 7f, mistakeChance = 0.2f, placementNoise = 1.5f, spellUsage = 0.5f, minSpellTargets = 3 };
        [SerializeField] AIDifficultySettings hard = new AIDifficultySettings
            { minThinkTime = 1f, maxThinkTime = 2.2f, openingDelay = 2f, minResourceToAttack = 5f, mistakeChance = 0.05f, placementNoise = 0.6f, spellUsage = 0.85f, minSpellTargets = 2 };
        [SerializeField] AIDeckManager deck;

        AIDifficultySettings Settings
        {
            get { return difficulty == AIDifficulty.Easy ? easy : (difficulty == AIDifficulty.Hard ? hard : normal); }
        }

        void Start()
        {
            if (NetworkGame.IsOnline) { enabled = false; return; } // online matches are player vs player
            if (deck == null) deck = GetComponent<AIDeckManager>();
            if (deck == null) { Debug.LogWarning("AIPlayer: no AIDeckManager found, AI disabled."); return; }
            StartCoroutine(Brain());
        }

        // The whole AI runs in one coroutine that wakes every couple of seconds (no Update needed).
        IEnumerator Brain()
        {
            yield return new WaitForSeconds(Settings.openingDelay);
            while (true)
            {
                if (MatchManager.IsRunning) Think();
                AIDifficultySettings s = Settings;
                yield return new WaitForSeconds(UnityEngine.Random.Range(s.minThinkTime, s.maxThinkTime));
            }
        }

        void Think()
        {
            ResourceManager res = ResourceManager.Instance;
            if (res == null || CardManager.Instance == null || ArenaLayout.Instance == null) return;
            AIDifficultySettings s = Settings;
            float mana = res.Get(Me);

            Unit threat = FindBiggestThreat();
            if (threat != null && UnityEngine.Random.value > s.mistakeChance && TryDefend(threat, mana)) return;
            if (UnityEngine.Random.value < s.spellUsage && TrySpell(mana)) return;
            if (mana >= s.minResourceToAttack) TryAttack(mana);
        }

        // ---------- Defence ----------

        // Enemy (player) units that have crossed or are about to cross the river, ranked by strength and closeness.
        Unit FindBiggestThreat()
        {
            Unit best = null;
            float bestScore = 0f;
            Vector3 home = ArenaLayout.Instance.TowerSlot(Me, 1);
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i] as Unit;
                if (u == null || u.IsDead || u.Team == Me) continue;
                if (u.transform.position.z < -2f) continue;
                float dist = Vector3.Distance(u.transform.position, home);
                float score = u.Power * Mathf.Max(1f, 40f - dist);
                if (score > bestScore) { bestScore = score; best = u; }
            }
            return best;
        }

        bool TryDefend(Unit threat, float mana)
        {
            CardData best = null;
            float bestScore = float.MinValue;
            IList<CardData> hand = deck.Hand;
            for (int i = 0; i < hand.Count; i++)
            {
                CardData c = hand[i];
                if (c == null || c.cardType == CardType.Spell || c.cost > mana) continue;
                if (threat.IsFlying ? !c.stats.canTargetAir : !c.stats.canTargetGround) continue;
                if (c.stats.targetPriority == TargetPriority.BuildingsOnly) continue; // tanks ignore units
                float score = c.stats.damage * c.stats.attackSpeed * Mathf.Max(1, c.stats.spawnCount)
                            + c.stats.health * 0.05f - c.cost * 8f;
                if (c.cardType == CardType.Building) score += 25f;
                if (score > bestScore) { bestScore = score; best = c; }
            }
            if (best == null) return false;
            Vector3 threatPos = threat.transform.position;
            return DeployWithRetries(best, () => DefensePoint(threatPos, best));
        }

        Vector3 DefensePoint(Vector3 threatPos, CardData card)
        {
            ArenaLayout a = ArenaLayout.Instance;
            float noise = Settings.placementNoise * 0.5f;
            float back = card.stats.attackRange > 3f ? 5f : 3f; // ranged units stand further behind the threat
            float z = Mathf.Clamp(threatPos.z + back, a.RiverHalf + 1.2f, a.HalfLength - 6f);
            float x = Mathf.Clamp(threatPos.x + UnityEngine.Random.Range(-noise, noise), -a.HalfWidth + 1f, a.HalfWidth - 1f);
            return new Vector3(x, 0f, z);
        }

        // ---------- Spells ----------

        bool TrySpell(float mana)
        {
            IList<CardData> hand = deck.Hand;
            AIDifficultySettings s = Settings;
            for (int i = 0; i < hand.Count; i++)
            {
                CardData c = hand[i];
                if (c == null || c.cardType != CardType.Spell || c.cost > mana || c.prefab == null) continue;

                Vector3 center;
                bool found;
                if (c.prefab.GetComponent<HealSpell>() != null)
                    found = FindCluster(Me, c.spell.radius, 2, u => u.HealthPercent < 0.6f, out center);
                else if (c.prefab.GetComponent<SlowSpell>() != null || c.prefab.GetComponent<StunSpell>() != null)
                    found = FindCluster(Team.Player, c.spell.radius, 2, null, out center);
                else
                    found = FindCluster(Team.Player, c.spell.radius, s.minSpellTargets, null, out center);

                if (found && Deploy(c, center)) return true;
            }
            return false;
        }

        // Finds the unit of 'unitsTeam' with the most same-team units within 'radius' around it.
        bool FindCluster(Team unitsTeam, float radius, int minCount, Func<Unit, bool> filter, out Vector3 center)
        {
            center = Vector3.zero;
            var units = new List<Unit>();
            var all = Damageable.All;
            for (int i = 0; i < all.Count; i++)
            {
                Unit u = all[i] as Unit;
                if (u == null || u.IsDead || u.Team != unitsTeam) continue;
                if (filter != null && !filter(u)) continue;
                units.Add(u);
            }
            int bestCount = 0;
            for (int i = 0; i < units.Count; i++)
            {
                int count = 0;
                Vector3 pi = units[i].transform.position;
                for (int j = 0; j < units.Count; j++)
                {
                    Vector3 d = units[j].transform.position - pi;
                    d.y = 0f;
                    if (d.magnitude <= radius) count++;
                }
                if (count > bestCount) { bestCount = count; center = pi; }
            }
            return bestCount >= minCount;
        }

        // ---------- Offence ----------

        bool TryAttack(float mana)
        {
            IList<CardData> hand = deck.Hand;
            var options = new List<CardData>();
            for (int i = 0; i < hand.Count; i++)
            {
                CardData c = hand[i];
                if (c != null && c.cardType == CardType.Unit && c.cost <= mana) options.Add(c);
            }
            if (options.Count == 0) return false;

            CardData pick = options[UnityEngine.Random.Range(0, options.Count)];
            if (difficulty == AIDifficulty.Hard) // hard AI prefers its strongest affordable push
                for (int i = 0; i < options.Count; i++) if (options[i].cost > pick.cost) pick = options[i];

            float laneX = ChooseLaneX();
            return DeployWithRetries(pick, () => AttackPoint(pick, laneX));
        }

        float ChooseLaneX()
        {
            ArenaLayout a = ArenaLayout.Instance;
            if (difficulty == AIDifficulty.Hard)
            {
                // Hard AI pressures the lane whose player side tower is weakest.
                Tower weakest = null;
                foreach (Tower t in Tower.Registry)
                    if (t.Team == Team.Player && !t.IsMainTower && !t.IsDead && (weakest == null || t.Health < weakest.Health)) weakest = t;
                if (weakest != null) return a.NearestBridgeX(weakest.transform.position.x);
            }
            return a.BridgeXs[UnityEngine.Random.Range(0, a.BridgeXs.Count)];
        }

        Vector3 AttackPoint(CardData c, float laneX)
        {
            ArenaLayout a = ArenaLayout.Instance;
            float noise = Settings.placementNoise * 0.5f;
            float depth = c.stats.attackRange > 3f ? 5f : 3f; // ranged units wait further behind the bridge
            float z = Mathf.Min(a.RiverHalf + depth, a.HalfLength - 6f);
            float x = Mathf.Clamp(laneX + UnityEngine.Random.Range(-noise, noise), -a.HalfWidth + 1f, a.HalfWidth - 1f);
            return new Vector3(x, 0f, z);
        }

        // ---------- Deploy helpers ----------

        bool Deploy(CardData card, Vector3 point)
        {
            string message;
            if (CardManager.Instance.TryDeploy(Me, card, point, out message) != DeployResult.Success) return false;
            deck.Played(card);
            return true;
        }

        // A few jittered attempts: failed attempts cost nothing (validation happens before spending).
        bool DeployWithRetries(CardData card, Func<Vector3> pointFactory)
        {
            for (int attempt = 0; attempt < 3; attempt++)
                if (Deploy(card, pointFactory())) return true;
            return false;
        }
    }
}
