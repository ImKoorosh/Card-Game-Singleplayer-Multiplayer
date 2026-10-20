using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Plain C# hand/queue logic shared by the player and AI decks.
    /// handSize == deck size  -> every card is always available (default, 8 cards).
    /// handSize &lt; deck size   -> classic rotation: a played card goes to the back of the queue.
    /// </summary>
    public class DeckModel
    {
        readonly List<CardData> hand = new List<CardData>();
        readonly Queue<CardData> queue = new Queue<CardData>();
        public IList<CardData> Hand { get { return hand; } }
        public CardData NextInQueue { get { return queue.Count > 0 ? queue.Peek() : null; } }

        public DeckModel(IList<CardData> cards, int handSize, bool shuffle)
        {
            var list = new List<CardData>();
            if (cards != null) for (int i = 0; i < cards.Count; i++) if (cards[i] != null) list.Add(cards[i]);
            if (shuffle)
                for (int i = list.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    CardData tmp = list[i]; list[i] = list[j]; list[j] = tmp;
                }
            int size = Mathf.Clamp(handSize, 1, Mathf.Max(1, list.Count));
            for (int i = 0; i < list.Count; i++)
            {
                if (i < size) hand.Add(list[i]); else queue.Enqueue(list[i]);
            }
        }

        public void Cycle(int slot)
        {
            if (slot < 0 || slot >= hand.Count || queue.Count == 0) return;
            CardData played = hand[slot];
            hand[slot] = queue.Dequeue();
            queue.Enqueue(played);
        }
    }

    /// <summary>Player deck: hand contents + which card is currently selected. Attach to the "PlayerControl" object.</summary>
    public class DeckManager : MonoBehaviour
    {
        public static DeckManager Instance { get; private set; }

        [Tooltip("The player's deck. 8 cards recommended.")]
        [SerializeField] List<CardData> deck = new List<CardData>();
        [Tooltip("How many cards are shown at once. 8 = whole deck visible; 4 = rotating hand.")]
        [SerializeField] int handSize = 8;
        [SerializeField] bool shuffleOnStart = false;

        [Header("Random deck")]
        [Tooltip("Draws a fresh random deck every match from ALL cards stored in a Resources/Cards folder (no dragging cards in). Turn off to use the Deck list above.")]
        [SerializeField] bool randomDeck = true;
        [SerializeField] int deckSize = 8;
        [Tooltip("Rules for the random draw (best effort).")]
        [SerializeField] int minSpells = 1;
        [SerializeField] int maxSpells = 2;
        [SerializeField] int maxBuildings = 2;

        DeckModel model;
        int selected = -1;
        readonly List<CardData> networkHand = new List<CardData>();   // online: filled from the server

        public IList<CardData> Hand { get { return NetworkGame.IsOnline ? (IList<CardData>)networkHand : model.Hand; } }
        public CardData NextCard { get { return model != null ? model.NextInQueue : null; } }
        public int SelectedIndex { get { return selected; } }
        public CardData SelectedCard { get { IList<CardData> h = Hand; return (selected >= 0 && selected < h.Count) ? h[selected] : null; } }

        public event Action HandChanged;
        public event Action<int> SelectionChanged;   // -1 = nothing selected

        void Awake()
        {
            Instance = this;
            if (NetworkGame.IsOnline) return; // online: the server deals the deck and sends only the hand (see NetworkPlayer)
            if (randomDeck)
            {
                List<CardData> pool = CardLibrary.LoadPool();
                if (pool.Count > 0) deck = CardLibrary.RandomDeck(pool, deckSize, minSpells, maxSpells, maxBuildings);
                else Debug.LogWarning("DeckManager: Random Deck is on but no cards were found in a Resources/Cards folder. Using the Deck list instead (run Tools > Card Battle > Fix Card Pool).");
            }
            else if (deck.Count != 8) Debug.LogWarning("DeckManager: deck has " + deck.Count + " cards (8 recommended).");
            if (deck.Count == 0) Debug.LogError("DeckManager: the deck is empty. Put CardData assets in a Resources/Cards folder or fill the Deck list.");
            model = new DeckModel(deck, handSize, shuffleOnStart);
        }

        public void Select(int index)
        {
            if (index < 0 || index >= Hand.Count) return;
            selected = index;
            if (SelectionChanged != null) SelectionChanged(selected);
        }

        public void ClearSelection()
        {
            if (selected == -1) return;
            selected = -1;
            if (SelectionChanged != null) SelectionChanged(selected);
        }

        /// <summary>Cycle the selection forward (+1) or backward (-1) through the hand.</summary>
        public void CycleSelection(int direction)
        {
            int count = Hand.Count;
            if (count == 0) return;
            int next = selected < 0 ? (direction > 0 ? 0 : count - 1) : (selected + direction + count) % count;
            Select(next);
        }

        /// <summary>Call after a successful deployment: rotates the hand (if enabled) and clears the selection.</summary>
        public void OnCardPlayed(int slot)
        {
            if (!NetworkGame.IsOnline) model.Cycle(slot); // online the server cycles and sends the new hand
            selected = -1;
            if (HandChanged != null) HandChanged();
            if (SelectionChanged != null) SelectionChanged(-1);
        }

        /// <summary>Online: replaces the hand with the card ids the server sent for this player.</summary>
        public void SetNetworkHand(IList<int> cardIds)
        {
            networkHand.Clear();
            for (int i = 0; i < cardIds.Count; i++)
            {
                CardData c = CardRegistry.Get(cardIds[i]);
                if (c != null) networkHand.Add(c);
            }
            if (selected >= networkHand.Count) selected = -1;
            if (HandChanged != null) HandChanged();
        }
    }
}
