using UnityEngine;
using UnityEngine.Playables;

public class ButtonTimelineTouch : MonoBehaviour
{
    public PlayableDirector director;
    public PlayableAsset    timeline;

    [Header("Finger Settings")]
    public Transform leftFingertip;
    public Transform rightFingertip;
    public float triggerDistance = 0.02f;

    [Header("Button Visual")]
    public float pressDepth = 0.01f;
    public float cooldown   = 0.5f;

    [Header("OSC")]
    public OscSender oscSender; // optional — assign in Inspector to notify Reaper

    private Vector3 startPos;
    private bool    pressed = false;

    void Start()
    {
        startPos = transform.localPosition;
    }

    void Update()
    {
        if (pressed) return;

        if ((leftFingertip  != null && Vector3.Distance(leftFingertip.position,  transform.position) < triggerDistance) ||
            (rightFingertip != null && Vector3.Distance(rightFingertip.position, transform.position) < triggerDistance))
        {
            PressButton();
        }
    }

    void PressButton()
    {
        pressed = true;
        transform.localPosition = startPos - new Vector3(0, pressDepth, 0);

        if (director != null && timeline != null)
        {
            director.Stop();
            director.playableAsset = timeline;
            director.RebuildGraph();
            director.Play();
        }

        oscSender?.SendPlay();

        Invoke(nameof(ResetButton), cooldown);
    }

    void ResetButton()
    {
        transform.localPosition = startPos;
        pressed = false;
    }
}
