using System;
using UnityEngine;

namespace CardBattle
{
    /// <summary>
    /// Procedural sound design: builds every effect (and a short battle music loop) from oscillators,
    /// noise and envelopes at runtime, so the project needs zero audio assets. Clips are created once and cached.
    /// </summary>
    public static class SfxSynth
    {
        const int Rate = 44100;
        static uint seed = 22222u;

        // Deterministic white noise in [-1, 1].
        static float Noise()
        {
            seed = seed * 1664525u + 1013904223u;
            return ((seed >> 9) / 4194304f) - 1f;
        }

        static float Sin(float hz, float t) { return Mathf.Sin(2f * Mathf.PI * hz * t); }
        static float Decay(float t, float k) { return Mathf.Exp(-k * t); }
        static float Chirp(float f0, float f1, float dur, float t)
        {
            // Phase of a linear frequency sweep f0 -> f1 over 'dur' seconds.
            return Mathf.Sin(2f * Mathf.PI * (f0 * t + (f1 - f0) * t * t / (2f * dur)));
        }
        static float Saw(float hz, float t) { float p = hz * t; return 2f * (p - Mathf.Floor(p)) - 1f; }
        static float Square(float hz, float t) { return Sin(hz, t) >= 0f ? 1f : -1f; }
        static float Tri(float hz, float t) { float p = hz * t; return 4f * Mathf.Abs(p - Mathf.Floor(p + 0.5f)) - 1f; }
        static float Chime(float hz, float start, float t) { return t < start ? 0f : Sin(hz, t - start) * Decay(t - start, 7f); }
        static float Note(float hz, float start, float t)
        {
            if (t < start) return 0f;
            float u = t - start;
            float env = Mathf.Min(1f, u / 0.012f) * Decay(u, 2.6f);
            return (Tri(hz, u) * 0.3f + Sin(hz * 2f, u) * 0.06f) * env;
        }

