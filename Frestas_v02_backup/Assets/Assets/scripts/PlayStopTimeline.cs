using UnityEngine;
using UnityEngine.Playables;

public class PlayStopTimeline : MonoBehaviour
{
    public PlayableDirector director;

    [Header("Finger Settings")]
    public Transform leftFingertip;
    public Transform rightFingertip;
    public float triggerDistance = 0.02f;

    [Header("Button Visual")]
    public float pressDepth = 0.01f;
    public float cooldown = 0.5f;

    [Header("Behavior")]
    public bool resetInsteadOfPlay = false; // toggle per button

    [Header("OSC")]
    public string buttonId = "playstop";
    public bool sendButtonPressedOsc = true;

    private Vector3 startPos;
    private bool pressed = false;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (pressed) return;

        bool isTouching =
            (leftFingertip != null && Vector3.Distance(leftFingertip.position, transform.position) < triggerDistance) ||
            (rightFingertip != null && Vector3.Distance(rightFingertip.position, transform.position) < triggerDistance);

        if (isTouching)
        {
            PressButton();
        }
    }

    void PressButton()
    {
        pressed = true;

        // Visual press
        transform.localPosition = startPos - new Vector3(0, pressDepth, 0);

        if (director != null)
        {
            if (resetInsteadOfPlay)
            {
                // STOP + REWIND
                director.Stop();
                director.time = 0;
                director.Evaluate();
                OscSender.Instance?.Send("/timeline/stop", buttonId);
            }
            else
            {
                // PLAY
                director.Play();
                OscSender.Instance?.Send("/timeline/play", buttonId);
            }
        }

        if (sendButtonPressedOsc)
            OscSender.Instance?.Send("/button/pressed", buttonId);

        Invoke(nameof(ResetButton), cooldown);
    }

    void ResetButton()
    {
        transform.localPosition = startPos;
        pressed = false;
    }
}