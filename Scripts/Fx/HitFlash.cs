using System.Collections;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Flashes a Damageable's meshes white for a moment whenever it takes damage. Added automatically by
    /// Damageable.Start. Renderers named "Fx*" (shields, auras) are ignored. Uses property blocks (no materials created).
    /// </summary>
    public class HitFlash : MonoBehaviour
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        Damageable owner;
        Renderer[] renderers;
        Color[] baseColors;
        bool[] isTint;
        MaterialPropertyBlock block;
        float flashUntil;
        Coroutine routine;
        const float Duration = 0.14f;

        public static void Attach(Damageable d)
        {
            if (d == null || d.GetComponent<HitFlash>() != null) return;
            HitFlash f = d.gameObject.AddComponent<HitFlash>();
            f.owner = d;
            d.Damaged += f.OnDamaged;
        }

        void OnDamaged(float amount)
        {
            flashUntil = Time.time + Duration;
            if (routine == null) routine = StartCoroutine(Flash());
        }

        void Cache()
        {
            MeshRenderer[] all = owner.GetComponentsInChildren<MeshRenderer>(true);
            var list = new System.Collections.Generic.List<Renderer>();
            for (int i = 0; i < all.Length; i++) if (!all[i].name.StartsWith("Fx")) list.Add(all[i]);
            renderers = list.ToArray();
            baseColors = new Color[renderers.Length];
            isTint = new bool[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                isTint[i] = renderers[i].name.StartsWith("Tint");
                Material m = renderers[i].sharedMaterial;
                baseColors[i] = isTint[i] ? owner.Team.GetColor() : (m != null ? m.color : Color.white);
            }
            block = new MaterialPropertyBlock();
        }

        IEnumerator Flash()
        {
            if (renderers == null) Cache();
            while (Time.time < flashUntil && owner != null && !owner.IsDead)
            {
                float k = Mathf.Clamp01((flashUntil - Time.time) / Duration);
                Apply(k * 0.85f);
                yield return null;
            }
            if (owner != null && !owner.IsDead) Apply(0f);
            routine = null;
        }

        void Apply(float whiteAmount)
        {
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] == null) continue;
                if (whiteAmount <= 0.001f && !isTint[i]) { renderers[i].SetPropertyBlock(null); continue; }
                Color c = Color.Lerp(baseColors[i], Color.white, whiteAmount);
                renderers[i].GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                block.SetColor(BaseColorId, c);
                renderers[i].SetPropertyBlock(block);
            }
        }

        void OnDestroy() { if (owner != null) owner.Damaged -= OnDamaged; }
    }
}
