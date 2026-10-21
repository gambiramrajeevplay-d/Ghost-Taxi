
using System.Collections;
using UnityEngine;

public class RandomSoundPlayer : MonoBehaviour
{
    [Header("Audio Settings")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip[] soundClips = new AudioClip[4];

    [Header("Playback Settings")]
    [SerializeField] private float delayAfterClip = 5f;
    [SerializeField] private bool playOnStart = true;

    private Coroutine playRoutine;

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        if (playOnStart)
            playRoutine = StartCoroutine(PlayRandomSounds());
    }

    private IEnumerator PlayRandomSounds()
    {
        while (true)
        {
            if (audioSource == null || soundClips.Length == 0)
                yield break;

            AudioClip clip = soundClips[Random.Range(0, soundClips.Length)];

            if (clip == null)
            {
                yield return new WaitForSeconds(delayAfterClip);
                continue;
            }

            audioSource.PlayOneShot(clip);

            // Wait until this one-shot clip finishes.
            yield return new WaitForSeconds(clip.length);

            // Wait another 5 seconds before playing the next clip.
            yield return new WaitForSeconds(delayAfterClip);
        }
    }

    public void StopSounds()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        if (audioSource != null)
            audioSource.Stop();
    }
}
