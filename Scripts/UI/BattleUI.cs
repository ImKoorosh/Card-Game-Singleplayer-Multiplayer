using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>
    /// Builds and drives the whole battle HUD from code: tower health for both sides, match timer,
    /// mana bar, the card hand (tap or drag), selected-card hint, toast messages and the victory/defeat screen.
    /// Everything is event driven; there is no Update loop.
    /// Attach to an empty "BattleUI" GameObject. No fields need to be assigned.
    /// </summary>
    public class BattleUI : MonoBehaviour
    {
        public static BattleUI Instance { get; private set; }

        [SerializeField] Vector2 referenceResolution = new Vector2(1080f, 1920f);
        [SerializeField] float cardHeight = 200f;
        [SerializeField] float cardGap = 12f;

        class TowerHud { public Damageable tower; public RectTransform fill; public Text label; public string prefix; }

        Canvas canvas;
        RectTransform safeRoot, tray;
        RectTransform manaFill;
        Text manaText, timerText, hintText, toastText, bannerText, endTitle, endSubtitle;
        GameObject endPanel;
        Transform endBox;
        Text muteLabel;
        readonly List<CardUI> cardUIs = new List<CardUI>();
        readonly List<TowerHud> towerHuds = new List<TowerHud>();
        int lastManaInt = -1, draggingSlot = -1;
        Coroutine toastRoutine, bannerRoutine, timerPunch, manaPunch;
        Vector2 toastBasePos;

        /// <summary>Fraction of the screen height covered by the card tray (the camera frames the arena above it).</summary>
        public float BottomReserveFraction
        {
            get
            {
                if (tray == null || canvas == null) return 0f;
                return Mathf.Clamp01((tray.rect.height * canvas.scaleFactor + Screen.safeArea.y) / Mathf.Max(1f, Screen.height));
            }
        }

        void Awake() { Instance = this; }

        IEnumerator Start()
        {
            yield return null; // let every other system finish Start() (towers, decks) before we read them
            if (NetworkGame.IsOnline) yield return WaitForOnlineState(); // the hand comes from the server
            EnsureEventSystem();
            BuildCanvas();
            BuildTopHud();
            BuildTray();
            BuildEndPanel();
            Subscribe();
            RefreshHand();
            RefreshMana(NetworkGame.LocalTeam, ResourceManager.Instance != null ? ResourceManager.Instance.Get(NetworkGame.LocalTeam) : 0f);
            if (NetworkGame.IsOnline && NetworkPlayer.Local != null) NetworkPlayer.Local.NotifyBattleReady(); // lets the server start the countdown
        }

        // Online: wait (max 8 s) until our hand has arrived so the tray is built with the right number of cards.
        IEnumerator WaitForOnlineState()
        {
            float t = 0f;
            while (t < 8f)
            {
                if (NetworkPlayer.Local != null)
                {
                    NetworkPlayer.Local.PushStateToUI();
                    if (DeckManager.Instance != null && DeckManager.Instance.Hand.Count > 0) yield break;
                }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // ---------- Construction ----------

        static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        void BuildCanvas()
        {
            GameObject go = new GameObject("BattleCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;

            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = referenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            // Keep the HUD inside notches / rounded corners.
            safeRoot = UIFactory.MakeRect("SafeArea", go.transform);
            Rect sa = Screen.safeArea;
            safeRoot.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        void BuildTopHud()
        {
            RectTransform top = UIFactory.MakeRect("TopHud", safeRoot);
            UIFactory.Place(top, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), Vector2.zero, new Vector2(0f, 130f));

            Image timerBg = UIFactory.MakeImage("TimerBg", top, new Color(0f, 0f, 0f, 0.55f));
            UIFactory.Place(timerBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(220f, 84f));
            timerText = UIFactory.MakeText("Timer", timerBg.transform, "3:00", 52, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(timerText.rectTransform, 0f, 0f, 0f, 0f);

            bannerText = UIFactory.MakeText("Banner", top, "", 32, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.3f));
            UIFactory.Place(bannerText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(500f, 40f));

            // Sound on/off toggle.
            Image muteBg = UIFactory.MakeImage("MuteButton", top, new Color(0f, 0f, 0f, 0.5f));
            muteBg.raycastTarget = true;
            UIFactory.Place(muteBg.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(190f, 40f));
            Button muteButton = muteBg.gameObject.AddComponent<Button>();
            muteButton.targetGraphic = muteBg;
            muteLabel = UIFactory.MakeText("Label", muteBg.transform, AudioManager.Muted ? "SOUND OFF" : "SOUND ON", 22, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(muteLabel.rectTransform, 0f, 0f, 0f, 0f);
            muteButton.onClick.AddListener(() =>
            {
                AudioManager.ToggleMute();
                muteLabel.text = AudioManager.Muted ? "SOUND OFF" : "SOUND ON";
                AudioManager.Play(SfxId.UiClick);
            });

            MatchManager mm = MatchManager.Instance;
            if (mm == null) return;
            BuildTowerGroup(top, "ENEMY TOWERS", mm.OpponentTowers, true);
            BuildTowerGroup(top, "YOUR TOWERS", mm.OwnTowers, false);
        }

        void BuildTowerGroup(RectTransform top, string title, IList<Tower> towers, bool left)
        {
            Image panel = UIFactory.MakeImage(title, top, new Color(0f, 0f, 0f, 0.45f));
            Vector2 anchor = new Vector2(left ? 0f : 1f, 1f);
            UIFactory.Place(panel.rectTransform, anchor, anchor, anchor, new Vector2(left ? 10f : -10f, -10f), new Vector2(300f, 118f));
            Color teamColor = (left ? NetworkGame.OpponentTeam : NetworkGame.LocalTeam).GetColor();

            Text head = UIFactory.MakeText("Title", panel.transform, title, 20, TextAnchor.MiddleCenter, teamColor);
            UIFactory.Place(head.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(0f, 26f));

            for (int i = 0; i < towers.Count; i++)
            {
                Tower t = towers[i];
                Image rowBg = UIFactory.MakeImage("Row", panel.transform, new Color(0.12f, 0.12f, 0.16f, 0.9f));
                UIFactory.Place(rowBg.rectTransform, new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -30f - i * 28f), new Vector2(-20f, 24f));
                Image fill = UIFactory.MakeImage("Fill", rowBg.transform, teamColor);
                Text label = UIFactory.MakeText("Label", rowBg.transform, "", 18, TextAnchor.MiddleCenter, Color.white);
                UIFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);

                TowerHud hud = new TowerHud
                {
                    tower = t, fill = fill.rectTransform, label = label,
                    prefix = t.IsMainTower ? "MAIN" : (t.transform.position.x * NetworkGame.ViewSign < 0f ? "LEFT" : "RIGHT")
                };
                towerHuds.Add(hud);
                RefreshTower(hud);
                t.HealthChanged += d => RefreshTower(hud);
            }
        }

        void BuildTray()
        {
            DeckManager deck = DeckManager.Instance;
            int count = deck != null ? deck.Hand.Count : 0;
            int rows = count > 4 ? 2 : 1;
            int cols = Mathf.Max(1, Mathf.CeilToInt(count / (float)rows));
            float pad = 14f, manaH = 64f, hintH = 76f;
            float cardsH = rows * cardHeight + (rows - 1) * cardGap;
            float manaY = pad + cardsH + 10f;
            float hintY = manaY + manaH + 6f;
            float trayH = hintY + hintH + 6f;

            Image trayBg = UIFactory.MakeImage("Tray", safeRoot, new Color(0.07f, 0.08f, 0.12f, 1f));
            tray = trayBg.rectTransform;
            UIFactory.Place(tray, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(0f, trayH));

            hintText = UIFactory.MakeText("Hint", tray, "Select a card", 22, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Place(hintText.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, hintY), new Vector2(0f, hintH));

            // Mana bar with 10 segments.
            Image manaBg = UIFactory.MakeImage("ManaBar", tray, new Color(0.15f, 0.08f, 0.22f));
            UIFactory.Place(manaBg.rectTransform, new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, manaY), new Vector2(-40f, manaH));
            manaFill = UIFactory.MakeImage("Fill", manaBg.transform, new Color(0.85f, 0.3f, 0.95f)).rectTransform;
            for (int k = 1; k < 10; k++)
            {
                Image tick = UIFactory.MakeImage("Tick", manaBg.transform, new Color(0.1f, 0.05f, 0.15f, 0.7f));
                UIFactory.Place(tick.rectTransform, new Vector2(k / 10f, 0f), new Vector2(k / 10f, 1f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(3f, 0f));
            }
            manaText = UIFactory.MakeText("ManaText", manaBg.transform, "MANA", 34, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(manaText.rectTransform, 0f, 0f, 0f, 0f);

            // Toast just above the tray.
            toastText = UIFactory.MakeText("Toast", safeRoot, "", 38, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
            UIFactory.Place(toastText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, trayH + 24f), new Vector2(900f, 60f));
            toastBasePos = toastText.rectTransform.anchoredPosition;
            toastText.gameObject.SetActive(false);

            // Card grid (reading order, top row first).
            for (int i = 0; i < count; i++)
            {
                CardUI c = CardUI.Create(tray, i);
                int r = i / cols, col = i % cols;
                float bottom = pad + (rows - 1 - r) * (cardHeight + cardGap);
                RectTransform rt = c.RectTransform;
                rt.anchorMin = new Vector2(col / (float)cols, 0f);
                rt.anchorMax = new Vector2((col + 1) / (float)cols, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.offsetMin = new Vector2(cardGap * 0.5f, bottom);
                rt.offsetMax = new Vector2(-cardGap * 0.5f, bottom + cardHeight);
                c.Clicked += OnCardClicked;
                c.DragStarted += OnDragStarted;
                c.DragMoved += OnDragMoved;
                c.Dropped += OnDropped;
                cardUIs.Add(c);
            }
        }

        void BuildEndPanel()
        {
            Image dim = UIFactory.MakeImage("EndOverlay", canvas.transform, new Color(0f, 0f, 0f, 0.65f));
            dim.raycastTarget = true; // blocks input to the battlefield behind it
            UIFactory.Stretch(dim.rectTransform, 0f, 0f, 0f, 0f);
            endPanel = dim.gameObject;

            Image box = UIFactory.MakeImage("Box", dim.transform, new Color(0.12f, 0.14f, 0.2f, 0.98f));
            endBox = box.transform;
            UIFactory.Place(box.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(760f, 520f));

            endTitle = UIFactory.MakeText("Title", box.transform, "VICTORY", 96, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Place(endTitle.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -50f), new Vector2(720f, 130f));
            endSubtitle = UIFactory.MakeText("Subtitle", box.transform, "", 40, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.85f));
            UIFactory.Place(endSubtitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(700f, 100f));

            Image btn = UIFactory.MakeImage("RestartButton", box.transform, new Color(0.2f, 0.7f, 0.3f));
            btn.raycastTarget = true;
            UIFactory.Place(btn.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 50f), new Vector2(420f, 120f));
            Button button = btn.gameObject.AddComponent<Button>();
            button.targetGraphic = btn;
            button.onClick.AddListener(() => { AudioManager.Play(SfxId.UiClick); if (MatchManager.Instance != null) MatchManager.Instance.Restart(); });
            Text label = UIFactory.MakeText("Label", btn.transform, "PLAY AGAIN", 50, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(label.rectTransform, 0f, 0f, 0f, 0f);
            if (NetworkGame.IsOnline)
            {
                // Online: only the host can rematch; everybody can leave. The server moves both players, so no local reload.
                label.text = NetworkGame.IsServer ? "REMATCH" : "WAITING FOR HOST";
                button.interactable = NetworkGame.IsServer;
                Image leave = UIFactory.MakeImage("LeaveButton", box.transform, new Color(0.65f, 0.25f, 0.25f));
                leave.raycastTarget = true;
                UIFactory.Place(leave.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -90f), new Vector2(420f, 90f));
                Button leaveButton = leave.gameObject.AddComponent<Button>();
                leaveButton.targetGraphic = leave;
                leaveButton.onClick.AddListener(() => { AudioManager.Play(SfxId.UiClick); if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.Disconnect(); });
                Text leaveLabel = UIFactory.MakeText("Label", leave.transform, "LEAVE MATCH", 40, TextAnchor.MiddleCenter, Color.white);
                UIFactory.Stretch(leaveLabel.rectTransform, 0f, 0f, 0f, 0f);
            }

            endPanel.SetActive(false);
        }

        void Subscribe()
        {
            if (ResourceManager.Instance != null) ResourceManager.Instance.Changed += RefreshMana;
            if (DeckManager.Instance != null)
            {
                DeckManager.Instance.HandChanged += RefreshHand;
                DeckManager.Instance.SelectionChanged += RefreshSelection;
            }
            MatchManager mm = MatchManager.Instance;
            if (mm != null)
            {
                mm.TimerChanged += OnTimerChanged;
                mm.MatchEnded += ShowResult;
                mm.DoubleResourceStarted += OnDoubleResource;
                mm.MatchStarted += () => ShowToast("FIGHT!");
                OnTimerChanged(Mathf.CeilToInt(mm.TimeRemaining));
            }
            if (PlayerController.Instance != null) PlayerController.Instance.Message += ShowToast;
        }

        // ---------- Event handlers ----------

        bool IsAffordable(CardData c)
        {
            return c != null && ResourceManager.Instance != null && ResourceManager.Instance.CanAfford(NetworkGame.LocalTeam, c.cost);
        }

        void OnCardClicked(CardUI c)
        {
            DeckManager deck = DeckManager.Instance;
            if (deck == null) return;
            if (!IsAffordable(c.Card)) { AudioManager.Play(SfxId.UiError, 0.6f); ShowToast("Not enough mana"); return; }
            AudioManager.Play(SfxId.UiClick);
            if (deck.SelectedIndex == c.Slot) deck.ClearSelection(); else deck.Select(c.Slot);
        }

        void OnDragStarted(CardUI c)
        {
            if (!IsAffordable(c.Card)) { draggingSlot = -1; AudioManager.Play(SfxId.UiError, 0.6f); ShowToast("Not enough mana"); return; }
            AudioManager.Play(SfxId.UiClick);
            draggingSlot = c.Slot;
            DeckManager.Instance.Select(c.Slot);
            if (PlayerController.Instance != null) PlayerController.Instance.BeginDrag();
        }

        void OnDragMoved(CardUI c, Vector2 pos)
        {
            if (draggingSlot == c.Slot && PlayerController.Instance != null) PlayerController.Instance.PreviewAt(pos);
        }

        void OnDropped(CardUI c, Vector2 pos)
        {
            if (draggingSlot != c.Slot) return;
            draggingSlot = -1;
            if (PlayerController.Instance != null) PlayerController.Instance.EndDrag(pos);
        }

        void RefreshHand()
        {
            DeckManager deck = DeckManager.Instance;
            if (deck == null) return;
            for (int i = 0; i < cardUIs.Count && i < deck.Hand.Count; i++) cardUIs[i].SetCard(deck.Hand[i]);
            RefreshAffordability();
            RefreshSelection(deck.SelectedIndex);
        }

        void RefreshSelection(int index)
        {
            for (int i = 0; i < cardUIs.Count; i++) cardUIs[i].SetSelected(i == index);
            DeckManager deck = DeckManager.Instance;
            CardData card = deck != null ? deck.SelectedCard : null;
            if (hintText == null) return;
            hintText.text = card == null
                ? "Select a card"
                : card.DisplayName + " (" + card.cost + ")  -  " + (card.cardType == CardType.Spell ? "tap a target" : "tap your side to deploy") + "\n" + card.description;
        }

        void RefreshMana(Team team, float value)
        {
            if (team != NetworkGame.LocalTeam || manaFill == null) return;
            float max = ResourceManager.Instance != null ? ResourceManager.Instance.Max : 10f;
            UIFactory.SetFill(manaFill, value / max);
            manaText.text = "MANA  " + value.ToString("0.0");
            int whole = Mathf.FloorToInt(value + 0.001f);
            if (whole != lastManaInt)
            {
                if (lastManaInt >= 0 && whole > lastManaInt) Punch(ref manaPunch, manaText.transform, 1.15f);
                lastManaInt = whole;
                RefreshAffordability();
            }
        }

        // Cards are re-evaluated only when the whole-number mana value changes.
        void RefreshAffordability()
        {
            for (int i = 0; i < cardUIs.Count; i++) cardUIs[i].SetAffordable(IsAffordable(cardUIs[i].Card));
        }

        void RefreshTower(TowerHud h)
        {
            bool dead = h.tower == null || h.tower.IsDead;
            float pct = dead ? 0f : h.tower.HealthPercent;
            UIFactory.SetFill(h.fill, pct);
            h.label.text = h.prefix + "  " + (dead ? 0 : Mathf.CeilToInt(h.tower.Health));
        }

        void OnTimerChanged(int seconds)
        {
            if (timerText == null) return;
            timerText.text = (seconds / 60) + ":" + (seconds % 60).ToString("00");
            timerText.color = seconds <= 10 ? new Color(1f, 0.4f, 0.4f) : Color.white;
            if (seconds <= 10 && seconds > 0) Punch(ref timerPunch, timerText.transform, 1.35f);
        }

        void OnDoubleResource()
        {
            if (bannerRoutine != null) StopCoroutine(bannerRoutine);
            bannerRoutine = StartCoroutine(BannerRoutine("DOUBLE MANA!"));
        }

        IEnumerator BannerRoutine(string text)
        {
            bannerText.text = text;
            yield return new WaitForSeconds(3f);
            bannerText.text = "";
        }

        void ShowResult(MatchResult result)
        {
            if (endPanel == null) return;
            endPanel.SetActive(true);
            if (endBox != null) StartCoroutine(PopIn(endBox));
            switch (result)
            {
                case MatchResult.Victory: endTitle.text = "VICTORY!"; endTitle.color = new Color(1f, 0.85f, 0.3f); break;
                case MatchResult.Defeat: endTitle.text = "DEFEAT"; endTitle.color = new Color(1f, 0.4f, 0.4f); break;
                default: endTitle.text = "DRAW"; endTitle.color = Color.white; break;
            }
            MatchManager mm = MatchManager.Instance;
            endSubtitle.text = mm == null ? "" : (string.IsNullOrEmpty(mm.EndReason) ? "" : mm.EndReason + "\n") + "Towers destroyed\nYou " + mm.CrownsFor(NetworkGame.LocalTeam) + "  -  " + mm.CrownsFor(NetworkGame.OpponentTeam) + " Enemy";
        }

        public void ShowToast(string message)
        {
            if (toastText == null || string.IsNullOrEmpty(message)) return;
            if (toastRoutine != null) StopCoroutine(toastRoutine);
            toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            toastText.gameObject.SetActive(true);
            Color c = toastText.color;
            float t = 0f;
            while (t < 0.15f) // slide up + fade in
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / 0.15f);
                toastText.rectTransform.anchoredPosition = toastBasePos + new Vector2(0f, 24f * (1f - k));
                toastText.color = new Color(c.r, c.g, c.b, k);
                yield return null;
            }
            toastText.color = new Color(c.r, c.g, c.b, 1f);
            yield return new WaitForSeconds(0.9f);
            t = 0f;
            while (t < 0.3f) // fade out
            {
                t += Time.unscaledDeltaTime;
                toastText.color = new Color(c.r, c.g, c.b, 1f - Mathf.Clamp01(t / 0.3f));
                yield return null;
            }
            toastText.color = new Color(c.r, c.g, c.b, 1f);
            toastText.gameObject.SetActive(false);
        }

        /// <summary>Called when a card is played so its slot punches.</summary>
        public void PopCard(int slot)
        {
            if (slot >= 0 && slot < cardUIs.Count) cardUIs[slot].Pop();
        }

        void Punch(ref Coroutine handle, Transform t, float peak)
        {
            if (t == null) return;
            if (handle != null) StopCoroutine(handle);
            handle = StartCoroutine(PunchRoutine(t, peak));
        }

        // Scale spikes to 'peak' and eases back to normal.
        IEnumerator PunchRoutine(Transform t, float peak)
        {
            float time = 0f;
            while (time < 0.25f && t != null)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / 0.25f);
                t.localScale = Vector3.one * Mathf.Lerp(peak, 1f, k * k);
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one;
        }

        // The result box springs open with an overshoot.
        IEnumerator PopIn(Transform t)
        {
            float time = 0f;
            while (time < 0.45f)
            {
                time += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(time / 0.45f);
                float s = 1f + 2.70158f * Mathf.Pow(k - 1f, 3f) + 1.70158f * Mathf.Pow(k - 1f, 2f); // easeOutBack
                t.localScale = Vector3.one * Mathf.Max(0.01f, s);
                yield return null;
            }
            t.localScale = Vector3.one;
        }
    }
}
