using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Finds the 2D illustration for a card. Priority:
    ///  1. CardData.icon (assign your own sprite in the Inspector to override the art)
    ///  2. a PNG named exactly like the CardData asset (e.g. "Card_Vanguard") in any Resources/CardArt folder.
    /// Option 2 needs no setup: the art is picked up automatically, whatever the texture's import type is.
    /// </summary>
    public static class CardArt
    {
        public const string ResourcesFolder = "CardArt";
        static readonly Dictionary<string, Sprite> cache = new Dictionary<string, Sprite>();

        public static Sprite Get(CardData card)
        {
            if (card == null) return null;
            if (card.icon != null) return card.icon;

            Sprite sprite;
            if (cache.TryGetValue(card.name, out sprite) && sprite != null) return sprite;

            Texture2D tex = Resources.Load<Texture2D>(ResourcesFolder + "/" + card.name);
            if (tex == null) return null; // no art: the card falls back to a coloured tile with the first letter
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
            cache[card.name] = sprite;
            return sprite;
        }
    }
}
