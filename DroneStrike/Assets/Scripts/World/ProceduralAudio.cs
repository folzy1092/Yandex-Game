using UnityEngine;

/// <summary>
/// Synthesises the game's sound effects as raw samples at runtime.
///
/// Same reasoning as the textures: no imported audio files means nothing to
/// license and almost nothing added to the WebGL download, which is what keeps
/// the game loading fast enough that players stay long enough to see an ad.
///
/// Every sound here is built from three ingredients — noise, sine tones, and
/// exponential envelopes that shape how fast each ingredient fades.
/// </summary>
public static class ProceduralAudio
{
    const int SampleRate = 44100;

    /// <summary>
    /// Gunshot: a sharp crack of filtered noise over a short low-frequency thump,
    /// followed by a quiet tail so it does not sound like it stops dead.
    /// </summary>
    public static AudioClip CreateGunshot(string name, float duration, float bodyFrequency,
                                          float brightness, int seed)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        Random.State previous = Random.state;
        Random.InitState(seed);

        float lowPassState = 0f;
        float highPassState = 0f;
        float previousNoise = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;
            float progress = (float)i / sampleCount;

            // The crack: white noise that dies almost instantly.
            float noise = Random.value * 2f - 1f;
            lowPassState += (noise - lowPassState) * brightness;
            highPassState = lowPassState - previousNoise;
            previousNoise = lowPassState;

            float crackEnvelope = Mathf.Exp(-t * 70f);
            float crack = highPassState * crackEnvelope * 0.9f;

            // The body: a low tone that drops in pitch as it fades, which is what
            // makes it read as a gunshot rather than a hiss.
            float sweep = bodyFrequency * (1f - progress * 0.55f);
            float bodyEnvelope = Mathf.Exp(-t * 26f);
            float body = Mathf.Sin(2f * Mathf.PI * sweep * t) * bodyEnvelope * 0.55f;

            // The tail: quiet filtered noise standing in for room reflections.
            float tailEnvelope = Mathf.Exp(-t * 9f) * 0.16f;
            float tail = lowPassState * tailEnvelope;

            samples[i] = Clip(crack + body + tail);
        }

        Random.state = previous;
        return Build(name, samples);
    }

    /// <summary>
    /// Bullet impact on hard surfaces: a short bright tick with a little ring to it.
    /// </summary>
    public static AudioClip CreateImpact(string name, float duration, float ringFrequency, int seed)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        Random.State previous = Random.state;
        Random.InitState(seed);

        float filterState = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            float noise = Random.value * 2f - 1f;
            filterState += (noise - filterState) * 0.55f;

            float tick = filterState * Mathf.Exp(-t * 120f) * 0.8f;
            float ring = Mathf.Sin(2f * Mathf.PI * ringFrequency * t) * Mathf.Exp(-t * 55f) * 0.35f;

            samples[i] = Clip(tick + ring);
        }

        Random.state = previous;
        return Build(name, samples);
    }

    /// <summary>
    /// Hitting a person: duller and lower than hitting concrete, no metallic ring.
    /// </summary>
    public static AudioClip CreateFleshHit(string name, float duration, int seed)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        Random.State previous = Random.state;
        Random.InitState(seed);

        float filterState = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            float noise = Random.value * 2f - 1f;
            // Heavy filtering is what takes the "brightness" out and makes it thuddy.
            filterState += (noise - filterState) * 0.08f;

            float thud = filterState * Mathf.Exp(-t * 40f) * 1.6f;
            float low = Mathf.Sin(2f * Mathf.PI * 95f * t) * Mathf.Exp(-t * 45f) * 0.4f;

            samples[i] = Clip(thud + low);
        }

        Random.state = previous;
        return Build(name, samples);
    }

    /// <summary>
    /// A mechanical click, used for the two halves of the reload.
    /// </summary>
    public static AudioClip CreateClick(string name, float duration, float frequency,
                                        float decay, int seed)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        Random.State previous = Random.state;
        Random.InitState(seed);

        float filterState = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            float noise = Random.value * 2f - 1f;
            filterState += (noise - filterState) * 0.75f;

            float envelope = Mathf.Exp(-t * decay);
            float metal = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope * 0.5f;
            float snap = filterState * Mathf.Exp(-t * decay * 2.5f) * 0.5f;

            samples[i] = Clip(metal + snap);
        }

        Random.state = previous;
        return Build(name, samples);
    }

    /// <summary>
    /// Footstep: a soft low-passed thump, quiet enough to sit under everything else.
    /// </summary>
    public static AudioClip CreateFootstep(string name, float duration, int seed)
    {
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        Random.State previous = Random.state;
        Random.InitState(seed);

        float filterState = 0f;

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            float noise = Random.value * 2f - 1f;
            filterState += (noise - filterState) * 0.12f;

            samples[i] = Clip(filterState * Mathf.Exp(-t * 55f) * 1.3f);
        }

        Random.state = previous;
        return Build(name, samples);
    }

    /// <summary>
    /// Dry click for pulling the trigger on an empty magazine.
    /// </summary>
    public static AudioClip CreateEmptyClick(string name, int seed)
    {
        return CreateClick(name, 0.06f, 2400f, 130f, seed);
    }

    /// <summary>
    /// A short rising two-tone chime, used when the player respawns.
    /// </summary>
    public static AudioClip CreateRespawnChime(string name)
    {
        const float duration = 0.35f;
        int sampleCount = Mathf.CeilToInt(SampleRate * duration);
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = (float)i / SampleRate;

            // Two notes, the second starting halfway through the first.
            float first = Mathf.Sin(2f * Mathf.PI * 523f * t) * Mathf.Exp(-t * 9f);
            float secondTime = Mathf.Max(0f, t - 0.12f);
            float second = t > 0.12f
                ? Mathf.Sin(2f * Mathf.PI * 784f * secondTime) * Mathf.Exp(-secondTime * 9f)
                : 0f;

            samples[i] = Clip((first + second) * 0.3f);
        }

        return Build(name, samples);
    }

    static float Clip(float value)
    {
        return Mathf.Clamp(value, -1f, 1f);
    }

    static AudioClip Build(string name, float[] samples)
    {
        var clip = AudioClip.Create(name, samples.Length, 1, SampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
