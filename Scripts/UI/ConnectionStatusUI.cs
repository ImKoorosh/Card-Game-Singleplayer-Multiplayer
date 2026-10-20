using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>
    /// Small overlay in the Battle scene (online only): shows "ONLINE" status, a banner while the match is paused
    /// waiting for a disconnected opponent (with the countdown), and a Leave button. Does nothing offline.
    /// </summary>
    public class ConnectionStatusUI : MonoBehaviour
    {
        Text statusText, banner;
        GameObject bannerRoot;

        void Start()
        {
            if (!NetworkGame.IsOnline) { enabled = false; return; }

            GameObject go = new GameObject("ConnectionCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            Canvas c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 150;
            CanvasScaler scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;

            statusText = UIFactory.MakeText("Status", go.transform, "", 24, TextAnchor.UpperLeft, new Color(1f, 1f, 1f, 0.6f));
            UIFactory.Place(statusText.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(16f, -150f), new Vector2(500f, 40f));

            Image bg = UIFactory.MakeImage("Banner", go.transform, new Color(0f, 0f, 0f, 0.8f));
            bannerRoot = bg.gameObject;
            UIFactory.Place(bg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 200f), new Vector2(900f, 260f));
            banner = UIFactory.MakeText("Text", bg.transform, "", 44, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.4f));
            UIFactory.Stretch(banner.rectTransform, 20f, 20f, 20f, 20f);
            bannerRoot.SetActive(false);

            MatchManager mm = MatchManager.Instance;
            if (mm != null) mm.StateChanged += s => Refresh();
            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.StateChanged += Refresh;
            InvokeRepeating("Refresh", 0.5f, 0.5f); // twice a second is plenty for a text label
            Refresh();
        }

        void OnDestroy()
        {
            if (NetworkBootstrap.Instance != null) NetworkBootstrap.Instance.StateChanged -= Refresh;
        }

        void Refresh()
        {
            if (statusText == null) return;
            ConnectionState cs = NetworkBootstrap.Instance != null ? NetworkBootstrap.Instance.State : ConnectionState.Disconnected;
            statusText.text = "ONLINE  -  " + (NetworkGame.IsServer ? "HOST" : "GUEST") + "  -  " + cs;

            MatchManager mm = MatchManager.Instance;
            bool paused = mm != null && mm.State == MatchState.Paused;
            bannerRoot.SetActive(paused);
            if (paused)
            {
                int grace = NetworkMatchManager.Instance != null ? NetworkMatchManager.Instance.GraceSeconds : 0;
                banner.text = grace > 0
                    ? "Opponent disconnected.\nWaiting for them to reconnect... " + grace + "s"
                    : "Match paused.\nWaiting for the other player...";
            }
        }
    }
}
