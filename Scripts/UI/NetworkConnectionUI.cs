using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>
    /// The multiplayer menu (Menu scene), built from code like the battle HUD. Three screens:
    ///  - Main:  Play vs AI (offline) / Host online game / Join online game
    ///  - Host:  port, Start Server and Local Player, connection info, players, Start Match, Stop Server
    ///  - Join:  IP address, port, Connect, status and errors, Disconnect
    /// It only calls NetworkBootstrap / NetworkGameManager and displays their state.
    /// </summary>
    public class NetworkConnectionUI : MonoBehaviour
    {
        [SerializeField] string battleScene = "Battle";

        enum Screen { Main, Host, Join }

        RectTransform root, mainPanel, hostPanel, joinPanel;
        Screen current;

        // host screen
        InputField hostPort;
        Button hostStartButton, hostMatchButton, hostPublicIpButton, hostCopyButton;
        Text hostStartLabel, hostInfo, hostError, hostMatchLabel;
        string publicIp;
        // join screen
        InputField joinAddress, joinPort;
        Button joinConnectButton;
        Text joinStatus, joinError, joinConnectLabel;

        void Start()
        {
            EnsureEventSystem();
            Canvas canvas = BuildCanvas();
            root = canvas.GetComponent<RectTransform>();
            BuildMain(); BuildHost(); BuildJoin();

            bool online = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
            Show(online ? (NetworkManager.Singleton.IsServer ? Screen.Host : Screen.Join) : Screen.Main);

            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.StateChanged += Refresh;
            if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.SessionChanged += Refresh;
            Refresh();
        }

        void OnDestroy()
        {
            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.StateChanged -= Refresh;
            if (NetworkGameManager.Instance != null) NetworkGameManager.Instance.SessionChanged -= Refresh;
        }

        // ---------------- layout helpers ----------------

        static void EnsureEventSystem()
        {
            if (EventSystem.current == null) new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }

        Canvas BuildCanvas()
        {
            GameObject go = new GameObject("MenuCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(transform, false);
            Canvas c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            Image bg = UIFactory.MakeImage("Background", go.transform, new Color(0.09f, 0.12f, 0.2f));
            UIFactory.Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);
            return c;
        }

        RectTransform NewPanel(string name)
        {
            RectTransform p = UIFactory.MakeRect(name, root);
            UIFactory.Stretch(p, 0f, 0f, 0f, 0f);
            return p;
        }

        static readonly Vector2 TopCenter = new Vector2(0.5f, 1f);

        Text Label(Transform parent, string text, int size, float y, float height, TextAnchor anchor = TextAnchor.MiddleCenter, float width = 900f)
        {
            Text t = UIFactory.MakeText("Label", parent, text, size, anchor, Color.white);
            UIFactory.Place(t.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -y), new Vector2(width, height));
            return t;
        }

        Button MakeButton(Transform parent, string label, Color color, float y, UnityAction onClick, out Text labelText, float width = 760f, float height = 110f)
        {
            Image img = UIFactory.MakeImage("Button", parent, color);
            img.raycastTarget = true;
            UIFactory.Place(img.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -y), new Vector2(width, height));
            Button b = img.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            b.onClick.AddListener(onClick);
            labelText = UIFactory.MakeText("Label", img.transform, label, 42, TextAnchor.MiddleCenter, Color.white);
            UIFactory.Stretch(labelText.rectTransform, 0f, 0f, 0f, 0f);
            return b;
        }

        InputField MakeInput(Transform parent, string placeholder, string value, float y, bool integersOnly, float width = 760f)
        {
            Image bg = UIFactory.MakeImage("Input", parent, new Color(0.16f, 0.2f, 0.3f));
            bg.raycastTarget = true;
            UIFactory.Place(bg.rectTransform, TopCenter, TopCenter, TopCenter, new Vector2(0f, -y), new Vector2(width, 96f));
            InputField field = bg.gameObject.AddComponent<InputField>();
            Text text = UIFactory.MakeText("Text", bg.transform, "", 40, TextAnchor.MiddleLeft, Color.white);
            UIFactory.Stretch(text.rectTransform, 24f, 4f, 24f, 4f);
            text.supportRichText = false;
            Text ph = UIFactory.MakeText("Placeholder", bg.transform, placeholder, 40, TextAnchor.MiddleLeft, new Color(1f, 1f, 1f, 0.35f));
            UIFactory.Stretch(ph.rectTransform, 24f, 4f, 24f, 4f);
            field.textComponent = text;
            field.placeholder = ph;
            field.characterValidation = integersOnly ? InputField.CharacterValidation.Integer : InputField.CharacterValidation.None;
            field.characterLimit = integersOnly ? 5 : 64;
            field.text = value;
            return field;
        }

        static readonly Color Green = new Color(0.2f, 0.65f, 0.3f), Blue = new Color(0.25f, 0.45f, 0.85f),
                              Red = new Color(0.7f, 0.28f, 0.28f), Grey = new Color(0.35f, 0.38f, 0.48f);

        // ---------------- screens ----------------

        void BuildMain()
        {
            mainPanel = NewPanel("Main");
            Label(mainPanel, "CARD BATTLE", 110, 160f, 160f);
            Label(mainPanel, "version " + Application.version, 28, 320f, 40f);
            Text t;
            MakeButton(mainPanel, "PLAY VS AI (OFFLINE)", Green, 480f, () => { AudioManager.Play(SfxId.UiClick); NetworkGame.ResetToOffline(); SceneManager.LoadScene(battleScene); }, out t);
            MakeButton(mainPanel, "HOST ONLINE GAME", Blue, 630f, () => Show(Screen.Host), out t);
            MakeButton(mainPanel, "JOIN ONLINE GAME", Blue, 780f, () => Show(Screen.Join), out t);
        }

        void BuildHost()
        {
            hostPanel = NewPanel("Host");
            Label(hostPanel, "HOST GAME", 84, 80f, 110f);
            Label(hostPanel, "Port (UDP)", 30, 215f, 40f, TextAnchor.MiddleLeft, 760f);
            ushort port = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.DefaultPort : (ushort)7777;
            hostPort = MakeInput(hostPanel, "7777", port.ToString(), 260f, true);
            hostStartButton = MakeButton(hostPanel, "START SERVER AND PLAY", Green, 380f, OnHostStart, out hostStartLabel);

            hostInfo = Label(hostPanel, "", 30, 520f, 560f, TextAnchor.UpperLeft, 860f);
            hostInfo.verticalOverflow = VerticalWrapMode.Overflow;

            Text t;
            hostPublicIpButton = MakeButton(hostPanel, "SHOW MY PUBLIC IP", Grey, 1100f, OnLookUpPublicIp, out t, 370f, 90f);
            hostCopyButton = MakeButton(hostPanel, "COPY ADDRESS", Grey, 1100f, OnCopyAddress, out t, 370f, 90f);
            hostPublicIpButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(-195f, -1100f);
            hostCopyButton.GetComponent<RectTransform>().anchoredPosition = new Vector2(195f, -1100f);

            hostMatchButton = MakeButton(hostPanel, "START MATCH", Green, 1230f, OnStartMatch, out hostMatchLabel);
            MakeButton(hostPanel, "STOP SERVER / BACK", Red, 1370f, OnHostBack, out t);
            hostError = Label(hostPanel, "", 32, 1520f, 220f, TextAnchor.UpperCenter, 860f);
            hostError.color = new Color(1f, 0.55f, 0.5f);
        }

        void BuildJoin()
        {
            joinPanel = NewPanel("Join");
            Label(joinPanel, "JOIN GAME", 84, 80f, 110f);
            Label(joinPanel, "Host's IP address", 30, 215f, 40f, TextAnchor.MiddleLeft, 760f);
            joinAddress = MakeInput(joinPanel, "e.g. 203.0.113.25", PlayerPrefs.GetString("cb_last_ip", ""), 260f, false);
            Label(joinPanel, "Port (UDP)", 30, 375f, 40f, TextAnchor.MiddleLeft, 760f);
            ushort port = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.DefaultPort : (ushort)7777;
            joinPort = MakeInput(joinPanel, "7777", port.ToString(), 420f, true);
            joinConnectButton = MakeButton(joinPanel, "CONNECT", Green, 560f, OnConnect, out joinConnectLabel);
            joinStatus = Label(joinPanel, "", 34, 710f, 360f, TextAnchor.UpperCenter, 860f);
            joinStatus.verticalOverflow = VerticalWrapMode.Overflow;
            Text t;
            MakeButton(joinPanel, "DISCONNECT / BACK", Red, 1100f, OnJoinBack, out t);
            joinError = Label(joinPanel, "", 32, 1240f, 300f, TextAnchor.UpperCenter, 860f);
            joinError.color = new Color(1f, 0.55f, 0.5f);
        }

        void Show(Screen s)
        {
            current = s;
            mainPanel.gameObject.SetActive(s == Screen.Main);
            hostPanel.gameObject.SetActive(s == Screen.Host);
            joinPanel.gameObject.SetActive(s == Screen.Join);
            Refresh();
        }

        // ---------------- actions ----------------

        static ushort ParsePort(InputField f)
        {
            int p;
            if (!int.TryParse(f.text, out p) || p < 1024 || p > 65535) return 0;
            return (ushort)p;
        }

        void OnHostStart()
        {
            AudioManager.Play(SfxId.UiClick);
            ushort port = ParsePort(hostPort);
            if (port == 0) { hostError.text = "Enter a port between 1024 and 65535."; return; }
            publicIp = null;
            NetworkBootstrap.Instance.StartHost(port);
            Refresh();
        }

        void OnStartMatch()
        {
            AudioManager.Play(SfxId.UiClick);
            if (NetworkGameManager.Instance != null && !NetworkGameManager.Instance.StartMatch()) hostError.text = "Cannot start yet: two players are required.";
        }

        void OnHostBack()
        {
            if (NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.State != ConnectionState.Disconnected) NetworkBootstrap.Instance.Disconnect();
            Show(Screen.Main);
        }

        void OnLookUpPublicIp()
        {
            hostError.text = "Asking api.ipify.org for your public IP...";
            StartCoroutine(NetworkBootstrap.Instance.LookUpPublicIp(ip =>
            {
                publicIp = ip;
                hostError.text = ip == null ? "Could not look up the public IP (no internet?). Search \"what is my IP\" in a browser instead." : "";
                Refresh();
            }));
        }

        void OnCopyAddress()
        {
            string port = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.Port.ToString() : "7777";
            GUIUtility.systemCopyBuffer = (publicIp ?? "") + ":" + port;
        }

        void OnConnect()
        {
            AudioManager.Play(SfxId.UiClick);
            ushort port = ParsePort(joinPort);
            if (port == 0) { joinError.text = "Enter a port between 1024 and 65535."; return; }
            joinError.text = "";
            PlayerPrefs.SetString("cb_last_ip", joinAddress.text.Trim());
            NetworkBootstrap.Instance.StartClient(joinAddress.text, port);
            Refresh();
        }

        void OnJoinBack()
        {
            if (NetworkBootstrap.Instance != null && NetworkBootstrap.Instance.State != ConnectionState.Disconnected) NetworkBootstrap.Instance.Disconnect();
            joinError.text = "";
            Show(Screen.Main);
        }

        // ---------------- display ----------------

        void Refresh()
        {
            if (hostPanel == null) return;
            NetworkBootstrap nb = NetworkBootstrap.Instance;
            NetworkGameManager gm = NetworkGameManager.Instance;
            ConnectionState state = nb != null ? nb.State : ConnectionState.Disconnected;
            bool idle = state == ConnectionState.Disconnected || state == ConnectionState.ConnectionLost;

            // --- host screen ---
            hostStartButton.interactable = idle;
            hostStartLabel.text = idle ? "START SERVER AND PLAY" : "SERVER RUNNING";
            hostPort.interactable = idle;
            hostMatchButton.interactable = gm != null && gm.CanStartMatch;
            hostMatchLabel.text = (gm != null && gm.CanStartMatch) ? "START MATCH" : "START MATCH (needs 2 players)";
            hostPublicIpButton.interactable = !idle;
            hostCopyButton.interactable = !string.IsNullOrEmpty(publicIp);
            if (!idle && state != ConnectionState.Connecting && hostError != null && (hostError.text.StartsWith("Enter") || hostError.text.StartsWith("Cannot"))) hostError.text = "";

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("Status: " + StateName(state) + (nb != null ? "  -  " + nb.Status : ""));
            if (!idle)
            {
                sb.AppendLine("Listening on UDP port " + nb.Port);
                sb.AppendLine("Players connected: " + NetworkPlayer.All.Count + " / " + NetworkGame.MaxPlayers);
                sb.AppendLine(PlayerLine(0) + "\n" + PlayerLine(1));
                sb.AppendLine("Same Wi-Fi / VPN: share one of these addresses:");
                foreach (string a in NetworkBootstrap.GetLocalAddresses()) sb.AppendLine("   " + a);
                sb.AppendLine("Over the internet: share your public IP" + (publicIp != null ? " = " + publicIp : " (press SHOW MY PUBLIC IP)") + " and port " + nb.Port);
                sb.AppendLine("(needs port forwarding or a VPN, see MULTIPLAYER.md)");
            }
            else if (nb != null && !string.IsNullOrEmpty(nb.LastError)) sb.AppendLine(nb.LastError);
            hostInfo.text = sb.ToString();

            // --- join screen ---
            joinConnectButton.interactable = idle;
            joinConnectLabel.text = state == ConnectionState.Connecting ? "CONNECTING..." : (idle ? "CONNECT" : "CONNECTED");
            joinAddress.interactable = idle;
            joinPort.interactable = idle;
            string status = "Status: " + StateName(state);
            if (!idle && nb != null) status += "\n" + nb.Status + "\nPlayers in lobby: " + NetworkPlayer.All.Count + " / " + NetworkGame.MaxPlayers;
            joinStatus.text = status;
            if (nb != null && !string.IsNullOrEmpty(nb.LastError) && idle) joinError.text = nb.LastError;
            else if (!idle) joinError.text = "";
        }

        static string PlayerLine(int slot)
        {
            foreach (NetworkPlayer p in NetworkPlayer.All)
                if (p.Slot == slot) return "   Player " + (slot + 1) + (slot == 0 ? " (host)" : "") + ": connected";
            return "   Player " + (slot + 1) + (slot == 0 ? " (host)" : "") + ": waiting...";
        }

        static string StateName(ConnectionState s)
        {
            switch (s)
            {
                case ConnectionState.WaitingForOpponent: return "Waiting for opponent";
                case ConnectionState.LobbyReady: return "Lobby ready";
                case ConnectionState.MatchInProgress: return "Match in progress";
                case ConnectionState.ConnectionLost: return "Connection lost";
                default: return s.ToString();
            }
        }
    }
}