        static AudioClip Make(string name, float seconds, Func<float, float> f, int rate, bool fadeEnd)
        {
            int n = Mathf.Max(1, (int)(seconds * rate));
            float[] data = new float[n];
            for (int i = 0; i < n; i++) data[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
            if (fadeEnd) // 5 ms fade-out removes the click at the end of a clip
            {
                int fade = Mathf.Min(n, rate / 200);
                for (int i = 0; i < fade; i++) data[n - 1 - i] *= i / (float)fade;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        static AudioClip Make(string name, float seconds, Func<float, float> f) { return Make(name, seconds, f, Rate, true); }

        public static AudioClip Create(SfxId id)
        {
            switch (id)
            {
                case SfxId.UiClick: return Make("click", 0.08f, t => Sin(900f, t) * Decay(t, 45f) * 0.6f);
                case SfxId.UiError: return Make("error", 0.25f, t => (Square(150f, t) + Square(157f, t)) * 0.2f * Decay(t, 6f));
                case SfxId.Deploy: return Make("deploy", 0.3f, t => (Chirp(160f, 55f, 0.3f, t) * 0.8f + Noise() * 0.25f * Decay(t, 40f)) * Decay(t, 8f));
                case SfxId.Spawn: return Make("spawn", 0.16f, t => Chirp(300f, 650f, 0.16f, t) * Decay(t, 14f) * 0.5f);
                case SfxId.SwordHit:
                    return Make("sword", 0.2f, t => Noise() * Decay(t, 40f) * 0.55f + Sin(190f, t) * Decay(t, 35f) * 0.5f + Sin(1750f, t) * Decay(t, 22f) * 0.18f);
                case SfxId.ArrowShot:
                    {
                        float prev = 0f;
                        return Make("arrow", 0.22f, t => { float n = Noise(); float hp = n - prev; prev = n; return hp * 0.45f * Mathf.Sin(Mathf.PI * t / 0.22f); });
                    }
                case SfxId.MagicShot: return Make("magic", 0.32f, t => (Chirp(500f, 1300f, 0.32f, t) * 0.5f + Sin(1900f, t) * 0.15f) * Decay(t, 7f));
                case SfxId.Zap: return Make("zap", 0.28f, t => (Saw(240f + Noise() * 90f, t) * 0.35f + Noise() * 0.3f) * Decay(t, 11f));
                case SfxId.Explosion:
                    {
                        float lp = 0f;
                        return Make("boom", 0.8f, t => { lp += (Noise() - lp) * 0.12f; return (lp * 2.2f + Chirp(90f, 35f, 0.8f, t) * 0.8f) * Decay(t, 4.5f); });
                    }
                case SfxId.Heal: return Make("heal", 0.75f, t => (Chime(523f, 0f, t) + Chime(659f, 0.11f, t) + Chime(784f, 0.22f, t) + Chime(1047f, 0.33f, t)) * 0.3f);
                case SfxId.Freeze: return Make("freeze", 0.5f, t => (Sin(2300f + 300f * Sin(35f, t), t) * 0.3f + Noise() * 0.12f) * Decay(t, 7f));
                case SfxId.Stun: return Make("stun", 0.35f, t => (Sin(660f, t) * Decay(t, 9f) + (t > 0.12f ? Sin(880f, t - 0.12f) * Decay(t - 0.12f, 9f) : 0f)) * 0.4f);
                case SfxId.Shield: return Make("shield", 0.3f, t => Sin(1100f + 40f * Sin(24f, t), t) * Decay(t, 9f) * 0.4f);
                case SfxId.Whoosh:
                    {
                        float prev = 0f;
                        return Make("whoosh", 0.35f, t => { float n = Noise(); float hp = n - prev; prev = n; return hp * 0.4f * Mathf.Sin(Mathf.PI * t / 0.35f); });
                    }
                case SfxId.TowerShot: return Make("tshot", 0.3f, t => (Chirp(230f, 70f, 0.3f, t) * 0.7f + Noise() * 0.3f * Decay(t, 30f)) * Decay(t, 9f));
                case SfxId.TowerHit: return Make("thit", 0.16f, t => (Sin(95f, t) * 0.8f + Noise() * 0.3f * Decay(t, 50f)) * Decay(t, 22f));
                case SfxId.TowerDestroyed:
                    {
                        float lp = 0f;
                        return Make("tdestroy", 1.5f, t =>
                        {
                            lp += (Noise() - lp) * 0.08f;
                            float crumble = Noise() * (Noise() > 0.55f ? 1f : 0.2f) * 0.25f;
                            return (lp * 2.5f + Sin(48f, t) * 0.7f + crumble) * Decay(t, 2.4f);
                        });
                    }
                case SfxId.UnitDeath: return Make("death", 0.28f, t => (Chirp(420f, 110f, 0.28f, t) * 0.5f + Noise() * 0.2f * Decay(t, 25f)) * Decay(t, 9f));
                case SfxId.BattleStart:
                    return Make("horn", 1.0f, t =>
                    {
                        float env = Mathf.Min(1f, t / 0.08f) * Decay(Mathf.Max(0f, t - 0.5f), 4f);
                        return (Saw(220f, t) * 0.25f + Saw(330f, t) * 0.18f + Square(110f, t) * 0.12f) * env;
                    });
                case SfxId.Victory:
                    return Make("victory", 1.7f, t => Note(523.25f, 0f, t) + Note(659.25f, 0.16f, t) + Note(783.99f, 0.32f, t) + Note(1046.5f, 0.5f, t));
                case SfxId.Defeat:
                    return Make("defeat", 1.7f, t => Note(392f, 0f, t) + Note(349.23f, 0.3f, t) + Note(311.13f, 0.6f, t) + Note(261.63f, 0.95f, t));
                case SfxId.Tick: return Make("tick", 0.06f, t => Sin(1300f, t) * Decay(t, 70f) * 0.5f);
                default: return null;
            }
        }

        /// <summary>A short looping battle theme (Am - F - C - G) built from sines, kick and hats. ~8.7 s.</summary>
        public static AudioClip MakeMusic()
        {
            const int mRate = 22050;
            float beat = 60f / 110f;
            float total = beat * 16f;
            float[] roots = { 110f, 87.31f, 130.81f, 98f };
            float[][] arps =
            {
                new[] { 220f, 261.63f, 329.63f, 261.63f },
                new[] { 174.61f, 220f, 261.63f, 220f },
                new[] { 261.63f, 329.63f, 392f, 329.63f },
                new[] { 196f, 246.94f, 293.66f, 246.94f }
            };
            return Make("music", total, t =>
            {
                float beatPos = t / beat;
                int bar = ((int)(beatPos / 4f)) % 4;
                float inBar = beatPos - bar * 4f;

                float bt = (inBar - Mathf.Floor(inBar)) * beat;                 // time since the beat started
                float kick = Chirp(110f, 42f, 0.16f, bt) * Decay(bt, 14f) * 0.5f;

                float e = inBar * 2f;                                            // eighth-note position
                int e8 = (int)e;
                float et = (e - Mathf.Floor(e)) * beat * 0.5f;                   // time since the eighth started
                float hat = (e8 % 2 == 1) ? Noise() * Decay(et, 60f) * 0.1f : 0f;

                float bassHz = roots[bar] * ((e8 % 4 == 2) ? 2f : 1f);
                float attack = Mathf.Min(1f, et / 0.004f);
                float bass = (Sin(bassHz, et) + Sin(bassHz * 2f, et) * 0.35f) * Decay(et, 6f) * 0.24f * attack;

                float ahz = arps[bar][e8 % 4] * 2f;
                float arp = (Sin(ahz, et) * 0.7f + Sin(ahz * 2f, et) * 0.15f) * Decay(et, 7f) * 0.12f * attack;
                return kick + hat + bass + arp;
            }, mRate, false);
        }
    }
}
