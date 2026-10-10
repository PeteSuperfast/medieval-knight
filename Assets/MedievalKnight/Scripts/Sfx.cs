using UnityEngine;

namespace MedievalKnight
{
    public enum Snd { Swing, Slice, ArmorImpact, Clash, Grunt, Ready }

    // All sound: six clips from Resources/Audio played through a small pool of 2D AudioSources.
    // Every play gets a slight random pitch / volume, each clip has a minimum gap between plays and the
    // pool is the hard limit on overlapping voices. M cycles the master volume (100% > 60% > 30% > muted).
    // The variation uses its own System.Random so sound never disturbs the gameplay random numbers.
    public class Sfx : MonoBehaviour
    {
        public static Sfx I;

        public const int Voices = 8;
        public static readonly string[] Files = { "sword_swing", "sword_slice", "armor_impact", "sword_clash", "knight_grunt", "knight_ready" };
        static readonly float[] BaseVolume = { 0.55f, 0.75f, 0.7f, 0.8f, 0.6f, 0.85f };
        static readonly float[] PitchVar = { 0.12f, 0.10f, 0.08f, 0.08f, 0.06f, 0.02f };
        static readonly float[] MinGap = { 0.06f, 0.07f, 0.07f, 0.1f, 0.9f, 3f };      // seconds between two plays of the same clip
        static readonly float[] VolumeSteps = { 1f, 0.6f, 0.3f, 0f };
        const float LightGruntChance = 0.25f, LightGruntGap = 1.8f;

        public bool logPlays;                         // the self-test turns this on: one log line per played clip
        public readonly AudioClip[] clips = new AudioClip[6];
        public readonly int[] played = new int[6];
        public AudioSource LastSource { get; private set; }
        public int VolumeStep { get; private set; }
        public float Volume { get { return VolumeSteps[VolumeStep]; } }
        public string VolumeLabel { get { return Volume <= 0f ? "MUTED" : Mathf.RoundToInt(Volume * 100f) + "%"; } }

        readonly AudioSource[] voices = new AudioSource[Voices];
        readonly float[] lastTime = { -10f, -10f, -10f, -10f, -10f, -10f };
        readonly System.Random rng = new System.Random();
        int nextVoice;

        void Awake()
        {
            I = this;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i] = Resources.Load<AudioClip>("Audio/" + Files[i]);
                AudioClip c = clips[i];
                if (c == null) { Debug.LogError("Sfx: Resources/Audio/" + Files[i] + " missing"); continue; }
                c.LoadAudioData();                    // decode now, so the first swing does not hitch or play late
                Debug.Log("SFX loaded " + Files[i] + ": length " + c.length.ToString("0.000") + "s, " + c.samples + " samples, " + c.channels + " ch, "
                    + c.frequency + " Hz, state " + c.loadState);
            }
            for (int i = 0; i < Voices; i++)
            {
                AudioSource a = gameObject.AddComponent<AudioSource>();
                a.playOnAwake = false;
                a.loop = false;
                a.spatialBlend = 0f;                  // plain 2D: the camera is far above the knight
                voices[i] = a;
            }
            AudioListener.volume = Volume;
        }

        float Rand(float range) { return ((float)rng.NextDouble() * 2f - 1f) * range; }

        public int Count(Snd s) { return played[(int)s]; }

        // Returns false when the clip is missing or was played too recently.
        public bool Play(Snd s, float volume = 1f, float pitch = 1f)
        {
            int i = (int)s;
            AudioClip c = clips[i];
            float now = Time.unscaledTime;
            if (c == null || now - lastTime[i] < MinGap[i]) return false;
            lastTime[i] = now;

            // a free voice if there is one, otherwise the oldest one is cut off
            AudioSource a = null;
            for (int n = 0; n < Voices && a == null; n++)
            {
                AudioSource v = voices[(nextVoice + n) % Voices];
                if (!v.isPlaying) a = v;
            }
            if (a == null) a = voices[nextVoice];
            nextVoice = (System.Array.IndexOf(voices, a) + 1) % Voices;

            a.Stop();
            a.clip = c;
            a.pitch = pitch * (1f + Rand(PitchVar[i]));
            a.volume = Mathf.Clamp01(BaseVolume[i] * volume * (1f + Rand(0.1f)));
            a.Play();
            LastSource = a;
            played[i]++;
            if (logPlays) Debug.Log("SFX play " + Files[i] + " #" + played[i] + " t=" + now.ToString("0.00") + " vol " + a.volume.ToString("0.00")
                + " pitch " + a.pitch.ToString("0.00") + " playing=" + a.isPlaying + " master " + VolumeLabel);
            return true;
        }

        // The knight's attack voice. Heavy hits and skills always grunt (still at most once per 0.9 s);
        // light swings only now and then, so a fast combo does not turn into constant shouting.
        // volume < 1: another player's knight (quieter, see Game.RemoteVolume).
        public bool Grunt(bool heavy, float volume = 1f)
        {
            if (!heavy)
            {
                if (Time.unscaledTime - lastTime[(int)Snd.Grunt] < LightGruntGap) return false;
                if (rng.NextDouble() >= LightGruntChance) return false;
            }
            return Play(Snd.Grunt, (heavy ? 1f : 0.75f) * volume, heavy ? 0.96f : 1.04f);
        }

        // M key: 100% > 60% > 30% > muted > 100% ...
        public void CycleVolume()
        {
            VolumeStep = (VolumeStep + 1) % VolumeSteps.Length;
            AudioListener.volume = Volume;
        }

        public int PlayingVoices()
        {
            int n = 0;
            foreach (AudioSource v in voices) if (v.isPlaying) n++;
            return n;
        }
    }
}
