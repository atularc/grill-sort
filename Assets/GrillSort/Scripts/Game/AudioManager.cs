using UnityEngine;

namespace GrillSort
{
    /// <summary>
    /// Sound effects, background music and the grill sizzle ambience, all on AudioSources authored in the scene by the
    /// builder (nothing is created at runtime).
    /// Effects use a small pool of voices, one sound per voice, so each can have its own pitch: with one shared source,
    /// changing the pitch for a new sound also bent every sound still playing.
    /// </summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public AudioSource[] voices;
        public AudioSource music;
        public AudioSource ambience;
        [Range(0, 1)] public float musicVolume = .1f;   // user 2026-11-22: still too loud at .22 (only -2.7 dB); .1 = about a third of .3   // user 2026-11-21: "lower the music a little bit"
        [Range(0, 1)] public float ambienceVolume = .16f;
        [Tooltip("Soft continuous grill sizzle under the music while playing. Off (user 2026-11-21: the constant grilling sound wasn't good).")]
        public bool ambienceOn = false;

        // kept for older references (scene builds before 2026-11-21)
        public AudioSource sfx;
        public AudioSource loop;

        bool _sound = true, _music = true;
        int _next;
        float _musicTarget, _ambTarget;

        /// <summary>Effects (and the sizzle ambience) on/off.</summary>
        public bool Enabled { get => _sound; set { _sound = value; _ambTarget = value && _ambWanted ? ambienceVolume : 0f; } }
        public bool MusicEnabled { get => _music; set { _music = value; _musicTarget = value ? musicVolume : 0f; } }
        bool _ambWanted;

        /// <summary>Settings slider values 0..1 (user 2026-11-22). Music slider 1 = <see cref="MusicMax"/>.</summary>
        public float sfxVolume = 1f;
        public const float MusicMax = .3f;

        public void SetMusicSlider(float v)
        {
            musicVolume = Mathf.Clamp01(v) * MusicMax;
            _musicTarget = _music ? musicVolume : 0f;
            if (music != null && Mathf.Abs(music.volume - _musicTarget) > .001f) music.volume = _musicTarget;   // immediate while dragging
        }

        public void SetSfxSlider(float v) { sfxVolume = Mathf.Clamp01(v); }

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            volume *= sfxVolume;
            if (volume <= .001f) return;
            if (!_sound || clip == null || voices == null || voices.Length == 0) return;
            // take a free voice; if all are busy, reuse the oldest one in turn
            AudioSource v = null;
            for (int i = 0; i < voices.Length; i++)
            {
                var c = voices[(_next + i) % voices.Length];
                if (!c.isPlaying) { v = c; _next = (_next + i + 1) % voices.Length; break; }
            }
            if (v == null) { v = voices[_next]; _next = (_next + 1) % voices.Length; }
            v.clip = clip; v.pitch = pitch; v.volume = volume;
            v.Play();
        }

        /// <summary>Plays one of several variations at random, with a little pitch variety (less robotic repeats).</summary>
        public void PlayVariant(AudioClip[] clips, float volume = 1f, float pitch = 1f)
        {
            if (clips == null || clips.Length == 0) return;
            Play(clips[Random.Range(0, clips.Length)], volume, pitch * Random.Range(.96f, 1.04f));
        }

        public void StartMusic(AudioClip clip)
        {
            if (music == null || clip == null) return;
            if (music.clip != clip) { music.clip = clip; music.loop = true; music.volume = 0f; music.Play(); }
            else if (!music.isPlaying) music.Play();
            _musicTarget = _music ? musicVolume : 0f;
        }

        /// <summary>The soft grill sizzle under the music while trays are on screen.</summary>
        public void SetAmbience(AudioClip clip, bool on)
        {
            if (ambience == null) return;
            on &= ambienceOn;
            _ambWanted = on;
            if (on && clip != null && ambience.clip != clip) { ambience.clip = clip; ambience.loop = true; ambience.volume = 0f; ambience.Play(); }
            _ambTarget = on && _sound ? ambienceVolume : 0f;
        }

        void Update()
        {
            // smooth fades (also used when toggling in the pause menu)
            float dt = Time.unscaledDeltaTime;
            if (music != null) music.volume = Mathf.MoveTowards(music.volume, _musicTarget, dt * .6f);
            if (ambience != null)
            {
                ambience.volume = Mathf.MoveTowards(ambience.volume, _ambTarget, dt * .5f);
                if (ambience.clip != null && !ambience.isPlaying && _ambTarget > 0f) ambience.Play();
            }
        }
    }
}
