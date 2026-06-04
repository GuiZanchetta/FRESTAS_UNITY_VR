using UnityEngine;

public class cloverSway : MonoBehaviour
{
    [Header("Wind Settings")]
    public float swayAmount = 5f;
    public float swaySpeed = 1f;
    public float noiseScale = 1f;

    [Header("Axis Control")]
    public bool swayX = false;
    public bool swayY = false;
    public bool swayZ = true;

    private Vector3 initialRotation;
    private Vector3 positionOffset;

    void Start()
    {
        initialRotation = transform.eulerAngles;

        // Use world position to offset noise → key to breaking sync
        positionOffset = transform.position * 0.5f;
    }

    void Update()
    {
        float time = Time.time * swaySpeed;

        // Sample noise using BOTH position + time
        float noise = Mathf.PerlinNoise(
            positionOffset.x + time * noiseScale,
            positionOffset.y + time * noiseScale
        );

        noise = (noise - 0.5f) * 2f;

        float heightFactor = Mathf.Clamp01(transform.position.y * 0.1f);
float sway = noise * swayAmount * heightFactor;

        Vector3 newRotation = initialRotation;

        if (swayX) newRotation.x += sway;
        if (swayY) newRotation.y += sway;
        if (swayZ) newRotation.z += sway;

        transform.eulerAngles = newRotation;
    }
}