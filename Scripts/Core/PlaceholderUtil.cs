using UnityEngine;
using UnityEngine.Rendering;

namespace CardBattle
{
    /// <summary>
    /// Helpers for generating placeholder graphics that work in Built-in, URP and HDRP without
    /// hard-coding shader names (we clone whatever default material the pipeline uses for primitives).
    /// </summary>
    public static class PlaceholderUtil
    {
        static Material cachedDefault;

        /// <summary>Set by GameManager: an asset material that uses a transparent shader, so it survives shader stripping in builds.</summary>
        public static Material OverlayBase;

        /// <summary>Set by GameManager: an asset material using the pipeline's lit shader. Because a scene object references it, the shader is guaranteed to be in builds (no pink materials).</summary>
        public static Material LitBase;

        public static Material GetDefaultLitMaterial()
        {
            if (LitBase != null) return LitBase;
            if (cachedDefault != null) return cachedDefault;
            RenderPipelineAsset rp = GraphicsSettings.currentRenderPipeline;
            if (rp != null && rp.defaultMaterial != null)
            {
                cachedDefault = rp.defaultMaterial;
                return cachedDefault;
            }
            GameObject tmp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cachedDefault = tmp.GetComponent<Renderer>().sharedMaterial;
            DestroyObject(tmp);
            return cachedDefault;
        }

        public static Material MakeLit(Color color)
        {
            Material m = new Material(GetDefaultLitMaterial());
            m.color = color;
            return m;
        }

        /// <summary>Flat, alpha-blended material for area indicators (falls back to lit if the shader is missing).</summary>
        public static Material MakeOverlay(Color color)
        {
            if (OverlayBase != null)
            {
                Material b = new Material(OverlayBase);
                b.color = color;
                return b;
            }
            Shader s = Shader.Find("Sprites/Default");
            Material m = s != null ? new Material(s) : new Material(GetDefaultLitMaterial());
            m.color = color;
            return m;
        }

        public static void DestroyObject(Object o)
        {
            if (o == null) return;
            if (Application.isPlaying) Object.Destroy(o); else Object.DestroyImmediate(o);
        }

        /// <summary>Creates a collider-free primitive child. Name it "Tint_*" to receive the team colour.</summary>
        public static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider col = go.GetComponent<Collider>();
            if (col != null) DestroyObject(col);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            if (material != null) go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            return Part(type, name, parent, localPos, localScale, MakeLit(color));
        }
    }
}
