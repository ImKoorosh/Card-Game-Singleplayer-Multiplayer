using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Networking;

namespace CardBattle
{
    /// <summary>
    /// Starts and stops the network session. It creates and configures the NetworkManager + UnityTransport at runtime
    /// (or re-uses one you placed in the scene), registers the network prefabs, performs the connection handshake and
    /// exposes a simple <see cref="ConnectionState"/> for the UI.
    /// Lives for the whole app (DontDestroyOnLoad). Put it in the Menu scene.
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        public static NetworkBootstrap Instance { get; private set; }

        [Tooltip("The NetworkPlayer prefab (NetworkObject + NetworkPlayer). Assigned by the scene builder.")]
        [SerializeField] GameObject playerPrefab;
        [SerializeField] ushort defaultPort = 7777;
        [SerializeField] string menuScene = "Menu";
        [Tooltip("How long a client waits for the server before giving up.")]
        [SerializeField] float connectTimeoutSeconds = 12f;

        // The secret that lets the server recognise a reconnecting player. Per app session, never shown to anyone.
        static readonly string SessionToken = Guid.NewGuid().ToString("N");

        public GameObject PlayerPrefab { get { return playerPrefab; } }
        public ushort DefaultPort { get { return defaultPort; } }
        public ConnectionState State { get; private set; }
        public string Status { get; private set; }
        public string LastError { get; private set; }
        public string Address { get; private set; }
        public ushort Port { get; private set; }
        public bool IsHost { get { return NetworkGame.IsServer; } }
        public event Action StateChanged;

        NetworkManager nm;
        UnityTransport transport;
        readonly HashSet<GameObject> registered = new HashSet<GameObject>();
        bool wasConnected, shuttingDown, matchActive;
        Coroutine timeoutRoutine;

