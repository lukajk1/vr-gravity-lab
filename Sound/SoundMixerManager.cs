using UnityEngine;
using UnityEngine.Audio;

public class SoundMixerManager : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;

    public static SoundMixerManager i;

    private void Awake()
    {
        if (i == null) i = this;
    }
    public static void SetMasterVolume(float volume)
    {
        if (volume <= 0.02f) i.audioMixer.SetFloat("MasterVol", -80f); // -80 is effectively muted
        else i.audioMixer.SetFloat("MasterVol", Mathf.Log10(volume) * 20f);
    }
    public static void SetSFXVolume(float volume)
    {
        if (volume <= 0.02f) i.audioMixer.SetFloat("SFXVol", -80f);
        else i.audioMixer.SetFloat("SFXVol", Mathf.Log10(volume) * 20f);
    }
    public static void SetMusicVolume(float volume)
    {
        if (volume <= 0.02f) i.audioMixer.SetFloat("MusicVol", -80f);
        else i.audioMixer.SetFloat("MusicVol", Mathf.Log10(volume) * 20f);
    }
    public static void SetEnvVolume(float volume)
    {
        if (volume <= 0.02f) i.audioMixer.SetFloat("EnvVol", -80f);
        else i.audioMixer.SetFloat("EnvVol", Mathf.Log10(volume) * 20f);
    }

    public static void SetVolume(VolumeControl.VolType volType, float volume)
    {
        switch (volType)
        {
            case VolumeControl.VolType.Master:
                SetMasterVolume(volume);
                break;
            case VolumeControl.VolType.Music:
                SetMusicVolume(volume);
                break;
            case VolumeControl.VolType.SFX:
                SetSFXVolume(volume);
                break;
            case VolumeControl.VolType.Ambient:
                SetEnvVolume(volume);
                break;
        }
    }

}
