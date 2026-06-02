using System.Collections.Generic;
using UnityEngine;

public class SmoothTreeNoTwist : MonoBehaviour
{
    [Header("Root")]
    public Vector3 rootPosition = Vector3.zero; // New: starting position of the trunk

    [Header("Attractors")]
    public int attractorCount = 800;
    public Vector3 crownCenter = new Vector3(0, 6, 0);
    public Vector3 crownSize = new Vector3(4, 6, 4);

    [Header("Growth")]
    public float influenceRadius = 2f;
    public float killRadius = 0.6f;
    public float segmentLength = 0.35f;
    public int maxPoints = 4000;

    [Header("VR Growth")]
    public Transform playerCamera;
    public float movementToGrowth = 40f;

    [Header("Smoothing")]
    public int bezierSamples = 6;
    public float curveTension = 0.5f;

    [Header("Rendering")]
    public Material material;
    public float radius = 0.05f;
    public int radialSegments = 8;

    class Attractor
    {
        public Vector3 pos;
        public bool reached;
        public Attractor(Vector3 p) { pos = p; reached = false; }
    }

    List<Attractor> attractors = new List<Attractor>();
    List<Vector3> path = new List<Vector3>();

    Mesh mesh;
    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();

    int prevRingStart = -1;

    Vector3 lastForward;
    Vector3 lastRight;
    Vector3 lastUp;

    float growthAccumulator;
    Vector3 lastCamPos;

    void Start()
    {
        if (playerCamera == null)
            playerCamera = Camera.main.transform;

        lastCamPos = playerCamera.position;

        GenerateAttractors();
path.Add(rootPosition);
path.Add(rootPosition + Vector3.up * segmentLength);

        GameObject go = new GameObject("TreeMesh");
        go.transform.SetParent(transform);

        mesh = new Mesh();
        go.AddComponent<MeshFilter>().mesh = mesh;

        var mr = go.AddComponent<MeshRenderer>();
        mr.material = material;

        // Initial frame
        lastForward = Vector3.up;
        lastRight = Vector3.Cross(lastForward, Vector3.right).normalized;
        lastUp = Vector3.Cross(lastRight, lastForward).normalized;
    }

    void Update()
    {
        float movement = Vector3.Distance(playerCamera.position, lastCamPos);
        lastCamPos = playerCamera.position;

        growthAccumulator += movement * movementToGrowth;

        bool grew = false;

        while (growthAccumulator >= 1f)
        {
            if (!Grow()) break;
            growthAccumulator -= 1f;
            grew = true;
        }

        if (grew)
            AppendMesh();
    }

    void GenerateAttractors()
    {
        attractors.Clear();

        for (int i = 0; i < attractorCount; i++)
        {
            Vector3 p = Random.insideUnitSphere;
            p = Vector3.Scale(p, crownSize);
            attractors.Add(new Attractor(crownCenter + p));
        }
    }

    bool Grow()
    {
        if (path.Count >= maxPoints) return false;

        Vector3 tip = path[path.Count - 1];

        Vector3 dirSum = Vector3.zero;
        int count = 0;

        foreach (var a in attractors)
        {
            float d = Vector3.Distance(a.pos, tip);

            if (d < killRadius)
            {
                a.reached = true;
                continue;
            }

            if (d < influenceRadius)
            {
                dirSum += (a.pos - tip).normalized;
                count++;
            }
        }

        attractors.RemoveAll(a => a.reached);

        if (count == 0)
            dirSum = Vector3.up;

        Vector3 newDir = dirSum.normalized;
        Vector3 newPos = tip + newDir * segmentLength;

        path.Add(newPos);
        return true;
    }

    void AppendMesh()
    {
        int i = path.Count - 1;
        if (i < 2) return;

        Vector3 p0 = path[i - 1];
        Vector3 p1 = path[i];

        Vector3 prevDir = (p0 - path[Mathf.Max(i - 2, 0)]).normalized;
        Vector3 nextDir = (p1 - p0).normalized;

        Vector3 c0 = p0 + prevDir * segmentLength * curveTension;
        Vector3 c1 = p1 - nextDir * segmentLength * curveTension;

        Vector3 lastPoint = p0;

        for (int s = 1; s <= bezierSamples; s++)
        {
            float t = s / (float)bezierSamples;

            Vector3 point = Bezier(p0, c0, c1, p1, t);

            Vector3 forward = (point - lastPoint).normalized;

            // Stable frame (parallel transport, NO twist)
            Quaternion rot = Quaternion.FromToRotation(lastForward, forward);
            lastRight = rot * lastRight;
            lastUp = rot * lastUp;
            lastForward = forward;

            Vector3 right = lastRight;
            Vector3 up = lastUp;

            AddRing(point, ref right, ref up, forward);

            lastRight = right;
            lastUp = up;

            lastPoint = point;
        }

        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
    }

    Vector3 Bezier(Vector3 p0, Vector3 c0, Vector3 c1, Vector3 p1, float t)
    {
        float u = 1f - t;
        return u * u * u * p0 +
               3f * u * u * t * c0 +
               3f * u * t * t * c1 +
               t * t * t * p1;
    }

    void AddRing(Vector3 center, ref Vector3 right, ref Vector3 up, Vector3 forward)
    {
        Vector3 newRight = Vector3.Cross(forward, up);
        if (newRight.sqrMagnitude < 0.001f)
            newRight = Vector3.Cross(forward, Vector3.right);

        newRight.Normalize();
        up = Vector3.Cross(newRight, forward).normalized;
        right = newRight;

        int ringStart = verts.Count;

        for (int i = 0; i < radialSegments; i++)
        {
            float a = i / (float)radialSegments * Mathf.PI * 2f;

            Vector3 offset =
                right * Mathf.Cos(a) * radius +
                up * Mathf.Sin(a) * radius;

            verts.Add(center + offset);
        }

        if (prevRingStart != -1)
        {
            for (int i = 0; i < radialSegments; i++)
            {
                int next = (i + 1) % radialSegments;

                int a = prevRingStart + i;
                int b = prevRingStart + next;
                int c = ringStart + i;
                int d = ringStart + next;

                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }
        }

        prevRingStart = ringStart;
    }
}