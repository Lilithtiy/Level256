using System;
using UnityEngine;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager audioManagerInstance { get; private set; }

    [SerializeField] private AudioSource backgroundMusicSource;
    [SerializeField] private AudioSource sfxSourcePrefab;

    [SerializeField] private int poolSize = 10;

    private readonly List<AudioSource> audioPool = new List<AudioSource>();

    private void Awake()
    {
        if (audioManagerInstance == null)
        {
            audioManagerInstance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        // initial AudioSource pool
        for (int i = 0; i < poolSize; i++)
        {
            CreateAudioSource();
        }
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
    }
}
