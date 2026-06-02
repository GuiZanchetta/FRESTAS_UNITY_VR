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
            }
            else
            {
                // PLAY
                director.Play();
            }
        }

        Invoke(nameof(ResetButton), cooldown);
    }

    void ResetButton()
    {
        transform.localPosition = startPos;
        pressed = false;
    }
}