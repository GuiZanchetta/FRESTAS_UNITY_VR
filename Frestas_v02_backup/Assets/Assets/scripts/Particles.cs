using UnityEngine;

public class HandMovementEmitter : MonoBehaviour
{
    public Transform handTransform;   // assign wrist joint
    public ParticleSystem ps;

    public float speedThreshold = 0.2f;  // tweak this
    public float emissionMultiplier = 10f;

    private Vector3 lastPosition;
    private bool firstFrame = true;

    void Update()
    {
        if (handTransform == null || ps == null) return;

        Vector3 currentPosition = handTransform.position;

        if (firstFrame)
        {
            lastPosition = currentPosition;
            firstFrame = false;
            return;
        }

        // Calculate velocity (distance per second)
        float speed = Vector3.Distance(currentPosition, lastPosition) / Time.deltaTime;

        // Emit based on speed
        if (speed > speedThreshold)
        {
            int particlesToEmit = Mathf.FloorToInt(speed * emissionMultiplier);
            ps.Emit(particlesToEmit);
        }

        lastPosition = currentPosition;
    }
}