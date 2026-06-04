using UnityEngine;

public class PitchDebug : MonoBehaviour
{
    public AudioPitchEstimator estimator; // your GitHub script
    public AudioSource micSource;         // the microphone AudioSource

    void Update()
    {
        if (micSource == null || estimator == null) return;

        float pitch = estimator.Estimate(micSource);

        if (float.IsNaN(pitch))
            Debug.Log("Pitch: ---");
        else
            Debug.Log("Pitch: " + pitch.ToString("F2") + " Hz");
    }
}