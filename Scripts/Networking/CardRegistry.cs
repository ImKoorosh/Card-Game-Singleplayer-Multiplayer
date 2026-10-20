using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Maps every CardData to a small stable integer so the network only ever sends card ids, never card data.
    /// All CardData assets in a Resources/Cards folder are listed in alphabetical order, so the host and the client
    /// (same build) compute identical ids. <see cref="Hash"/> is compared during connection approval to catch
    /// players running different card sets.
    /// </summary>
    public static class CardRegistry
    {
        static List<CardData> all;
        static Dictionary<CardData, int> ids;
        static uint hash;

        public static IList<CardData> All { get { Load(); return all; } }
        public static uint Hash { get { Load(); return hash; } }

        public static CardData Get(int id)
        {
            Load();
            return (id >= 0 && id < all.Count) ? all[id] : null;
        }

        public static int IdOf(CardData card)
        {
            Load();
            int id;
            return (card != null && ids.TryGetValue(card, out id)) ? id : -1;
        }

        public static void Reload() { all = null; }

        static void Load()
        {
            if (all != null) return;
            all = new List<CardData>(Resources.LoadAll<CardData>(CardLibrary.ResourcesFolder));
            all.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            ids = new Dictionary<CardData, int>();
            uint h = 2166136261u; // FNV-1a over card names + costs + types
            for (int i = 0; i < all.Count; i++)
            {
                ids[all[i]] = i;
                string key = all[i].name + "|" + all[i].cost + "|" + (int)all[i].cardType + ";";
                for (int c = 0; c < key.Length; c++) h = (h ^ key[c]) * 16777619u;
            }
            hash = h;
        }
    }
}
