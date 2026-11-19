using UnityEngine;
using UnityEngine.Audio;

[CreateAssetMenu(fileName = "ScriptableAudio", menuName = "Scriptable Objects/ScriptableAudio")]
public class ScriptableAudio : ScriptableObject
{
    public AudioClip[] audioClips;

    [Header("Playback Settings")]
    [Range(0f, 1f)]
    public float volume = 1f;

    [Range(0.1f, 3f)]
    public float pitch = 1f;

    public bool randomPitch = false;

    [Header("Spatial Settings")]
    [Range(0f, 1f)]
    public float spatialBlend = 0f;

    public float minDistance = 1f;
    public float maxDistance = 15f;


    [Header("Other Settings")]
    public bool looping = false;

    public AudioMixerGroup mixerGroup;

    public AudioClip GetRandomClip()
    {
        if (audioClips.Length == 0 || audioClips == null) {return null;}

        int randomIndex = Random.Range(0, audioClips.Length);
        return audioClips[randomIndex];
    }

    public float GetPitch()
    {
        if (!randomPitch) { return pitch; }

        return pitch * Random.Range(0.9f, 1.1f);
    }

}
