using UnityEngine;

namespace SlotMachine.Audio
{
    /// <summary>
    /// Optional sound feedback. No audio files were supplied with the assignment, so any clip left
    /// empty is replaced by a short procedurally generated tone. Assign real clips in the Inspector
    /// to override them.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [SerializeField] private AudioSource source;
        [SerializeField] private AudioClip clickClip;
        [SerializeField] private AudioClip spinStartClip;
        [SerializeField] private AudioClip reelStopClip;
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip loseClip;
        [SerializeField] private AudioClip errorClip;
        [SerializeField] private bool generateFallbackTones = true;
        [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

        public bool IsEnabled { get; private set; } = true;

        public void SetEnabled(bool enabled)
        {
            IsEnabled = enabled;
            if (!enabled && source != null)
                source.Stop();
        }

        public void PlayClick() => Play(clickClip);
        public void PlaySpinStart() => Play(spinStartClip);
        public void PlayReelStop() => Play(reelStopClip);
        public void PlayWin() => Play(winClip);
        public void PlayLose() => Play(loseClip);
        public void PlayError() => Play(errorClip);

        private void Awake()
        {
            if (source == null)
                source = GetComponent<AudioSource>();
            source.playOnAwake = false;

            if (!generateFallbackTones)
                return;

            if (clickClip == null) clickClip = Tone("click", new[] { 1400f }, 0.035f, 0.5f);
            if (spinStartClip == null) spinStartClip = Sweep("spin", 220f, 660f, 0.25f, 0.35f);
            if (reelStopClip == null) reelStopClip = Tone("reelStop", new[] { 140f }, 0.09f, 0.8f);
            if (winClip == null) winClip = Tone("win", new[] { 523.25f, 659.25f, 783.99f, 1046.5f }, 0.12f, 0.45f);
            if (loseClip == null) loseClip = Tone("lose", new[] { 330f, 247f }, 0.14f, 0.35f);
            if (errorClip == null) errorClip = Tone("error", new[] { 110f, 110f }, 0.1f, 0.5f);
        }

        private void Play(AudioClip clip)
        {
            if (!IsEnabled || clip == null || source == null)
                return;
            source.PlayOneShot(clip, volume);
        }

        /// <summary>Sequence of decaying square-ish notes.</summary>
        private static AudioClip Tone(string clipName, float[] frequencies, float noteLength, float amplitude)
        {
            int noteSamples = Mathf.CeilToInt(noteLength * SampleRate);
            var data = new float[noteSamples * frequencies.Length];
            for (int n = 0; n < frequencies.Length; n++)
            {
                for (int i = 0; i < noteSamples; i++)
                {
                    float t = (float)i / SampleRate;
                    float envelope = Mathf.Exp(-5f * i / noteSamples) * Mathf.Min(1f, i / 80f);
                    float wave = Mathf.Sin(2f * Mathf.PI * frequencies[n] * t);
                    wave = Mathf.Clamp(wave * 1.8f, -1f, 1f);
                    data[n * noteSamples + i] = wave * envelope * amplitude;
                }
            }

            return Create(clipName, data);
        }

        private static AudioClip Sweep(string clipName, float fromHz, float toHz, float length, float amplitude)
        {
            int samples = Mathf.CeilToInt(length * SampleRate);
            var data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float k = (float)i / samples;
                phase += 2f * Mathf.PI * Mathf.Lerp(fromHz, toHz, k) / SampleRate;
                float envelope = Mathf.Sin(Mathf.PI * k);
                data[i] = Mathf.Sin(phase) * envelope * amplitude;
            }

            return Create(clipName, data);
        }

        private static AudioClip Create(string clipName, float[] data)
        {
            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
