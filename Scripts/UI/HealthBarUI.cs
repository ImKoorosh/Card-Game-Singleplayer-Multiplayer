using UnityEngine;
using UnityEngine.UI;

namespace CardBattle
{
    /// <summary>
    /// World-space health bar attached above a Damageable. Fully event driven: it redraws only when the
    /// owner's HealthChanged fires. Units only show it once damaged; towers always show it.
    /// The bar is oriented once to the (fixed-angle) battle camera, so it needs no per-frame billboarding.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        Damageable owner;
        RectTransform fill;
        Canvas canvas;
        bool alwaysVisible;

        public static HealthBarUI Create(Damageable owner, float height, float width, bool alwaysVisible)
        {
            if (owner == null) return null;

            GameObject go = new GameObject("HealthBar", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(owner.transform, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);

            Canvas c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.WorldSpace;
            c.sortingOrder = 20;

            RectTransform rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(100f, alwaysVisible ? 18f : 12f);
            go.transform.localScale = Vector3.one * (width / 100f);
            Camera cam = Camera.main;
            if (cam != null) go.transform.rotation = cam.transform.rotation;

            Image bg = UIFactory.MakeImage("Background", rt, new Color(0f, 0f, 0f, 0.7f));
            UIFactory.Stretch(bg.rectTransform, 0f, 0f, 0f, 0f);
            Image fillImage = UIFactory.MakeImage("Fill", rt, owner.Team.GetColor());

            HealthBarUI bar = go.AddComponent<HealthBarUI>();
            bar.owner = owner;
            bar.fill = fillImage.rectTransform;
            bar.canvas = c;
            bar.alwaysVisible = alwaysVisible;
            owner.HealthChanged += bar.Refresh;
            owner.Died += bar.OnOwnerDied;
            bar.Refresh(owner);
            return bar;
        }

        void Refresh(Damageable d)
        {
            if (canvas == null || d == null) return;
            float pct = d.HealthPercent;
            UIFactory.SetFill(fill, pct);
            // Keep a 2px inner margin look by shrinking slightly (purely cosmetic).
            fill.offsetMin = new Vector2(2f, 2f);
            fill.offsetMax = new Vector2(pct >= 0.999f ? -2f : 0f, -2f);
            canvas.enabled = alwaysVisible || pct < 0.999f;
        }

        void OnOwnerDied(Damageable d)
        {
            if (canvas != null) canvas.enabled = false;
        }

        void OnDestroy()
        {
            if (owner != null)
            {
                owner.HealthChanged -= Refresh;
                owner.Died -= OnOwnerDied;
            }
        }
    }
}
