using System;
using UnityEngine;

namespace CardBattle
{
    public enum DeployResult { Success, MatchNotRunning, InvalidCard, MissingPrefab, NotEnoughResource, InvalidLocation }

    /// <summary>
    /// The single entry point for putting cards onto the battlefield. Player input and the AI both call
    /// <see cref="TryDeploy"/>, so validation and resource spending can never be bypassed.
    /// </summary>
    public class CardManager : MonoBehaviour
    {
        public static CardManager Instance { get; private set; }

        [Tooltip("Optional parent for spawned objects (keeps the Hierarchy tidy).")]
        [SerializeField] Transform spawnParent;

        public event Action<Team, CardData, Vector3> CardDeployed;

        void Awake() { Instance = this; }

        /// <summary>Checks that the card's prefab exists and carries the component its card type needs.</summary>
        public static bool ValidateCard(CardData card, out string error)
        {
            error = null;
            if (card == null) { error = "Card is missing"; return false; }
            if (card.prefab == null) { error = card.DisplayName + " has no prefab"; return false; }
            bool ok;
            switch (card.cardType)
            {
                case CardType.Unit: ok = card.prefab.GetComponent<Unit>() != null; break;
                case CardType.Building: ok = card.prefab.GetComponent<Building>() != null; break;
                default: ok = card.prefab.GetComponent<Spell>() != null; break;
            }
            if (!ok) error = card.DisplayName + " prefab is missing its " + card.cardType + " component";
            return ok;
        }

        public bool CanAfford(Team team, CardData card)
        {
            return card != null && ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(team, card.cost);
        }

        public DeployResult TryDeploy(Team team, CardData card, Vector3 position, out string message)
        {
            message = null;
            if (!MatchManager.IsRunning) { message = "Match is not running"; return DeployResult.MatchNotRunning; }

            string error;
            if (card == null) { message = "No card"; return DeployResult.InvalidCard; }
            if (!ValidateCard(card, out error))
            {
                message = error;
                Debug.LogWarning("CardManager: " + error);
                return card.prefab == null ? DeployResult.MissingPrefab : DeployResult.InvalidCard;
            }

            ArenaLayout arena = ArenaLayout.Instance;
            if (arena == null) { message = "No arena in scene"; return DeployResult.InvalidLocation; }
            position.y = 0f;
            if (!arena.IsValidDeployPoint(team, card, position, out message)) return DeployResult.InvalidLocation;

            // Validation passed: only now spend the resource, so a failed deploy never costs anything.
            if (ResourceManager.Instance == null || !ResourceManager.Instance.TrySpend(team, card.cost))
            {
                message = "Not enough mana";
                return DeployResult.NotEnoughResource;
            }

            switch (card.cardType)
            {
                case CardType.Unit: SpawnUnits(team, card, position, arena); break;
                case CardType.Building: SpawnBuilding(team, card, position); break;
                default: SpawnSpell(team, card, position); break;
            }
            PlayDeployFx(team, card, position);
            if (CardDeployed != null) CardDeployed(team, card, position);
            return DeployResult.Success;
        }

        /// <summary>Spawns a unit card at a position without cost or deploy-zone rules (death splits, barracks, etc.).</summary>
        public void SpawnUnitFree(Team team, CardData card, Vector3 position)
        {
            string error;
            if (ArenaLayout.Instance == null || card == null || card.cardType != CardType.Unit || !ValidateCard(card, out error)) return;
            position.y = 0f;
            SpawnUnits(team, card, position, ArenaLayout.Instance);
        }

        static void PlayDeployFx(Team team, CardData card, Vector3 pos)
        {
            if (card.cardType == CardType.Spell) return; // spells play their own effects
            Vfx.Ring(pos, 1.3f, team.GetColor(), 0.4f);
            Vfx.Dust(pos);
        }

        void SpawnUnits(Team team, CardData card, Vector3 pos, ArenaLayout arena)
        {
            int count = Mathf.Max(1, card.stats.spawnCount);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Vector3.zero;
                if (count > 1)
                {
                    float angle = (Mathf.PI * 2f / count) * i;
                    offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (0.35f + card.stats.bodyRadius);
                }
                Vector3 p = arena.ClampToArena(pos + offset, card.stats.bodyRadius);
                GameObject go = Instantiate(card.prefab, p, Quaternion.identity, spawnParent);
                Unit unit = go.GetComponent<Unit>();
                unit.Initialize(card, team);
                NetworkGame.SpawnEntity(go, card, team);
            }
        }

        void SpawnBuilding(Team team, CardData card, Vector3 pos)
        {
            GameObject go = Instantiate(card.prefab, pos, Quaternion.identity, spawnParent);
            go.GetComponent<Building>().Initialize(card, team);
            NetworkGame.SpawnEntity(go, card, team);
        }

        void SpawnSpell(Team team, CardData card, Vector3 pos)
        {
            GameObject go = Instantiate(card.prefab, pos, Quaternion.identity, spawnParent);
            go.GetComponent<Spell>().Initialize(card, team, pos);
        }
    }
}
