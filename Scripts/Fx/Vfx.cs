using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Code-built visual effects (particle bursts, expanding rings, lightning bolts). No prefabs or textures needed.
    /// All calls are fire-and-forget: the created objects clean themselves up.
    /// </summary>
    public static class Vfx
    {
        static Material shared;

        /// <summary>One shared white, alpha-blended material used by particles, trails and bolts (colour comes from vertex colour).</summary>
        public static Material SharedOverlay
        {
            get
            {
                if (shared == null) shared = PlaceholderUtil.MakeOverlay(Color.white);
                return shared;
            }
        }

        public static void Burst(Vector3 pos, Color color, int count = 10, float speed = 3f, float size = 0.22f, float life = 0.5f, float gravity = 0.6f)
        {
            GameObject go = new GameObject("VfxBurst");
            go.transform.position = pos;
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear); // it auto-plays with defaults; configure while stopped

            ParticleSystem.MainModule main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = life;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = gravity;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.15f;

            ParticleSystem.SizeOverLifetimeModule sol = ps.sizeOverLifetime;
            sol.enabled = true;
            sol.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f)); // shrink to nothing

            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = SharedOverlay;
            ps.Play();
            Object.Destroy(go, life + 0.6f);
        }

        public static void Dust(Vector3 pos)
        {
            Burst(pos + Vector3.up * 0.1f, new Color(0.78f, 0.7f, 0.55f, 0.9f), 8, 1.6f, 0.3f, 0.45f, -0.05f);
        }

        public static void Sparks(Vector3 pos, Color color)
        {
            Burst(pos, color, 6, 4f, 0.14f, 0.3f, 1.2f);
        }

        public static void Explosion(Vector3 pos, float radius, Color color)
        {
            Burst(pos + Vector3.up * 0.3f, color, 28, 2f + radius * 1.5f, 0.45f, 0.7f, 0.3f);
            Burst(pos + Vector3.up * 0.2f, new Color(1f, 0.92f, 0.65f, 1f), 14, 3f + radius, 0.25f, 0.4f, 0.8f);
            Ring(pos, radius, color, 0.45f);
        }

        /// <summary>Flat disc on the ground that expands to <paramref name="radius"/> and fades out.</summary>
        public static void Ring(Vector3 pos, float radius, Color color, float duration)
        {
            Material m = PlaceholderUtil.MakeOverlay(new Color(color.r, color.g, color.b, 0.5f));
            GameObject go = PlaceholderUtil.Part(PrimitiveType.Cylinder, "FxRing", null, new Vector3(pos.x, 0.07f, pos.z), new Vector3(0.2f, 0.01f, 0.2f), m);
            go.AddComponent<FxDisc>().Play(radius * 2f, duration);
        }

        /// <summary>Jagged lightning line that vanishes after a moment.</summary>
        public static void Bolt(Vector3 from, Vector3 to, Color color)
        {
            GameObject go = new GameObject("FxBolt");
            LineRenderer lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = true;
            lr.positionCount = 7;
            lr.widthMultiplier = 0.16f;
            lr.sharedMaterial = SharedOverlay;
            lr.startColor = color;
            lr.endColor = color;
            for (int i = 0; i < 7; i++)
            {
                Vector3 p = Vector3.Lerp(from, to, i / 6f);
                if (i > 0 && i < 6) p += new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f)) * 0.28f;
                lr.SetPosition(i, p);
            }
            Object.Destroy(go, 0.15f);
        }
    }

    /// <summary>Animates a flat disc: grows to a target diameter while fading out, then destroys itself and its material.</summary>
    public class FxDisc : MonoBehaviour
    {
        float target, duration, t;
        Renderer rend;
        Material mat;
        Color baseColor;

        public void Play(float diameter, float seconds)
        {
            target = diameter;
            duration = Mathf.Max(0.05f, seconds);
            rend = GetComponent<Renderer>();
            mat = rend.sharedMaterial;
            baseColor = mat.color;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / duration);
            float d = Mathf.Lerp(0.2f, target, 1f - (1f - k) * (1f - k));
            transform.localScale = new Vector3(d, 0.01f, d);
            Color c = baseColor;
            c.a = baseColor.a * (1f - k);
            mat.color = c;
            if (k >= 1f) Destroy(gameObject);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
