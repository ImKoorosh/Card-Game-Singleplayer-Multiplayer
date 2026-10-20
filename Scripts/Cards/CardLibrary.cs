using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// The pool of playable cards. Every CardData stored in any folder named "Resources/Cards" is discovered
    /// automatically at runtime, so cards never have to be dragged into the scene by hand.
    /// Cards with "In Deck Pool" turned off (summoned minions) are skipped.
    /// To add a card to the game: create a CardData and put it in a Resources/Cards folder. That's it.
    /// </summary>
    public static class CardLibrary
    {
        public const string ResourcesFolder = "Cards";

        public static List<CardData> LoadPool()
        {
            var pool = new List<CardData>();
            CardData[] all = Resources.LoadAll<CardData>(ResourcesFolder);
            for (int i = 0; i < all.Length; i++)
            {
                CardData c = all[i];
                if (c != null && c.inDeckPool && c.prefab != null) pool.Add(c);
            }
            return pool;
        }

        /// <summary>
        /// Random deck of distinct cards. Best-effort rules: between minSpells and maxSpells spells, at most maxBuildings
        /// buildings, and at least one card that can hit air units. If the pool is too small to satisfy them, the last draw is used.
        /// </summary>
        public static List<CardData> RandomDeck(IList<CardData> pool, int size, int minSpells, int maxSpells, int maxBuildings)
        {
            var result = new List<CardData>();
            if (pool == null || pool.Count == 0) return result;
            size = Mathf.Clamp(size, 1, pool.Count);

            for (int attempt = 0; attempt < 40; attempt++)
            {
                result = Draw(pool, size);
                if (IsValid(result, minSpells, maxSpells, maxBuildings)) break;
            }
            return result;
        }

        static List<CardData> Draw(IList<CardData> pool, int size)
        {
            var list = new List<CardData>(pool);
            for (int i = list.Count - 1; i > 0; i--) // Fisher-Yates shuffle
            {
                int j = Random.Range(0, i + 1);
                CardData tmp = list[i]; list[i] = list[j]; list[j] = tmp;
            }
            return list.GetRange(0, size);
        }

        static bool IsValid(List<CardData> deck, int minSpells, int maxSpells, int maxBuildings)
        {
            int spells = 0, buildings = 0;
            bool antiAir = false;
            for (int i = 0; i < deck.Count; i++)
            {
                CardData c = deck[i];
                if (c.cardType == CardType.Spell) spells++;
                else
                {
                    if (c.cardType == CardType.Building) buildings++;
                    if (c.stats.canTargetAir) antiAir = true;
                }
            }
            return spells >= minSpells && spells <= maxSpells && buildings <= maxBuildings && antiAir;
        }
    }
}
