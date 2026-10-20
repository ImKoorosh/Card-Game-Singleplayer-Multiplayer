using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Fixed-angle orthographic battle camera that automatically frames the whole arena for any screen
    /// aspect ratio and leaves room for the card tray. Optional controlled movement:
    ///   Mouse: right/middle drag pans, wheel zooms, arrow keys pan, F resets.
    ///   Touch: two-finger drag pans, pinch zooms (single finger is reserved for card placement).
    /// Panning is clamped so the arena can never leave the screen.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class BattleCameraController : MonoBehaviour
    {
        [Tooltip("Colour shown around the arena (e.g. the sides on a wide screen). Change it here.")]
        [SerializeField] Color backgroundColor = new Color(0.09f, 0.17f, 0.12f);
        [SerializeField, Range(35f, 80f)] float pitch = 58f;
        [Tooltip("Extra world units of space around the arena when fully zoomed out.")]
        [SerializeField] float framingMargin = 1.2f;
        [Tooltip("Allowance for tall objects (towers) so their tops are not clipped.")]
        [SerializeField] float towerHeightAllowance = 3.5f;
        [Tooltip("Smallest zoom factor (0.55 = 55% of the fitted view).")]
        [SerializeField, Range(0.3f, 1f)] float maxZoomIn = 0.55f;
        [SerializeField] float scrollZoomSpeed = 0.12f;
        [SerializeField] float keyPanSpeed = 14f;
        [SerializeField] float smoothing = 14f;
        [SerializeField] float cameraDistance = 60f;

        public static BattleCameraController Instance { get; private set; }

        Camera cam;
        float shakeTime, shakeDuration, shakeMagnitude;
        float viewYaw;   // 180 for the remote client so its own base is at the bottom of the screen
        float fittedSize = 10f, shownSize;
        float zoom = 1f, lastReserve = -1f;
        Vector2 focus, shownFocus, lastMouse;
        int lastW, lastH;
        bool dirty = true;

        float HalfW { get { return ArenaLayout.Instance != null ? ArenaLayout.Instance.HalfWidth : 9f; } }
        float HalfL { get { return ArenaLayout.Instance != null ? ArenaLayout.Instance.HalfLength : 16f; } }
        float Reserve { get { return BattleUI.Instance != null ? BattleUI.Instance.BottomReserveFraction : 0f; } }
        float SinPitch { get { return Mathf.Sin(pitch * Mathf.Deg2Rad); } }

        /// <summary>Quick screen shake (explosions, tower destruction). Strength fades out over the duration.</summary>
        public void Shake(float magnitude, float duration)
        {
            shakeMagnitude = magnitude;
            shakeDuration = Mathf.Max(0.05f, duration);
            shakeTime = shakeDuration;
        }

        void Awake()
        {
            Instance = this;
            viewYaw = NetworkGame.LocalTeam == Team.Enemy ? 180f : 0f;
            cam = GetComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = backgroundColor;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = cameraDistance * 3f;
            transform.rotation = Quaternion.Euler(pitch, viewYaw, 0f);
        }

        void Start() { Refit(true); }

        /// <summary>Recomputes the fitted view (screen size / tray height changed).</summary>
        void Refit(bool snap)
        {
            lastW = Screen.width;
            lastH = Screen.height;
            lastReserve = Reserve;
            cam.rect = new Rect(0f, lastReserve, 1f, 1f - lastReserve);

            float aspect = (float)Screen.width / Mathf.Max(1f, Screen.height * (1f - lastReserve));
            float sin = SinPitch, cos = Mathf.Cos(pitch * Mathf.Deg2Rad);
            float needV = (HalfL * 2f * sin + towerHeightAllowance * cos) * 0.5f + framingMargin;
            float needH = (HalfW + framingMargin) / aspect;
            fittedSize = Mathf.Max(needV, needH);
            if (snap) shownSize = fittedSize * zoom;
            ClampFocus();
            dirty = true;
        }

        void Update()
        {
            if (Screen.width != lastW || Screen.height != lastH || !Mathf.Approximately(Reserve, lastReserve)) Refit(false);
            HandleInput();

            Vector2 f = focus;
            float targetSize = fittedSize * zoom;
            bool shaking = shakeTime > 0f;
            if (!dirty && !shaking && (shownFocus - f).sqrMagnitude < 1e-6f && Mathf.Abs(shownSize - targetSize) < 1e-4f) return; // idle: do nothing

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            shownFocus = Vector2.Lerp(shownFocus, f, k);
            shownSize = Mathf.Lerp(shownSize, targetSize, k);
            cam.orthographicSize = shownSize;
            transform.rotation = Quaternion.Euler(pitch, viewYaw, 0f);
            transform.position = new Vector3(shownFocus.x, 0f, shownFocus.y) - transform.forward * cameraDistance;
            if (shaking)
            {
                shakeTime -= Time.unscaledDeltaTime;
                float fade = Mathf.Clamp01(shakeTime / shakeDuration);
                Vector3 off = new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), 0f) * shakeMagnitude * fade;
                transform.position += transform.rotation * off;
            }
            dirty = shaking; // one extra un-shaken frame after the shake ends
        }

        void HandleInput()
        {
            if (Input.touchCount == 2)
            {
                Touch a = Input.GetTouch(0), b = Input.GetTouch(1);
                float prev = ((a.position - a.deltaPosition) - (b.position - b.deltaPosition)).magnitude;
                float now = (a.position - b.position).magnitude;
                if (prev > 1f && now > 1f) Zoom(prev / now);
                Pan((a.deltaPosition + b.deltaPosition) * 0.5f);
                return;
            }
            if (Input.touchCount > 0) return;

            Vector2 m = Input.mousePosition;
            if (Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2)) lastMouse = m;
            if (Input.GetMouseButton(1) || Input.GetMouseButton(2))
            {
                Pan(m - lastMouse);
                lastMouse = m;
            }
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f) Zoom(1f - scroll * scrollZoomSpeed);

            Vector2 key = Vector2.zero;
            if (Input.GetKey(KeyCode.LeftArrow)) key.x -= 1f;
            if (Input.GetKey(KeyCode.RightArrow)) key.x += 1f;
            if (Input.GetKey(KeyCode.UpArrow)) key.y += 1f;
            if (Input.GetKey(KeyCode.DownArrow)) key.y -= 1f;
            if (key != Vector2.zero)
            {
                focus += key * NetworkGame.ViewSign * keyPanSpeed * zoom * Time.unscaledDeltaTime;
                ClampFocus();
            }
            if (Input.GetKeyDown(KeyCode.F)) ResetView();
        }

        void Pan(Vector2 pixelDelta)
        {
            float worldPerPixel = (2f * fittedSize * zoom) / Mathf.Max(1f, cam.pixelHeight);
            focus.x -= pixelDelta.x * worldPerPixel * NetworkGame.ViewSign;
            focus.y -= pixelDelta.y * worldPerPixel * NetworkGame.ViewSign / SinPitch; // screen-up maps to world +Z stretched by 1/sin(pitch)
            ClampFocus();
        }

        void Zoom(float factor)
        {
            zoom = Mathf.Clamp(zoom * factor, maxZoomIn, 1f);
            ClampFocus();
        }

        public void ResetView()
        {
            zoom = 1f;
            focus = Vector2.zero;
        }

        // Keep the visible rectangle inside the arena (plus margin). At full zoom-out there is no slack, so focus = 0.
        void ClampFocus()
        {
            float size = fittedSize * zoom;
            float aspect = cam != null && cam.pixelHeight > 0 ? (float)cam.pixelWidth / cam.pixelHeight : 0.56f;
            float viewHalfX = size * aspect;
            float viewHalfZ = size / SinPitch;
            float maxX = Mathf.Max(0f, HalfW + framingMargin - viewHalfX);
            float maxZ = Mathf.Max(0f, HalfL + framingMargin - viewHalfZ);
            focus.x = Mathf.Clamp(focus.x, -maxX, maxX);
            focus.y = Mathf.Clamp(focus.y, -maxZ, maxZ);
        }
    }
}
