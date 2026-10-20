using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// The AI's own deck. Uses the same hand/queue rules as the player, so the AI only ever has
    /// <c>handSize</c> cards to choose from and must wait for cards to rotate back.
    /// With Random Deck on it draws its own random deck from the card pool (every CardData in a Resources/Cards folder).
    /// </summary>
    public class AIDeckManager : MonoBehaviour
    {
        [Tooltip("Used when Random Deck is off (or no cards are found in Resources/Cards).")]
        [SerializeField] List<CardData> deck = new List<CardData>();
        [Tooltip("Cards the AI can choose from at once (rotates as cards are played).")]
        [SerializeField] int handSize = 4;
        [SerializeField] bool randomDeck = true;
        [SerializeField] int deckSize = 8;

        DeckModel model;

        DeckModel Model
        {
            get
            {
                if (model == null) Build();
                return model;
            }
        }

        public IList<CardData> Hand { get { return Model.Hand; } }

        void Awake() { Build(); }

        void Build()
        {
            if (randomDeck)
            {
                List<CardData> pool = CardLibrary.LoadPool();
                if (pool.Count > 0) deck = CardLibrary.RandomDeck(pool, deckSize, 1, 2, 2);
            }
            model = new DeckModel(deck, handSize, true);
        }

        public void Played(CardData card)
        {
            int i = Hand.IndexOf(card);
            if (i >= 0) model.Cycle(i);
        }
    }
}