        void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true; // otherwise an unfocused window stops sending network heartbeats
            State = ConnectionState.Disconnected;
            Status = "Not connected";
        }

        // ---------------- NetworkManager setup ----------------

        void PrepareNetworkManager()
        {
            nm = NetworkManager.Singleton;
            if (nm == null)
            {
                GameObject go = new GameObject("NetworkManager");
                nm = go.AddComponent<NetworkManager>();
                go.AddComponent<UnityTransport>();
            }
            transport = nm.GetComponent<UnityTransport>();
            if (transport == null) transport = nm.gameObject.AddComponent<UnityTransport>();
            if (nm.NetworkConfig == null) nm.NetworkConfig = new NetworkConfig();

            nm.NetworkConfig.NetworkTransport = transport;
            nm.NetworkConfig.ConnectionApproval = true;       // the server decides who may join
            nm.NetworkConfig.PlayerPrefab = null;             // the server spawns NetworkPlayer objects itself
            nm.RunInBackground = true;

            // Fail fast on a wrong address instead of retrying for a full minute.
            transport.MaxConnectAttempts = 5;
            transport.ConnectTimeoutMS = 2000;
            transport.DisconnectTimeoutMS = 10000;

            RegisterPrefabs();
        }

        // Every prefab that can be spawned over the network must be registered on host AND client before starting.
        void RegisterPrefabs()
        {
            if (playerPrefab != null) Register(playerPrefab);
            IList<CardData> cards = CardRegistry.All;
            for (int i = 0; i < cards.Count; i++)
            {
                GameObject prefab = cards[i].prefab;
                if (prefab != null && prefab.GetComponent<NetworkObject>() != null) Register(prefab);
            }
        }

        void Register(GameObject prefab)
        {
            if (registered.Contains(prefab)) return;
            try { nm.AddNetworkPrefab(prefab); }
            catch (Exception) { /* already registered by a NetworkPrefabsList: fine */ }
            registered.Add(prefab);
        }

        // ---------------- host ----------------

        public bool StartHost(ushort port)
        {
            if (State != ConnectionState.Disconnected && State != ConnectionState.ConnectionLost) return false;
            if (NetworkGameManager.Instance == null) { Fail("Internal error: NetworkGameManager is missing from the scene."); return false; }

            PrepareNetworkManager();
            shuttingDown = false;
            LastError = "";
            Port = port;
            Address = "0.0.0.0";
            // Address 127.0.0.1 is what the host's own client dials; the server listens on every network adapter.
            transport.SetConnectionData("127.0.0.1", port, "0.0.0.0");

            NetworkGameManager.Instance.ServerBegin(this);
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            SetState(ConnectionState.Connecting, "Starting server on port " + port + "...");

            bool ok = false;
            try { ok = nm.StartHost(); } catch (Exception e) { Debug.LogException(e); }
            if (!ok)
            {
                CleanupCallbacks();
                NetworkGameManager.Instance.ServerEnd();
                Fail("Could not start the server on port " + port + ". Another program may already use it, or the port is blocked.");
                return false;
            }
            NetworkGame.SetLocalTeam(Team.Player);
            wasConnected = true;
            SetState(ConnectionState.WaitingForOpponent, "Server running. Waiting for an opponent...");
            return true;
        }

        // ---------------- client ----------------

        public bool StartClient(string address, ushort port)
        {
            if (State != ConnectionState.Disconnected && State != ConnectionState.ConnectionLost) return false;
            address = (address ?? "").Trim();
            if (address.Length == 0) { Fail("Enter the host's IP address."); return false; }

            string resolved;
            if (!TryResolve(address, out resolved)) { Fail("'" + address + "' is not a valid IP address or host name."); return false; }

            PrepareNetworkManager();
            shuttingDown = false;
            wasConnected = false;
            LastError = "";
            Address = resolved;
            Port = port;
            transport.SetConnectionData(resolved, port);
            nm.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(BuildPayload());
            nm.OnClientConnectedCallback += OnClientConnected;
            nm.OnClientDisconnectCallback += OnClientDisconnected;
            SetState(ConnectionState.Connecting, "Connecting to " + resolved + ":" + port + "...");

            bool ok = false;
            try { ok = nm.StartClient(); } catch (Exception e) { Debug.LogException(e); }
            if (!ok)
            {
                CleanupCallbacks();
                Fail("Could not start the client.");
                return false;
            }
            timeoutRoutine = StartCoroutine(ConnectTimeout());
            return true;
        }

        static string BuildPayload()
        {
            return "CB1|" + NetworkGame.ProtocolVersion + "|" + Application.version + "|" + CardRegistry.Hash + "|" + SessionToken;
        }

        static bool TryResolve(string text, out string ip)
        {
            ip = null;
            IPAddress parsed;
            if (IPAddress.TryParse(text, out parsed)) { ip = parsed.ToString(); return true; }
            try
            {
                foreach (IPAddress a in Dns.GetHostAddresses(text))
                    if (a.AddressFamily == AddressFamily.InterNetwork) { ip = a.ToString(); return true; }
            }
            catch (Exception) { }
            return false;
        }

        IEnumerator ConnectTimeout()
        {
            float t = 0f;
            while (t < connectTimeoutSeconds)
            {
                if (State != ConnectionState.Connecting) { timeoutRoutine = null; yield break; }
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            timeoutRoutine = null;
            if (State == ConnectionState.Connecting)
            {
                ShutdownInternal();
                Fail("Timed out. Check the IP address and port, the host's firewall / port forwarding, and that the host is running.");
            }
        }

        // ---------------- callbacks ----------------

        void OnClientConnected(ulong clientId)
        {
            if (nm == null || clientId != nm.LocalClientId) return;
            wasConnected = true;
            if (!nm.IsServer) SetState(ConnectionState.Connected, "Connected. Joining lobby...");
            RefreshState();
        }

        void OnClientDisconnected(ulong clientId)
        {
            if (shuttingDown || nm == null) return;
            if (nm.IsServer) { RefreshState(); return; }       // a guest left: NetworkGameManager handles it

            // We (a client) lost the connection or were rejected.
            string reason = nm.DisconnectReason;
            bool lost = wasConnected;
            if (string.IsNullOrEmpty(reason))
                reason = lost ? "The connection to the host was lost. The host may have stopped the server or the network dropped."
                              : "Could not reach the server. Check the IP address, port, firewall and port forwarding.";
            StartCoroutine(HandleClientDisconnect(reason, lost));
        }

        IEnumerator HandleClientDisconnect(string reason, bool lost)
        {
            yield return null; // leave the NGO callback before shutting down
            ShutdownInternal();
            LastError = reason;
            SetState(lost ? ConnectionState.ConnectionLost : ConnectionState.Disconnected, reason);
            LeaveBattleScene();
        }

        // ---------------- stop ----------------

        /// <summary>Host: stop the server. Client: disconnect. Both return to the menu.</summary>
        public void Disconnect()
        {
            bool wasHost = nm != null && nm.IsServer;
            ShutdownInternal();
            LastError = "";
            SetState(ConnectionState.Disconnected, wasHost ? "Server stopped." : "Disconnected.");
            LeaveBattleScene();
        }

        void ShutdownInternal()
        {
            shuttingDown = true;
            if (timeoutRoutine != null) { StopCoroutine(timeoutRoutine); timeoutRoutine = null; }
            CleanupCallbacks();
            if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.ServerEnd();
            if (nm != null && nm.IsListening) nm.Shutdown();
            matchActive = false;
            NetworkGame.ResetToOffline();
        }

        void CleanupCallbacks()
        {
            if (nm == null) return;
            nm.OnClientConnectedCallback -= OnClientConnected;
            nm.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        void LeaveBattleScene()
        {
            if (SceneManager.GetActiveScene().name != menuScene) SceneManager.LoadScene(menuScene);
        }

        // ---------------- state ----------------

        public void SetMatchActive(bool active)
        {
            matchActive = active;
            RefreshState();
        }

        /// <summary>Recomputes the high-level state from the facts (players present, match running).</summary>
        public void RefreshState()
        {
            if (nm == null || !nm.IsListening) return;
            if (State == ConnectionState.Connecting && !nm.IsServer && !nm.IsConnectedClient) return;

            ConnectionState s;
            string text;
            int players = NetworkPlayer.All.Count;
            if (matchActive) { s = ConnectionState.MatchInProgress; text = "Match in progress"; }
            else if (players >= NetworkGame.MaxPlayers) { s = ConnectionState.LobbyReady; text = nm.IsServer ? "Both players are here. Press START MATCH." : "Lobby ready. Waiting for the host to start the match..."; }
            else if (nm.IsServer) { s = ConnectionState.WaitingForOpponent; text = "Server running. Waiting for an opponent..."; }
            else if (players >= 1) { s = ConnectionState.WaitingForOpponent; text = "In the lobby. Waiting for the host..."; }
            else { s = ConnectionState.Connected; text = "Connected. Joining lobby..."; }
            SetState(s, text);
        }

        void SetState(ConnectionState state, string text)
        {
            bool changed = State != state || Status != text;
            State = state;
            Status = text;
            if (changed && StateChanged != null) StateChanged();
        }

        void Fail(string message)
        {
            LastError = message;
            SetState(ConnectionState.Disconnected, message);
        }

        // ---------------- helpers for the host UI ----------------

        /// <summary>The host's private network addresses (LAN / VPN). Public IP is NOT in this list.</summary>
        public static List<string> GetLocalAddresses()
        {
            var result = new List<string>();
            try
            {
                foreach (NetworkInterface ni in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (ni.OperationalStatus != OperationalStatus.Up || ni.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (UnicastIPAddressInformation ua in ni.GetIPProperties().UnicastAddresses)
                        if (ua.Address.AddressFamily == AddressFamily.InterNetwork) result.Add(ua.Address.ToString() + "  (" + ni.Name + ")");
                }
            }
            catch (Exception) { }
            return result;
        }

        /// <summary>Optional: asks api.ipify.org for the host's public IP. Only runs when the user presses the button.</summary>
        public IEnumerator LookUpPublicIp(Action<string> done)
        {
            using (UnityWebRequest req = UnityWebRequest.Get("https://api.ipify.org"))
            {
                req.timeout = 8;
                yield return req.SendWebRequest();
                string text = req.downloadHandler != null ? req.downloadHandler.text : "";
                done(string.IsNullOrEmpty(req.error) && text.Length > 0 ? text.Trim() : null);
            }
        }
    }
}
