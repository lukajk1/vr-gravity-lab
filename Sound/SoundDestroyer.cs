using System.Collections;
using UnityEngine;

public class SoundDestroyer : MonoBehaviour
{

    private AudioSource audioSource;
    private float clipLength;

    private void Awake()
    {
        audioSource = GetComponent<AudioSource>();
    }

    private IEnumerator Start()
    {
        clipLength = audioSource.clip.length;

        yield return new WaitForSeconds(clipLength + 1f);

        Destroy(gameObject); // return to object pool in future
    }

}