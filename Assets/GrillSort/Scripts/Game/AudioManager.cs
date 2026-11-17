using UnityEngine;

namespace GrillSort
{
    /// <summary>Plays one-shot SFX on AudioSources authored in the scene (no runtime-created sources).</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        public AudioSource sfx;
        public AudioSource loop;
        public bool Enabled = true;

        public void Play(AudioClip clip, float volume = 1f, float pitch = 1f)
        {
            if (!Enabled || clip == null || sfx == null) return;
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }
    }
}
