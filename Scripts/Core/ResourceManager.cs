using System;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Holds the regenerating resource ("Mana") for both players. Both sides use the exact same rules,
    /// so the AI can never out-produce the player.
    /// </summary>
    public class ResourceManager : MonoBehaviour
    {
        public static ResourceManager Instance { get; private set; }

        [SerializeField] float maxResource = 10f;
        [SerializeField] float startResource = 5f;
        [Tooltip("Seconds needed to regenerate 1 resource point.")]
        [SerializeField] float secondsPerPoint = 2.8f;

        readonly float[] current = new float[2];
        float regenMultiplier = 1f;
        bool running;

        public float Max { get { return maxResource; } }
        /// <summary>Raised whenever a pool changes (every frame while regenerating).</summary>
        public event Action<Team, float> Changed;

        void Awake()
        {
            Instance = this;
            current[0] = current[1] = Mathf.Clamp(startResource, 0f, maxResource);
        }

        public float Get(Team t) { return current[(int)t]; }
        public bool CanAfford(Team t, float cost) { return current[(int)t] + 0.0001f >= cost; }

        public bool TrySpend(Team t, float cost)
        {
            if (!CanAfford(t, cost)) return false;
            current[(int)t] = Mathf.Max(0f, current[(int)t] - cost);
            if (Changed != null) Changed(t, current[(int)t]);
            return true;
        }

        /// <summary>Remote clients only: mirrors the value the server sent for this player.</summary>
        public void ApplyRemote(Team t, float value)
        {
            current[(int)t] = Mathf.Clamp(value, 0f, maxResource);
            if (Changed != null) Changed(t, current[(int)t]);
        }

        public void SetRunning(bool value) { running = value; }
        public void SetRegenMultiplier(float m) { regenMultiplier = Mathf.Max(0f, m); }

        void Update()
        {
            if (!running) return;
            float gain = Time.deltaTime * regenMultiplier / Mathf.Max(0.01f, secondsPerPoint);
            for (int i = 0; i < 2; i++)
            {
                if (current[i] >= maxResource) continue;
                current[i] = Mathf.Min(maxResource, current[i] + gain);
                if (Changed != null) Changed((Team)i, current[i]);
            }
        }
    }
}
