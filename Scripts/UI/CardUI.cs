using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>
    /// One card slot in the hand. Builds its own visuals (frame, icon, name, cost badge, key hint) and raises
    /// simple events for tap-to-select and drag-to-deploy. Works with mouse and touch through the EventSystem.
    /// </summary>
    public class CardUI : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public event Action<CardUI> Clicked;
        public event Action<CardUI> DragStarted;
        public event Action<CardUI, Vector2> DragMoved;
        public event Action<CardUI, Vector2> Dropped;

        public int Slot { get; private set; }
        public CardData Card { get; private set; }
        public RectTransform RectTransform { get { return (RectTransform)transform; } }

        Image frame, highlight, iconImage, artImage;
        Text initialText, nameText, costText;
        CanvasGroup group;
        Coroutine scaleRoutine;
        float targetScale = 1f;

        public static CardUI Create(Transform parent, int slot)
        {
            RectTransform rt = UIFactory.MakeRect("Card_" + (slot + 1), parent);
            CardUI ui = rt.gameObject.AddComponent<CardUI>();
            ui.Slot = slot;
            ui.group = rt.gameObject.AddComponent<CanvasGroup>();
            ui.frame = rt.gameObject.AddComponent<Image>(); // the frame is the raycast target for the whole card

            ui.highlight = UIFactory.MakeImage("Highlight", rt, new Color(1f, 0.9f, 0.2f));
            UIFactory.Stretch(ui.highlight.rectTransform, -8f, -8f, -8f, -8f);
            ui.highlight.gameObject.SetActive(false);

            Image inner = UIFactory.MakeImage("Inner", rt, new Color(0.13f, 0.15f, 0.22f));
            UIFactory.Stretch(inner.rectTransform, 5f, 5f, 5f, 5f);

            ui.iconImage = UIFactory.MakeImage("Icon", inner.transform, Color.gray);
            ui.iconImage.rectTransform.anchorMin = new Vector2(0.05f, 0.31f);
            ui.iconImage.rectTransform.anchorMax = new Vector2(0.95f, 0.97f);
            ui.iconImage.rectTransform.offsetMin = ui.iconImage.rectTransform.offsetMax = Vector2.zero;
            ui.initialText = UIFactory.MakeText("Initial", ui.iconImage.transform, "", 64, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Stretch(ui.initialText.rectTransform, 0f, 0f, 0f, 0f);

            // The character illustration sits on top of the coloured backdrop and keeps its aspect ratio.
            ui.artImage = UIFactory.MakeImage("Art", ui.iconImage.transform, Color.white);
            UIFactory.Stretch(ui.artImage.rectTransform, 2f, 2f, 2f, 2f);
            ui.artImage.preserveAspect = true;
            ui.artImage.enabled = false;

            ui.nameText = UIFactory.MakeText("Name", inner.transform, "", 26, TextAnchor.MiddleCenter, Color.white);
            ui.nameText.resizeTextForBestFit = true;
            ui.nameText.resizeTextMinSize = 14;
            ui.nameText.resizeTextMaxSize = 28;
            ui.nameText.rectTransform.anchorMin = new Vector2(0f, 0f);
            ui.nameText.rectTransform.anchorMax = new Vector2(1f, 0.32f);
            ui.nameText.rectTransform.offsetMin = new Vector2(4f, 2f);
            ui.nameText.rectTransform.offsetMax = new Vector2(-4f, 0f);

            Image badge = UIFactory.MakeImage("CostBadge", rt, new Color(0.62f, 0.25f, 0.85f));
            UIFactory.Place(badge.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0.3f, 0.7f), new Vector2(8f, -8f), new Vector2(62f, 62f));
            ui.costText = UIFactory.MakeText("Cost", badge.transform, "0", 40, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(ui.costText.rectTransform, 0f, 0f, 0f, 0f);

            Text key = UIFactory.MakeText("Key", inner.transform, (slot + 1).ToString(), 22, TextAnchor.UpperRight, new Color(1f, 1f, 1f, 0.5f));
            UIFactory.Stretch(key.rectTransform, 0f, 0f, 8f, 4f);
            return ui;
        }

        public void SetCard(CardData card)
        {
            Card = card;
            if (card == null) return;
            nameText.text = card.DisplayName;
            costText.text = card.cost.ToString();
            frame.color = RarityColor(card.rarity);
            Sprite art = CardArt.Get(card); // CardData.icon, or the PNG from Resources/CardArt named like the card
            artImage.sprite = art;
            artImage.enabled = art != null;
            iconImage.color = art != null ? Color.Lerp(card.iconColor, Color.black, 0.45f) : card.iconColor; // backdrop behind the art
            string n = card.DisplayName;
            initialText.text = (art == null && n.Length > 0) ? n.Substring(0, 1).ToUpper() : "";
        }

        public void SetSelected(bool selected)
        {
            highlight.gameObject.SetActive(selected);
            targetScale = selected ? 1.07f : 1f;
            if (!isActiveAndEnabled) { transform.localScale = Vector3.one * targetScale; return; }
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(ScaleTo(targetScale, 0.12f, 1f));
        }

        /// <summary>Quick punch animation (used when the card is played).</summary>
        public void Pop()
        {
            if (!isActiveAndEnabled) return;
            if (scaleRoutine != null) StopCoroutine(scaleRoutine);
            scaleRoutine = StartCoroutine(ScaleTo(1f, 0.3f, 1.25f));
        }

        // Eases from 'startMultiplier * current' (or the punch peak) to the target scale.
        IEnumerator ScaleTo(float target, float seconds, float punch)
        {
            float start = punch > 1f ? punch : transform.localScale.x;
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / seconds);
                transform.localScale = Vector3.one * Mathf.Lerp(start, target, k * k * (3f - 2f * k));
                yield return null;
            }
            transform.localScale = Vector3.one * target;
            scaleRoutine = null;
        }

        /// <summary>Dims the card and turns its cost red when it cannot be afforded.</summary>
        public void SetAffordable(bool affordable)
        {
            group.alpha = affordable ? 1f : 0.45f;
            costText.color = affordable ? Color.white : new Color(1f, 0.55f, 0.55f);
        }

        static Color RarityColor(Rarity r)
        {
            switch (r)
            {
                case Rarity.Rare: return new Color(0.95f, 0.6f, 0.2f);
                case Rarity.Epic: return new Color(0.7f, 0.35f, 0.95f);
                case Rarity.Legendary: return new Color(1f, 0.85f, 0.3f);
                default: return new Color(0.55f, 0.65f, 0.8f);
            }
        }

        public void OnPointerClick(PointerEventData e) { if (Clicked != null) Clicked(this); }
        public void OnBeginDrag(PointerEventData e) { if (DragStarted != null) DragStarted(this); }
        public void OnDrag(PointerEventData e) { if (DragMoved != null) DragMoved(this, e.position); }
        public void OnEndDrag(PointerEventData e) { if (Dropped != null) Dropped(this, e.position); }
    }
}
