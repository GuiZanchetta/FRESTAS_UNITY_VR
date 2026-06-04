using UnityEngine;
using TMPro;

public class FPSDisplay3D : MonoBehaviour
{
    private TMP_Text tmp;
    private float timeLeft = 0.5f;
    private int frames;
    private float accum;

    void Start()
    {
        tmp = GetComponent<TMP_Text>();
    }

    void Update()
    {
        timeLeft -= Time.unscaledDeltaTime;
        accum += Time.timeScale / Time.unscaledDeltaTime;
        frames++;

        if (timeLeft <= 0f)
        {
            float fps = accum / frames;
            tmp.text = $"FPS: {fps:F1}";
            timeLeft = 1.0f;
            accum = 0f;
            frames = 0;
        }
    }
}
