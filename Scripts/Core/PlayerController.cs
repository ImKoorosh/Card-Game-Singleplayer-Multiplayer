using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace CardBattle
{
    /// <summary>
    /// Player input for deployment. Works with mouse and touch:
    ///  - Tap a card, then click/tap (release) on your half of the arena to deploy.
    ///  - Or drag a card from the tray onto the arena (BattleUI forwards the drag events here).
    ///  - Keyboard (editor): 1-8 select, Q/E cycle, Esc deselect.
    /// A ring shows the deploy point (green = valid, red = invalid).
    /// </summary>
    public class PlayerController : MonoBehaviour
    {
        public static PlayerController Instance { get; private set; }

        [SerializeField] Camera cam;
        [Tooltip("Optional. A transparent material for the placement ring.")]
        [SerializeField] Material indicatorMaterial;
        [SerializeField] Color validColor = new Color(0.3f, 1f, 0.4f, 0.9f);
        [SerializeField] Color invalidColor = new Color(1f, 0.3f, 0.3f, 0.9f);

        /// <summary>Human-readable feedback ("Not enough mana", "Deploy on your own side", ...).</summary>
        public event Action<string> Message;

        static readonly Plane Ground = new Plane(Vector3.up, Vector3.zero);
        const int RingSegments = 48;

        LineRenderer ring;
        float ringRadius = -1f;
        bool pressedOverUi, dragging;

        void Awake() { Instance = this; }

        void Start()
        {
            if (cam == null) cam = Camera.main;
            BuildIndicator();
        }

        void Update()
        {
            DeckManager deck = DeckManager.Instance;
            if (deck == null || cam == null || !MatchManager.IsRunning) { HideIndicator(); return; }

            HandleKeyboard(deck);
            if (dragging) return; // the card drag callbacks drive the preview instead

            bool touching = Input.touchCount > 0;
            Vector2 screen;
            bool pressed, released, overUi;
            if (touching)
            {
                if (Input.touchCount > 1) { HideIndicator(); pressedOverUi = true; return; } // 2 fingers = camera gesture
                Touch t = Input.GetTouch(0);
                screen = t.position;
                pressed = t.phase == TouchPhase.Began;
                released = t.phase == TouchPhase.Ended;
                overUi = IsOverUi(t.fingerId);
            }
            else
            {
                screen = Input.mousePosition;
                pressed = Input.GetMouseButtonDown(0);
                released = Input.GetMouseButtonUp(0);
                overUi = IsOverUi(-1);
            }
            if (pressed) pressedOverUi = overUi;

            CardData card = deck.SelectedCard;
            bool inView = cam.pixelRect.Contains(screen);
            Vector3 point;

            if (inView && !overUi && !pressedOverUi && TryGroundPoint(screen, out point))
            {
                if (card == null)
                {
                    HideIndicator();
                    if (released) Notify("Select a card first");
                    return;
                }
                // Touch shows the ring only while the finger is down; mouse shows it on hover.
                ShowIndicator(card, point);
                if (released) DeployAt(deck.SelectedIndex, point);
                return;
            }
            HideIndicator();
        }

        void HandleKeyboard(DeckManager deck)
        {
            for (int i = 0; i < 8; i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i)) deck.Select(i);
            if (Input.GetKeyDown(KeyCode.E)) deck.CycleSelection(1);
            if (Input.GetKeyDown(KeyCode.Q)) deck.CycleSelection(-1);
            if (Input.GetKeyDown(KeyCode.Escape)) deck.ClearSelection();
            if (Input.GetKeyDown(KeyCode.M)) AudioManager.ToggleMute();
        }

        // ---------- Drag from the card tray (called by BattleUI) ----------

        public void BeginDrag() { dragging = true; }

        public void PreviewAt(Vector2 screen)
        {
            DeckManager deck = DeckManager.Instance;
            Vector3 point;
            if (deck != null && deck.SelectedCard != null && cam != null && cam.pixelRect.Contains(screen) && TryGroundPoint(screen, out point))
                ShowIndicator(deck.SelectedCard, point);
            else
                HideIndicator();
        }

        public void EndDrag(Vector2 screen)
        {
            dragging = false;
            DeckManager deck = DeckManager.Instance;
            Vector3 point;
            if (deck != null && deck.SelectedCard != null && cam != null && cam.pixelRect.Contains(screen) && TryGroundPoint(screen, out point))
                DeployAt(deck.SelectedIndex, point);
            else
                HideIndicator(); // dropped back on the tray = cancel
        }

        // ---------- Deployment ----------

        bool DeployAt(int slot, Vector3 point)
        {
            HideIndicator();
            DeckManager deck = DeckManager.Instance;
            if (deck == null || CardManager.Instance == null || slot < 0 || slot >= deck.Hand.Count) return false;

            if (NetworkGame.IsOnline) return RequestOnlineDeploy(deck, slot, point);

            string message;
            DeployResult result = CardManager.Instance.TryDeploy(NetworkGame.LocalTeam, deck.Hand[slot], point, out message);
            if (result == DeployResult.Success)
            {
                if (BattleUI.Instance != null) BattleUI.Instance.PopCard(slot);
                deck.OnCardPlayed(slot);
                return true;
            }
            Notify(message);
            return false;
        }

        // Online the client never spawns anything: it sends a request and the server answers (OnDeployResult).
        bool RequestOnlineDeploy(DeckManager deck, int slot, Vector3 point)
        {
            NetworkPlayer me = NetworkPlayer.Local;
            if (me == null) { Notify("Not connected"); return false; }
            CardData card = deck.Hand[slot];
            string reason;
            // Cheap local checks for instant feedback. The server validates everything again.
            if (!ArenaLayout.Instance.IsValidDeployPoint(NetworkGame.LocalTeam, card, point, out reason)) { Notify(reason); return false; }
            if (!CardManager.Instance.CanAfford(NetworkGame.LocalTeam, card)) { Notify("Not enough mana"); return false; }
            me.RequestDeploy(slot, CardRegistry.IdOf(card), point);
            return true;
        }

        /// <summary>The server's answer to a deploy request (online only).</summary>
        public void OnDeployResult(int handSlot, DeployResult result, string message)
        {
            if (result == DeployResult.Success)
            {
                if (BattleUI.Instance != null) BattleUI.Instance.PopCard(handSlot);
                if (DeckManager.Instance != null) DeckManager.Instance.OnCardPlayed(handSlot);
            }
            else Notify(message);
        }

        void Notify(string message)
        {
            if (string.IsNullOrEmpty(message)) return;
            AudioManager.Play(SfxId.UiError, 0.6f);
            if (Message != null) Message(message);
        }

        // ---------- Helpers ----------

        bool TryGroundPoint(Vector2 screen, out Vector3 point)
        {
            Ray ray = cam.ScreenPointToRay(screen);
            float enter;
            if (Ground.Raycast(ray, out enter))
            {
                point = ray.GetPoint(enter);
                point.y = 0f;
                return true;
            }
            point = Vector3.zero;
            return false;
        }

        static bool IsOverUi(int fingerId)
        {
            EventSystem es = EventSystem.current;
            if (es == null) return false;
            return fingerId >= 0 ? es.IsPointerOverGameObject(fingerId) : es.IsPointerOverGameObject();
        }

        void BuildIndicator()
        {
            GameObject go = new GameObject("PlacementIndicator");
            go.transform.SetParent(transform, false);
            ring = go.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = RingSegments;
            ring.widthMultiplier = 0.14f;
            ring.sharedMaterial = indicatorMaterial != null ? indicatorMaterial : PlaceholderUtil.MakeOverlay(Color.white);
            ring.enabled = false;
        }

        void ShowIndicator(CardData card, Vector3 point)
        {
            if (ring == null) return;
            string reason;
            bool valid = ArenaLayout.Instance != null && ArenaLayout.Instance.IsValidDeployPoint(NetworkGame.LocalTeam, card, point, out reason);
            float radius = card.cardType == CardType.Spell ? Mathf.Max(0.5f, card.spell.radius) : Mathf.Max(0.6f, card.stats.bodyRadius + 0.3f);
            if (!Mathf.Approximately(radius, ringRadius)) SetRingRadius(radius);
            ring.transform.position = point + Vector3.up * 0.08f;
            ring.startColor = ring.endColor = valid ? validColor : invalidColor;
            ring.enabled = true;
        }

        void HideIndicator() { if (ring != null && ring.enabled) ring.enabled = false; }

        void SetRingRadius(float radius)
        {
            ringRadius = radius;
            for (int i = 0; i < RingSegments; i++)
            {
                float a = (Mathf.PI * 2f / RingSegments) * i;
                ring.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
            }
        }
    }
}
