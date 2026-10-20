using System;
using UnityEngine;

namespace CardBattle
{
    public enum CardType { Unit, Building, Spell }
    public enum Rarity { Common, Rare, Epic, Legendary }

    /// <summary>How a unit picks targets. Structures = buildings and towers.</summary>
    public enum TargetPriority
    {
        Nearest,        // closest enemy of any kind
        UnitsFirst,     // enemy units in range, otherwise structures
        BuildingsOnly   // ignores enemy units entirely (tank / siege style)
    }

    /// <summary>Stats for units and buildings. Applied to the spawned prefab by Unit/Building.Initialize.</summary>
    [Serializable]
    public class CardStats
    {
        public float health = 500f;
        public float damage = 60f;
        [Tooltip("Edge-to-edge reach. ~0.5 for melee, 4-6 for ranged.")]
        public float attackRange = 0.5f;
        [Tooltip("Attacks per second (cooldown = 1 / attackSpeed).")]
        public float attackSpeed = 1f;
        [Tooltip("World units per second.")]
        public float moveSpeed = 2.5f;
        [Tooltip("Enemies inside this distance are considered before marching on the enemy base.")]
        public float detectionRange = 5f;
        public float bodyRadius = 0.5f;
        public TargetPriority targetPriority = TargetPriority.Nearest;
        public bool canTargetAir = false;
        public bool canTargetGround = true;
        [Tooltip("> 0 makes each hit damage an area (magic / splash).")]
        public float splashRadius = 0f;
        [Tooltip("0 = instant hit, otherwise a projectile travels at this speed.")]
        public float projectileSpeed = 0f;
        [Min(1)] public int spawnCount = 1;
        [Tooltip("Buildings only: seconds until the building decays away. 0 = permanent.")]
        public float lifetime = 0f;
    }

    /// <summary>Settings shared by every spell type. Unused fields are simply ignored.</summary>
    [Serializable]
    public class SpellStats
    {
        public float radius = 3f;
        [Tooltip("0 = instant, single application.")]
        public float duration = 0f;
        public float tickInterval = 0.5f;
        [Tooltip("Damage or healing applied on every tick.")]
        public float amount = 200f;
        [Tooltip("Stun spells: how long affected units are frozen in place.")]
        public float stunSeconds = 1.2f;
        [Range(0.05f, 1f)] public float slowMultiplier = 0.5f;
        [Tooltip("Damage spells hit towers for this fraction of their damage.")]
        [Range(0f, 1f)] public float towerDamageMultiplier = 0.35f;
    }

    /// <summary>
    /// A card definition. To add a new card: Create > Card Battle > Card, assign a prefab that has the
    /// matching component (Unit / Building / Spell) and put it in a deck. No code changes needed.
    /// </summary>
    [CreateAssetMenu(fileName = "NewCard", menuName = "Card Battle/Card")]
    public class CardData : ScriptableObject
    {
        [Header("Identity")]
        public string cardName = "New Card";
        [TextArea(2, 4)] public string description;
        public Sprite icon;
        [Tooltip("Used as the placeholder icon colour when no sprite is assigned.")]
        public Color iconColor = new Color(0.4f, 0.6f, 0.9f);
        public CardType cardType = CardType.Unit;
        public Rarity rarity = Rarity.Common;
        [Range(1, 10)] public int cost = 3;
        [Tooltip("Random decks only draw cards with this on. Turn it off for summoned minions / tokens.")]
        public bool inDeckPool = true;

        [Header("Spawned object")]
        [Tooltip("Unit: prefab with a Unit component. Building: prefab with Building/DefensiveBuilding. Spell: prefab with a Spell component.")]
        public GameObject prefab;

        [Header("Unit / Building stats")]
        public CardStats stats = new CardStats();

        [Header("Spell settings")]
        public SpellStats spell = new SpellStats();

        public string DisplayName { get { return string.IsNullOrEmpty(cardName) ? name : cardName; } }
    }
}
