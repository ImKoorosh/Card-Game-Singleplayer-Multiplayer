using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace CardBattle
{
    public enum DisconnectPolicy
    {
        /// <summary>The player who stayed wins immediately.</summary>
        EndMatch,
        /// <summary>Pause the match and let the player reconnect within the grace period; if they do not, the other player wins.</summary>
        PauseThenEnd
    }

    /// <summary>Server-side record of one of the two seats. Survives a disconnect so a player can reconnect.</summary>
    public class PlayerSlot
    {
        public int index;
        public Team team;
        public string token = "";        // per-session secret chosen by the client, used to recognise a reconnect
        public ulong clientId;
        public bool connected;
        public bool reserved;            // approved, waiting for the connection to finish
        public ulong reservedClient;
        public bool battleReady;
        public NetworkPlayer player;
        public List<CardData> deck;
        public DeckModel model;
        public int lastRequestId;
        public float lastRequestTime;
    }

    /// <summary>
    /// Server-side session logic: seats, connection approval, spawning player objects, starting / restarting the
    /// match (scene change), and what happens when somebody disconnects. Lives for the whole app (DontDestroyOnLoad).
    /// Clients only ever see the replicated NetworkPlayer objects; this class does nothing on a remote client.
    /// </summary>
    public class NetworkGameManager : MonoBehaviour
    {
        public static NetworkGameManager Instance { get; private set; }

        [Header("Scenes (must be in Build Settings)")]
        [SerializeField] string battleScene = "Battle";
        [SerializeField] string menuScene = "Menu";

        [Header("Match rules")]
        [SerializeField] int deckSize = 8;
        [Tooltip("Cards visible at once. 4 = classic cycling from the 8-card deck, 8 = whole deck visible.")]
        [SerializeField] int handSize = 4;
        [SerializeField] float battleLoadTimeout = 30f;

        [Header("Disconnects during a match")]
        [SerializeField] DisconnectPolicy disconnectPolicy = DisconnectPolicy.PauseThenEnd;
        [SerializeField] float reconnectGraceSeconds = 30f;

        public readonly PlayerSlot[] Slots = new PlayerSlot[NetworkGame.MaxPlayers];

        public event Action SessionChanged;
        public bool InMatch { get; private set; }
        public int ConnectedPlayers
        {
            get { int n = 0; for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null && Slots[i].connected) n++; return n; }
        }

        NetworkBootstrap bootstrap;
        Coroutine graceRoutine, loadWatchdog;
        bool serverActive;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public static void NotifyPlayersChanged()
        {
            if (Instance != null && Instance.SessionChanged != null) Instance.SessionChanged();
            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.RefreshState();
        }

        // ---------------- server lifetime ----------------

        public void ServerBegin(NetworkBootstrap owner)
        {
            bootstrap = owner;
            serverActive = true;
            InMatch = false;
            for (int i = 0; i < Slots.Length; i++) Slots[i] = new PlayerSlot { index = i, team = (Team)i };

            NetworkManager nm = NetworkManager.Singleton;
            nm.ConnectionApprovalCallback = Approve;
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
        }

        public void ServerEnd()
        {
            serverActive = false;
            InMatch = false;
            StopAllCoroutines();
            graceRoutine = null; loadWatchdog = null;
            NetworkManager nm = NetworkManager.Singleton;
            if (nm != null)
            {
                nm.OnClientConnectedCallback -= OnClientConnected;
                nm.OnClientDisconnectCallback -= OnClientDisconnected;
                nm.ConnectionApprovalCallback = null;
            }
            for (int i = 0; i < Slots.Length; i++) Slots[i] = null;
        }

        // ---------------- connection approval (runs on the server) ----------------

        void Approve(NetworkManager.ConnectionApprovalRequest request, NetworkManager.ConnectionApprovalResponse response)
        {
            response.CreatePlayerObject = false; // we spawn the NetworkPlayer ourselves so the SERVER decides the seat
            response.Pending = false;
            NetworkManager nm = NetworkManager.Singleton;

            // The host's own connection.
            if (request.ClientNetworkId == nm.LocalClientId)
            {
                Reserve(Slots[0], request.ClientNetworkId, "");
                response.Approved = true;
                return;
            }

            string reason;
            string token;
            if (!ValidatePayload(request.Payload, out reason, out token)) { Reject(response, reason); return; }

            // A returning player (same session token, seat currently empty)?
            PlayerSlot slot = null;
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null && !Slots[i].connected && !Slots[i].reserved && Slots[i].token.Length > 0 && Slots[i].token == token) slot = Slots[i];

            if (slot == null)
            {
                if (InMatch) { Reject(response, "A match is already in progress."); return; }
                for (int i = 1; i < Slots.Length; i++)
                    if (Slots[i] != null && !Slots[i].connected && !Slots[i].reserved) { slot = Slots[i]; break; }
                if (slot == null) { Reject(response, "The match is full (2 players maximum)."); return; }
            }

            Reserve(slot, request.ClientNetworkId, token);
            response.Approved = true;
        }

        static void Reject(NetworkManager.ConnectionApprovalResponse response, string reason)
        {
            response.Approved = false;
            response.Reason = reason;
        }

        static void Reserve(PlayerSlot slot, ulong clientId, string token)
        {
            slot.reserved = true;
            slot.reservedClient = clientId;
            if (!string.IsNullOrEmpty(token)) slot.token = token;
        }

        static bool ValidatePayload(byte[] payload, out string reason, out string token)
        {
            reason = null; token = "";
            string text = payload != null ? Encoding.UTF8.GetString(payload) : "";
            string[] p = text.Split('|');
            if (p.Length < 5 || p[0] != "CB1") { reason = "This is not a Card Battle client."; return false; }
            int proto;
            if (!int.TryParse(p[1], out proto) || proto != NetworkGame.ProtocolVersion)
            { reason = "Network protocol mismatch. Both players need the same game version."; return false; }
            if (p[2] != Application.version)
            { reason = "Version mismatch: the host runs " + Application.version + ", you run " + p[2] + "."; return false; }
            if (p[3] != CardRegistry.Hash.ToString())
            { reason = "Card data mismatch. Both players must use the same build of the game."; return false; }
            token = p[4];
            return true;
        }

        // ---------------- connect / disconnect ----------------

        void OnClientConnected(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (!serverActive || !nm.IsServer) return;

            PlayerSlot slot = null;
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null && Slots[i].reserved && Slots[i].reservedClient == clientId) slot = Slots[i];
            if (slot == null && clientId == nm.LocalClientId) slot = Slots[0];
            if (slot == null) return;

            slot.reserved = false;
            slot.connected = true;
            slot.clientId = clientId;

            GameObject go = Instantiate(bootstrap.PlayerPrefab);
            NetworkPlayer np = go.GetComponent<NetworkPlayer>();
            np.ServerInit(slot);
            go.GetComponent<NetworkObject>().SpawnAsPlayerObject(clientId, false);
            slot.player = np;

            if (InMatch && slot.model != null)
            {
                // Reconnecting mid-match: restore the seat's hand. The client loads the battle scene on its own and reports ready.
                slot.battleReady = false;
                np.ServerBeginMatchStateKeepModel();
            }
            NotifyPlayersChanged();
        }

        void OnClientDisconnected(ulong clientId)
        {
            NetworkManager nm = NetworkManager.Singleton;
            if (!serverActive || nm == null || !nm.IsServer) return;

            PlayerSlot slot = null;
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] != null && (Slots[i].clientId == clientId || (Slots[i].reserved && Slots[i].reservedClient == clientId))) slot = Slots[i];
            if (slot == null || slot.index == 0) return; // the host leaving means the whole server is shutting down

            slot.connected = false;
            slot.reserved = false;
            slot.player = null;
            slot.battleReady = false;

            bool matchLive = InMatch && MatchManager.Instance != null && MatchManager.Instance.State != MatchState.Ended;
            if (!matchLive)
            {
                slot.token = "";          // free the seat for anyone
                slot.model = null;
            }
            else HandleDisconnectDuringMatch(slot);
            NotifyPlayersChanged();
        }

        void HandleDisconnectDuringMatch(PlayerSlot gone)
        {
            MatchManager mm = MatchManager.Instance;
            if (mm == null) return;
            if (disconnectPolicy == DisconnectPolicy.EndMatch || reconnectGraceSeconds <= 0f)
            {
                mm.EndByForfeit(gone.team, "Your opponent disconnected.");
                return;
            }
            mm.SetPaused(true);
            if (graceRoutine != null) StopCoroutine(graceRoutine);
            graceRoutine = StartCoroutine(GraceRoutine(gone));
        }

        IEnumerator GraceRoutine(PlayerSlot gone)
        {
            float remaining = reconnectGraceSeconds;
            while (remaining > 0f && !gone.connected && serverActive)
            {
                if (NetworkMatchManager.Instance != null) NetworkMatchManager.Instance.ServerSetGrace(Mathf.CeilToInt(remaining));
                yield return new WaitForSeconds(1f);
                remaining -= 1f;
            }
            if (NetworkMatchManager.Instance != null) NetworkMatchManager.Instance.ServerSetGrace(0);
            graceRoutine = null;
            if (!gone.connected && MatchManager.Instance != null && MatchManager.Instance.State == MatchState.Paused)
                MatchManager.Instance.EndByForfeit(gone.team, "Your opponent did not reconnect.");
        }

        // ---------------- match lifecycle (host only) ----------------

        public bool CanStartMatch { get { return serverActive && !InMatch && ConnectedPlayers == NetworkGame.MaxPlayers; } }

        /// <summary>Host button. Deals decks, then moves both players to the battle scene.</summary>
        public bool StartMatch()
        {
            if (!NetworkGame.IsServer) return false;
            if (ConnectedPlayers != NetworkGame.MaxPlayers) return false;

            List<CardData> pool = CardLibrary.LoadPool();
            if (pool.Count == 0) { Debug.LogError("NetworkGameManager: no cards found in a Resources/Cards folder."); return false; }

            for (int i = 0; i < Slots.Length; i++)
            {
                PlayerSlot s = Slots[i];
                s.deck = CardLibrary.RandomDeck(pool, deckSize, 1, 2, 2);         // each player gets their own random deck
                s.model = new DeckModel(s.deck, handSize, true);
                s.battleReady = false;
                s.player.ServerBeginMatchState();
            }
            InMatch = true;
            NetworkManager.Singleton.SceneManager.LoadScene(battleScene, LoadSceneMode.Single);
            if (loadWatchdog != null) StopCoroutine(loadWatchdog);
            loadWatchdog = StartCoroutine(BattleLoadWatchdog());
            NotifyPlayersChanged();
            return true;
        }

        IEnumerator BattleLoadWatchdog()
        {
            float t = 0f;
            while (t < battleLoadTimeout)
            {
                if (AllReady()) { loadWatchdog = null; yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            loadWatchdog = null;
            Debug.LogWarning("NetworkGameManager: a player did not finish loading the battle scene in time; returning to the lobby.");
            ReturnToLobby();
        }

        bool AllReady()
        {
            for (int i = 0; i < Slots.Length; i++)
                if (Slots[i] == null || !Slots[i].connected || !Slots[i].battleReady) return false;
            return true;
        }

        /// <summary>Called by a client (through its NetworkPlayer) when its battle HUD is ready.</summary>
        public static void OnPlayerBattleReady()
        {
            NetworkGameManager gm = Instance;
            if (gm == null || !gm.serverActive || MatchManager.Instance == null) return;
            if (!gm.AllReady()) return;
            MatchManager mm = MatchManager.Instance;
            if (mm.State == MatchState.Waiting) mm.BeginCountdown();                 // first start
            else if (mm.State == MatchState.Paused)                                  // everybody is back after a disconnect
            {
                if (gm.graceRoutine != null) { gm.StopCoroutine(gm.graceRoutine); gm.graceRoutine = null; }
                if (NetworkMatchManager.Instance != null) NetworkMatchManager.Instance.ServerSetGrace(0);
                mm.SetPaused(false);
            }
        }

        /// <summary>Host button on the result screen.</summary>
        public void RequestRematch()
        {
            if (!NetworkGame.IsServer) return;
            if (ConnectedPlayers < NetworkGame.MaxPlayers) ReturnToLobby(); // the opponent left: back to the lobby to wait for a new one
            else StartMatch();
        }

        public void ReturnToLobby()
        {
            if (!NetworkGame.IsServer) return;
            InMatch = false;
            for (int i = 0; i < Slots.Length; i++) if (Slots[i] != null) { Slots[i].battleReady = false; Slots[i].model = null; }
            NetworkManager.Singleton.SceneManager.LoadScene(menuScene, LoadSceneMode.Single);
            NotifyPlayersChanged();
        }
    }
}
