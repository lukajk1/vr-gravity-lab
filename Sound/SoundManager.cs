using UnityEngine;
using UnityEngine.Audio;

public class SoundManager : MonoBehaviour
{
    private static SoundManager i;

    public AudioSource SoundObject;
    public AudioMixerGroup SFXMixer;
    public AudioMixerGroup MusicMixer;
    public AudioMixerGroup EnvMixer;

    private static float volumeVariance = 0.15f;
    private static float pitchVariance = 0.1f;


    private void Awake()
    {
        i = this;
    }
    public static void Play(SoundData sound)
    {
        if (sound.clip == null)
        {
            Debug.LogError("soundclip was null!");
            return;
        }

        AudioSource audioSource = Instantiate(i.SoundObject, sound.soundPos, Quaternion.identity);

        if (sound.varyVolume)
        {
            float randVolume = Random.Range(sound.volume - volumeVariance, sound.volume + volumeVariance);
            audioSource.volume = randVolume;
        }

        if (sound.varyPitch)
        {
            float randPitch = Random.Range(1 - pitchVariance, 1 + pitchVariance);
            audioSource.pitch = randPitch;
        }

        //audioSource.pitch *= Game.;

        if (sound.soundBlend == SoundData.SoundBlend.Spatial)
        {
            audioSource.spatialBlend = 1f;
            audioSource.minDistance = sound.minDist;
            audioSource.maxDistance = sound.maxDist;
        }
        else
        {
            audioSource.spatialBlend = 0f;
        }

        switch (sound.type)
        {
            case SoundData.Type.Music:
                audioSource.outputAudioMixerGroup = i.MusicMixer;
                break;
            case SoundData.Type.SFX:
                audioSource.outputAudioMixerGroup = i.SFXMixer;
                break;
            case SoundData.Type.Env:
                audioSource.outputAudioMixerGroup = i.EnvMixer;
                break;
        }

        audioSource.clip = sound.clip;
        audioSource.loop = sound.isLooping;
        audioSource.Play();
    }

}