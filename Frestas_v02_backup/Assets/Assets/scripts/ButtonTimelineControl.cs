using UnityEngine;
using UnityEngine.Playables;

public class ButtonTimelineTouch : MonoBehaviour
{
    public PlayableDirector director;
    public PlayableAsset timeline;

    [Header("Finger Settings")]
    public Transform leftFingertip;
    public Transform rightFingertip;
    public float triggerDistance = 0.02f; // 2 cm

    [Header("Button Visual")]
    public float pressDepth = 0.01f; // 1 cm
    public float cooldown = 0.5f;

    private Vector3 startPos;
    private bool pressed = false;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (pressed) return;

        // Check distance to either hand
        if ((leftFingertip != null && Vector3.Distance(leftFingertip.position, transform.position) < triggerDistance) ||
            (rightFingertip != null && Vector3.Distance(rightFingertip.position, transform.position) < triggerDistance))
        {
            PressButton();
        }
    }

    void PressButton()
    {
        pressed = true;

        // Move button visually
        transform.localPosition = startPos - new Vector3(0, pressDepth, 0);

        // Play timeline
        if (director != null && timeline != null)
        {
            director.Stop();
            director.playableAsset = timeline;
            director.RebuildGraph();
            director.Play();
        }

        // Reset after cooldown
        Invoke(nameof(ResetButton), cooldown);
    }

    void ResetButton()
    {
        transform.localPosition = startPos;
        pressed = false;
    }
}