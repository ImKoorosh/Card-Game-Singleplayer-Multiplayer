using System;
using System.Collections.Generic;
using UnityEngine;

namespace CardBattle
{
    public enum SfxId
    {
        None = 0, UiClick, UiError, Deploy, Spawn, SwordHit, ArrowShot, MagicShot, Zap, Explosion, Heal,
        Freeze, Stun, Shield, Whoosh, TowerShot, TowerHit, TowerDestroyed, UnitDeath, BattleStart,
        Victory, Defeat, Tick
    }

    /// <summary>
    /// Plays every sound in the game. All clips are SYNTHESISED in code by <see cref="SfxSynth"/>, so no audio
    /// files are needed. To use real audio, add entries to "Overrides" (any SfxId -> your AudioClip).
    /// Sounds are throttled per type and played through a small voice pool, so big fights never get deafening.
    /// Static wrappers (AudioManager.Play) are null-safe: the game runs fine without an AudioManager in the scene.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public static bool Muted { get; private set; }

        [Serializable] public class ClipOverride { public SfxId id; public AudioClip clip; }

        [SerializeField, Range(0f, 1f)] float sfxVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] float musicVolume = 0.25f;
        [SerializeField] bool playMusic = true;
        [SerializeField] int voices = 14;
        [Tooltip("Optional: replace any generated sound with your own clip.")]
        [SerializeField] List<ClipOverride> overrides = new List<ClipOverride>();

        AudioSource[] pool;
        AudioSource music;
        int nextVoice;
        readonly Dictionary<SfxId, AudioClip> clips = new Dictionary<SfxId, AudioClip>();
        readonly Dictionary<SfxId, float> lastPlayed = new Dictionary<SfxId, float>();

        void Awake()
        {
            Instance = this;
            Muted = PlayerPrefs.GetInt("cb_muted", 0) == 1;
            AudioListener.volume = Muted ? 0f : 1f;

            pool = new AudioSource[Mathf.Max(4, voices)];
            for (int i = 0; i < pool.Length; i++)
            {
                GameObject go = new GameObject("Voice" + i);
                go.transform.SetParent(transform, false);
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f; // 2D: simple and consistent on mobile
                pool[i] = s;
            }
        }

        void Start()
        {
            if (playMusic)
            {
                GameObject go = new GameObject("Music");
                go.transform.SetParent(transform, false);
                music = go.AddComponent<AudioSource>();
                music.clip = SfxSynth.MakeMusic();
                music.loop = true;
                music.volume = musicVolume;
                music.Play();
            }
            HookGameEvents();
        }

        void HookGameEvents()
        {
            MatchManager mm = MatchManager.Instance;
            if (mm != null)
            {
                mm.MatchStarted += () => Play(SfxId.BattleStart);
                mm.TimerChanged += s => { if (s <= 10 && s > 0) Play(SfxId.Tick, 0.6f); };
                mm.DoubleResourceStarted += () => Play(SfxId.Stun, 0.6f);
                mm.MatchEnded += r =>
                {
                    if (music != null) music.volume = musicVolume * 0.3f;
                    Play(r == MatchResult.Defeat ? SfxId.Defeat : SfxId.Victory);
                };
            }
            if (CardManager.Instance != null)
                CardManager.Instance.CardDeployed += (team, card, pos) =>
                {
                    if (card.cardType != CardType.Spell) Play(SfxId.Deploy, team == Team.Player ? 1f : 0.55f);
                };
        }

        /// <summary>Plays a sound effect. Safe to call from anywhere (does nothing if there is no AudioManager).</summary>
        public static void Play(SfxId id, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlayInternal(id, volume, pitch);
        }

        public static void ToggleMute()
        {
            Muted = !Muted;
            PlayerPrefs.SetInt("cb_muted", Muted ? 1 : 0);
            AudioListener.volume = Muted ? 0f : 1f;
        }

        void PlayInternal(SfxId id, float volume, float pitch)
        {
            if (id == SfxId.None || Muted || pool == null) return;

            float now = Time.unscaledTime, last;
            if (lastPlayed.TryGetValue(id, out last) && now - last < MinGap(id)) return; // throttle
            lastPlayed[id] = now;

            AudioClip clip = GetClip(id);
            if (clip == null) return;
            AudioSource s = pool[nextVoice];
            nextVoice = (nextVoice + 1) % pool.Length;
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume * sfxVolume);
            s.pitch = pitch * UnityEngine.Random.Range(0.94f, 1.06f);
            s.Play();
        }

        AudioClip GetClip(SfxId id)
        {
            for (int i = 0; i < overrides.Count; i++)
                if (overrides[i].id == id && overrides[i].clip != null) return overrides[i].clip;
            AudioClip clip;
            if (!clips.TryGetValue(id, out clip))
            {
                clip = SfxSynth.Create(id);
                clips[id] = clip;
            }
            return clip;
        }

        static float MinGap(SfxId id)
        {
            switch (id)
            {
                case SfxId.SwordHit: return 0.05f;
                case SfxId.ArrowShot: return 0.06f;
                case SfxId.TowerHit: return 0.15f;
                case SfxId.Explosion: return 0.08f;
                case SfxId.UnitDeath: return 0.06f;
                default: return 0.03f;
            }
        }
    }
}
