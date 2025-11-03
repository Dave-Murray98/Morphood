using System.Collections;
using UnityEngine;

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Audio Sources")]
    [Tooltip("Audio source for the low intensity track")]
    [SerializeField] private AudioSource lowIntensitySource;
    [Tooltip("Audio source for the high intensity track")]
    [SerializeField] private AudioSource highIntensitySource;

    [Header("Audio Clips")]
    [Tooltip("Low intensity music track")]
    [SerializeField] private AudioClip lowIntensityClip;
    [Tooltip("High intensity music track")]
    [SerializeField] private AudioClip highIntensityClip;

    [Header("Volume Settings")]
    [Tooltip("Base volume level when a track is playing")]
    [Range(0f, 1f)]
    [SerializeField] private float baseVolume = 0.7f;
    [Tooltip("How long it takes to fade between tracks")]
    [SerializeField] private float fadeDuration = 2f;

    [Header("Debug")]
    [Tooltip("Show current intensity state in inspector")]
    [SerializeField] private bool isHighIntensity = false;

    // Fade state
    private Coroutine fadeCoroutine;
    private bool isFading = false;

    private void Awake()
    {
        // Singleton pattern
        if (Instance != null)
        {
            Debug.LogWarning("[MusicManager] Multiple MusicManager instances detected. Destroying duplicate.");
            Destroy(gameObject);
            return;
        }
        else
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep music manager across scenes
        }
    }

    private void Start()
    {
        SetupAudioSources();
        StartMusic();
    }

    /// <summary>
    /// Sets up the audio sources with proper settings
    /// </summary>
    private void SetupAudioSources()
    {
        if (lowIntensitySource == null || highIntensitySource == null)
        {
            Debug.LogError("[MusicManager] Audio sources are not assigned!");
            return;
        }

        // Assign audio clips
        if (lowIntensityClip != null)
        {
            lowIntensitySource.clip = lowIntensityClip;
        }
        else
        {
            Debug.LogWarning("[MusicManager] Low intensity audio clip is not assigned!");
        }

        if (highIntensityClip != null)
        {
            highIntensitySource.clip = highIntensityClip;
        }
        else
        {
            Debug.LogWarning("[MusicManager] High intensity audio clip is not assigned!");
        }

        // Configure both audio sources
        lowIntensitySource.loop = true;
        highIntensitySource.loop = true;
        lowIntensitySource.playOnAwake = false;
        highIntensitySource.playOnAwake = false;

        // Start with low intensity playing, high intensity silent
        lowIntensitySource.volume = baseVolume;
        highIntensitySource.volume = 0f;
    }

    /// <summary>
    /// Starts playing both music tracks
    /// </summary>
    public void StartMusic()
    {
        if (lowIntensitySource == null || highIntensitySource == null)
        {
            Debug.LogError("[MusicManager] Cannot start music - audio sources not assigned!");
            return;
        }

        if (lowIntensitySource.clip == null || highIntensitySource.clip == null)
        {
            Debug.LogError("[MusicManager] Cannot start music - audio clips not assigned!");
            return;
        }

        // Start both tracks at the same time to keep them synchronized
        lowIntensitySource.Play();
        highIntensitySource.Play();

        Debug.Log("[MusicManager] Music started");
    }

    /// <summary>
    /// Stops both music tracks
    /// </summary>
    public void StopMusic()
    {
        if (lowIntensitySource != null) lowIntensitySource.Stop();
        if (highIntensitySource != null) highIntensitySource.Stop();

        Debug.Log("[MusicManager] Music stopped");
    }

    /// <summary>
    /// Switches to low intensity music (out of round)
    /// </summary>
    public void SetLowIntensity()
    {
        if (isHighIntensity)
        {
            isHighIntensity = false;
            StartFade(lowIntensitySource, highIntensitySource);
        }
    }

    /// <summary>
    /// Switches to high intensity music (in round)
    /// </summary>
    public void SetHighIntensity()
    {
        if (!isHighIntensity)
        {
            isHighIntensity = true;
            StartFade(highIntensitySource, lowIntensitySource);
        }
    }

    /// <summary>
    /// Starts fading between the two audio sources
    /// </summary>
    /// <param name="fadeInSource">Audio source to fade in</param>
    /// <param name="fadeOutSource">Audio source to fade out</param>
    private void StartFade(AudioSource fadeInSource, AudioSource fadeOutSource)
    {
        // Stop any existing fade
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeCoroutine(fadeInSource, fadeOutSource));
    }

    /// <summary>
    /// Coroutine that handles the cross-fade between audio sources
    /// </summary>
    private IEnumerator FadeCoroutine(AudioSource fadeInSource, AudioSource fadeOutSource)
    {
        isFading = true;

        float elapsedTime = 0f;
        float startFadeInVolume = fadeInSource.volume;
        float startFadeOutVolume = fadeOutSource.volume;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / fadeDuration;

            // Fade in the target source
            fadeInSource.volume = Mathf.Lerp(startFadeInVolume, baseVolume, progress);

            // Fade out the other source
            fadeOutSource.volume = Mathf.Lerp(startFadeOutVolume, 0f, progress);

            yield return null;
        }

        // Ensure final volumes are exact
        fadeInSource.volume = baseVolume;
        fadeOutSource.volume = 0f;

        isFading = false;
        fadeCoroutine = null;

        Debug.Log($"[MusicManager] Faded to {(isHighIntensity ? "high" : "low")} intensity");
    }

    /// <summary>
    /// Updates the base volume for both tracks
    /// </summary>
    /// <param name="newBaseVolume">New base volume level</param>
    public void SetBaseVolume(float newBaseVolume)
    {
        baseVolume = Mathf.Clamp01(newBaseVolume);

        // Update the currently playing track's volume
        if (!isFading)
        {
            if (isHighIntensity)
            {
                highIntensitySource.volume = baseVolume;
            }
            else
            {
                lowIntensitySource.volume = baseVolume;
            }
        }
    }

    /// <summary>
    /// Gets the current intensity state
    /// </summary>
    public bool IsHighIntensity => isHighIntensity;

    private void OnDestroy()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Update volume in real-time while editing in inspector
        if (Application.isPlaying && !isFading)
        {
            if (isHighIntensity && highIntensitySource != null)
            {
                highIntensitySource.volume = baseVolume;
            }
            else if (!isHighIntensity && lowIntensitySource != null)
            {
                lowIntensitySource.volume = baseVolume;
            }
        }
    }
#endif
}