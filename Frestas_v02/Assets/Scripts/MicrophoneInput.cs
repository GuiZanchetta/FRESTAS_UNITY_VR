using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class MicInput : MonoBehaviour
{
    public AudioSource audioSource;

    void Start()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("No microphone detected!");
            return;
        }

        string micDevice = Microphone.devices[0];
        audioSource.clip = Microphone.Start(micDevice, true, 10, 44100);
        audioSource.loop = true;
        audioSource.mute = true; // keeps audio from playing out loud

        // wait until microphone starts
        while (Microphone.GetPosition(micDevice) <= 0) {}
        audioSource.Play();
    }
}