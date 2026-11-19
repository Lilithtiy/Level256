using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public enum MusicState
    {
        MainMenu,
        Lobby,
        Ambient,
        Chase
    }

    public static AudioManager audioManagerInstance { get; private set; }

    [SerializeField] private AudioSource sfxSourcePrefab;

    [SerializeField] private int poolSize = 10;

    [Header("Music")]
    [SerializeField] private AudioSource musicSourceA;
    [SerializeField] private AudioSource musicSourceB;

    [SerializeField] private ScriptableAudio mainMenuMusic;
    [SerializeField] private ScriptableAudio lobbyMusic;
    [SerializeField] private ScriptableAudio ambientMusic;
    [SerializeField] private ScriptableAudio chaseMusic;

    [SerializeField] private float musicFadeDuration = 3f;
    private AudioSource currentMusicSource;
    private MusicState? currentMusicState;
    private Coroutine musicFadeCoroutine;

    [Header("Dynamic Music")]
    [SerializeField] private float intensityTransitionSpeed = 1.5f;

    private AudioSource gameplayAmbientSource;
    private AudioSource gameplayChaseSource;

    private float targetMusicIntensity;
    private float currentMusicIntensity;

    private bool gameplayMusicActive;
    private bool gameplayMusicReady;

    private readonly List<AudioSource> audioPool = new List<AudioSource>();

    private void Awake()
    {
        if (audioManagerInstance != null && audioManagerInstance != this)
        {
            Destroy(gameObject);
            return;
        }

        audioManagerInstance = this;
        DontDestroyOnLoad(gameObject);

        // Prepare music sources
        SetupMusicSource(musicSourceA);
        SetupMusicSource(musicSourceB);

        // Initial AudioSource pool
        for (int i = 0; i < poolSize; i++)
        {
            CreateAudioSource();
        }
    }

    private void SetupMusicSource(AudioSource source)
    {
        if (source == null)
        {
            Debug.LogError("Music source is not assigned.");
            return;
        }
        source.loop = true;
        source.playOnAwake = false;
        source.volume = 0f; // Start with volume at 0 for fade-in
        source.Stop();
    }

    private AudioSource CreateAudioSource()
    {
        AudioSource newSource = Instantiate(sfxSourcePrefab, transform);
        newSource.gameObject.SetActive(false);

        audioPool.Add(newSource);
        return newSource;
    }

    private AudioSource GetAvailableSource()
    {
        foreach (AudioSource source in audioPool)
        {
            if (source == null)
                continue;

            if (!source.isPlaying)
            {
                source.gameObject.SetActive(true);
                return source;
            }
        }

        // Expand the pool if every source is occupied.
        AudioSource newSource = CreateAudioSource();
        newSource.gameObject.SetActive(true);

        return newSource;
    }

    //if you want to play a 2D sound effect
    public void PlayLocal(ScriptableAudio audio)
    {
        Play(audio, Vector3.zero, false);
    }

    //if you want to play a 3D sound effect
    public void PlayAt(ScriptableAudio audio, Vector3 position)
    {
        Play(audio, position, true);
    }

    public AudioSource PlayAttached(ScriptableAudio audioEvent, Transform target)
    {
        if (audioEvent == null || target == null)
            return null;

        AudioClip clip = audioEvent.GetRandomClip();

        if (clip == null)
            return null;

        AudioSource source = GetAvailableSource();

        // Parent the AudioSource to the target object.
        source.transform.SetParent(target);
        source.transform.localPosition = Vector3.zero;

        source.clip = clip;
        source.volume = audioEvent.volume;
        source.pitch = audioEvent.GetPitch();
        source.loop = true;

        source.spatialBlend = audioEvent.spatialBlend;
        source.minDistance = audioEvent.minDistance;
        source.maxDistance = audioEvent.maxDistance;
        source.outputAudioMixerGroup = audioEvent.mixerGroup;

        source.Play();

        return source;
    }

    public void StopAttached(AudioSource source)
    {
        if (source == null)
            return;

        source.Stop();
        source.clip = null;
        source.loop = false;

        source.transform.SetParent(transform);
        source.transform.localPosition = Vector3.zero;
        source.gameObject.SetActive(false);
    }

    private void Play(ScriptableAudio audio, Vector3 position, bool spatial)
    {
        if (audio == null)
            return;

        AudioClip clip = audio.GetRandomClip();

        if (clip == null)
            return;

        AudioSource source = GetAvailableSource();

        // Set the position of the AudioSource for 3D sounds
        source.transform.position = position;

        // Set AudioSource properties based on ScriptableAudio settings
        source.clip = clip;
        source.volume = audio.volume;
        source.pitch = audio.GetPitch();
        source.loop = audio.looping;

        source.spatialBlend = spatial ? audio.spatialBlend : 0f;
        source.minDistance = audio.minDistance;
        source.maxDistance = audio.maxDistance;

        source.outputAudioMixerGroup = audio.mixerGroup;

        // Play the audio
        source.Play();
    }

    private void Update()
    {
        // Iterate backwards so destroyed references can be removed safely
        for (int i = audioPool.Count - 1; i >= 0; i--)
        {
            AudioSource source = audioPool[i];

            // Remove references to AudioSources that have been destroyed.
            if (source == null)
            {
                audioPool.RemoveAt(i);
                continue;
            }

            // Deactivate finished AudioSources.
            if (!source.isPlaying && source.gameObject.activeSelf)
            {
                source.gameObject.SetActive(false);
            }
        }

        // Handle dynamic gameplay music.
        if (gameplayMusicActive && gameplayMusicReady)
        {
            currentMusicIntensity = Mathf.MoveTowards(currentMusicIntensity, targetMusicIntensity, intensityTransitionSpeed * Time.unscaledDeltaTime);

            // Ambient fades out as chase music fades in.
            gameplayAmbientSource.volume =
                ambientMusic.volume * (1f - currentMusicIntensity);

            gameplayChaseSource.volume =
                chaseMusic.volume * currentMusicIntensity;
        }
    }

    public void PlayMusic(MusicState state)
    {
        // Gameplay uses two simultaneous music tracks.
        if (state == MusicState.Ambient)
        {
            StartGameplayMusic();
            return;
        }

        if (state == MusicState.Chase)
        {
            StartGameplayMusic();
            SetMusicIntensity(1f);
            return;
        }

        // We're leaving gameplay music.
        if (gameplayMusicActive)
        {
            gameplayMusicActive = false;
            gameplayMusicReady = false;

            if (gameplayChaseSource != null)
            {
                gameplayChaseSource.Stop();
                gameplayChaseSource.clip = null;
                gameplayChaseSource.volume = 0f;
            }

            targetMusicIntensity = 0f;
            currentMusicIntensity = 0f;
        }

        Debug.Log($"Switching music to: {state}");

        // Find the corresponding music asset
        ScriptableAudio music = state switch
        {
            MusicState.MainMenu => mainMenuMusic,
            MusicState.Lobby => lobbyMusic,
            MusicState.Ambient => ambientMusic,
            MusicState.Chase => chaseMusic,
            _ => null
        };

        if (music == null)
        {
            Debug.LogWarning($"No music assigned for state: {state}");
            return;
        }

        Debug.Log($"Selected music: {music.name}, Volume: {music.volume}");

        if (musicSourceA == null || musicSourceB == null)
        {
            Debug.LogWarning("Music AudioSources have not been assigned!");
            return;
        }

        // Don't restart music if we're already playing this state
        if (currentMusicState == state && currentMusicSource != null && currentMusicSource.isPlaying)
        {
            return;
        }

        AudioClip clip = music.GetRandomClip();

        if (clip == null) return;

        // Alternate between the two AudioSources
        AudioSource nextSource = currentMusicSource == musicSourceA ? musicSourceB : musicSourceA;

        AudioSource previousSource = currentMusicSource;

        // Stop an ongoing transition if there is one
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
        }

        // Prepare the new music source
        nextSource.Stop();
        nextSource.clip = clip;
        nextSource.loop = true;
        nextSource.volume = 0f;
        nextSource.pitch = music.GetPitch();
        nextSource.spatialBlend = 0f;
        nextSource.outputAudioMixerGroup = music.mixerGroup;

        nextSource.Play();

        currentMusicSource = nextSource;
        currentMusicState = state;

        // Fade between the old and new music
        musicFadeCoroutine = StartCoroutine(CrossfadeMusic(previousSource, nextSource, music.volume));
    }

    private void StartGameplayMusic()
    {
        if (gameplayMusicActive) return;

        if (ambientMusic == null || chaseMusic == null)
        {
            Debug.LogWarning("Ambient or chase music is missing!");
            return;
        }

        // Stop any ongoing menu/lobby crossfade.
        if (musicFadeCoroutine != null)
        {
            StopCoroutine(musicFadeCoroutine);
            musicFadeCoroutine = null;
        }

        // Assign one source to each gameplay track.
        gameplayAmbientSource = musicSourceA;
        gameplayChaseSource = musicSourceB;

        // Get both clips.
        AudioClip ambientClip = ambientMusic.GetRandomClip();
        AudioClip chaseClip = chaseMusic.GetRandomClip();

        if (ambientClip == null || chaseClip == null)
        {
            Debug.LogWarning("Failed to get gameplay music clips!");
            return;
        }

        // Configure ambient music.
        gameplayAmbientSource.Stop();
        gameplayAmbientSource.clip = ambientClip;
        gameplayAmbientSource.loop = true;
        gameplayAmbientSource.pitch = ambientMusic.GetPitch();
        gameplayAmbientSource.spatialBlend = 0f;
        gameplayAmbientSource.outputAudioMixerGroup = ambientMusic.mixerGroup;
        gameplayAmbientSource.volume = ambientMusic.volume;

        // Configure chase music.
        gameplayChaseSource.Stop();
        gameplayChaseSource.clip = chaseClip;
        gameplayChaseSource.loop = true;
        gameplayChaseSource.pitch = chaseMusic.GetPitch();
        gameplayChaseSource.spatialBlend = 0f;
        gameplayChaseSource.outputAudioMixerGroup = chaseMusic.mixerGroup;
        gameplayChaseSource.volume = 0f;

        // Start both tracks.
        gameplayAmbientSource.Play();
        gameplayChaseSource.Play();

        currentMusicSource = gameplayAmbientSource;
        currentMusicState = MusicState.Ambient;

        // Enable dynamic music immediately.
        currentMusicIntensity = 0f;
        targetMusicIntensity = 0f;

        gameplayMusicActive = true;
        gameplayMusicReady = true;

        Debug.Log($"Gameplay music started. Ambient: {ambientClip.name}, Chase: {chaseClip.name}");
    }

    private IEnumerator CrossfadeMusic(AudioSource previousSource, AudioSource nextSource, float targetVolume)
    {
        float previousVolume = previousSource != null ? previousSource.volume : 0f;

        float elapsedTime = 0f;

        while (elapsedTime < musicFadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsedTime / musicFadeDuration);

            // Fade in the new music
            nextSource.volume = Mathf.Lerp(0f, targetVolume, progress);

            // Fade out the previous music
            if (previousSource != null)
            {
                previousSource.volume = Mathf.Lerp(previousVolume, 0f, progress);
            }
            yield return null;
        }

        // Make sure the final volumes are correct
        nextSource.volume = targetVolume;

        if (previousSource != null)
        {
            previousSource.Stop();
            Debug.Log("Previous music stopped!");
            previousSource.clip = null;
            previousSource.volume = 0f;
        }

        musicFadeCoroutine = null;

        // Gameplay music can now be controlled by intensity.
        if (gameplayMusicActive)
        {
            gameplayMusicReady = true;
        }
    }

    public void SetMusicIntensity(float intensity)
    {
        targetMusicIntensity = Mathf.Clamp01(intensity);
    }
}
