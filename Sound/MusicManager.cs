using UnityEngine;
using System.Collections.Generic;

public class MusicManager : MonoBehaviour
{
    [SerializeField] public AudioClip lofiSong1;
    [SerializeField] public AudioClip sadPiano;

    private AudioClip activeSong = null;

    public static MusicManager i;

    private void Awake()
    {
        i = this;
    }

    private void Start()
    {
        Play(lofiSong1);
    }

    public void Play(AudioClip song)
    {
        if (activeSong != song)
        {
            SoundManager.Play(new SoundData(song, isLooping: true, type: SoundData.Type.Music, varyPitch: false, varyVolume: false));
            activeSong = song;
        }
    }

}
