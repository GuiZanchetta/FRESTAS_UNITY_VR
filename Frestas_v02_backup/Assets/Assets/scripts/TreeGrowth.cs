using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Space-Colonization 3D Tree with smooth Bezier branches and overlapping cylinders.
/// Features:
/// - Smooth curved branches with Bezier interpolation
/// - Overlapping cylinders for seamless appearance
/// - UVs mapped along branch length for realistic textures
/// - Smooth growth animation
/// </summary>
public class SpaceColonizationTree3D_Bezier : MonoBehaviour
{
    [Header("Attractor Cloud")]
    [Min(1)] public int attractorCount = 800;
    public Vector3 crownCenter = new Vector3(0f, 6f, 0f);
    public Vector3 crownSize = new Vector3(4f, 6f, 4f);
    public bool useEllipsoidCrown = true;

    [Header("Growth Distances")]
    [Min(0.01f)] public float influenceRadius = 2.0f;
    [Min(0.01f)] public float killRadius = 0.6f;
    [Min(0.01f)] public float segmentLength = 0.35f;

    [Header("Growth Timing")]
    [Min(0.001f)] public float growthInterval = 0.04f;
    [Min(1)] public int maxIterations = 2500;
    public bool autoGrowOnStart = true;

    [Header("Trunk")]
    public bool growInitialTrunk = true;
    public Vector3 rootPosition = Vector3.zero;
    public Vector3 initialDirection = Vector3.up;
    [Min(0)] public int maxInitialTrunkSteps = 100;
    [Min(0f)] public float minTrunkHeight = 2.0f;

    [Header("Branching")]
    [Range(0f, 1f)] public float branchProbability = 0.25f;
    [Range(10f, 45f)] public float maxBranchAngle = 35f;

    [Header("Directional Bias")]
    [Range(0f, 2f)] public float upwardBias = 0.2f;
    [Range(0f, 1f)] public float randomJitter = 0.08f;

    [Header("Rendering")]
    public Material branchMaterial;
    [Min(0.01f)] public float baseRadius = 0.12f;
    [Min(0.0001f)] public float radiusDecay = 0.92f;
    public int cylinderSegments = 12; // more = smoother
    public float minRadius = 0.02f;
    public float growthAnimationSpeed = 3f; // seconds per segment growth
    public float overlapFactor = 0.1f; // fraction of segmentLength to overlap

    [Header("Runtime Controls")]
    public bool clearChildrenOnRegenerate = true;
    public KeyCode regenerateKey = KeyCode.R;

    private class Attractor
    {
        public Vector3 position;
        public bool reached;
        public Attractor(Vector3 pos) { position = pos; reached = false; }
    }

    private class Branch
    {
        public Vector3 position;
        public Vector3 direction;
        public Branch parent;
        public List<Branch> children = new List<Branch>();
        public Vector3 accumulatedDirection = Vector3.zero;
        public int influenceCount = 0;
        public int depth = 0;

        public Branch(Vector3 pos, Vector3 dir, Branch parent = null)
        {
            position = pos;
            direction = dir.normalized;
            this.parent = parent;
            depth = parent == null ? 0 : parent.depth + 1;
        }

        public void ResetGrowth() { accumulatedDirection = Vector3.zero; influenceCount = 0; }
        public bool IsTip => children.Count == 0;
    }

    private readonly List<Attractor> attractors = new List<Attractor>();
    private readonly List<Branch> branches = new List<Branch>();
    private readonly List<Branch> tips = new List<Branch>();
    private readonly List<GameObject> renderedSegments = new List<GameObject>();
    private Coroutine growthRoutine;

    private void Start()
    {
        if (autoGrowOnStart) GenerateAndGrow();
    }

    private void Update()
    {
        if (Input.GetKeyDown(regenerateKey)) GenerateAndGrow();
    }

    [ContextMenu("Generate And Grow")]
    public void GenerateAndGrow()
    {
        StopGrowth();
        ResetTree();
        GenerateAttractors();
        growthRoutine = StartCoroutine(GrowTreeCoroutine());
    }

    [ContextMenu("Stop Growth")]
    public void StopGrowth()
    {
        if (growthRoutine != null) StopCoroutine(growthRoutine);
    }

    private void ResetTree()
    {
        attractors.Clear();
        branches.Clear();
        tips.Clear();
        foreach (var go in renderedSegments) if (go != null) Destroy(go);
        renderedSegments.Clear();
    }

    private void GenerateAttractors()
    {
        for (int i = 0; i < attractorCount; i++)
        {
            Vector3 localPoint = useEllipsoidCrown ? Random.insideUnitSphere : RandomPointInUnitBox();
            Vector3 scaled = Vector3.Scale(localPoint, crownSize);
            scaled.y = Mathf.Max(scaled.y, crownSize.y * 0.3f);
            attractors.Add(new Attractor(crownCenter + scaled));
        }
    }

    private Vector3 RandomPointInUnitBox() => new Vector3(Random.Range(-1f, 1f), Random.Range(-1f, 1f), Random.Range(-1f, 1f));
    private Vector3Int Quantize(Vector3 p, float cellSize) =>
        new Vector3Int(Mathf.RoundToInt(p.x / cellSize), Mathf.RoundToInt(p.y / cellSize), Mathf.RoundToInt(p.z / cellSize));

    private bool IsAnyAttractorWithinInfluence(Vector3 point)
    {
        float sqr = influenceRadius * influenceRadius;
        foreach (var a in attractors) if ((a.position - point).sqrMagnitude <= sqr) return true;
        return false;
    }

    // -------------------------
    // Growth coroutine
    // -------------------------
    private IEnumerator GrowTreeCoroutine()
    {
        // Initial trunk
        if (growInitialTrunk)
        {
            Vector3 dir = initialDirection.sqrMagnitude > 0f ? initialDirection.normalized : Vector3.up;
            Branch root = new Branch(rootPosition, dir, null);
            branches.Add(root);
            tips.Add(root);
            Branch current = root;
            float trunkHeight = 0f;

            for (int i = 0; i < maxInitialTrunkSteps; i++)
            {
                Vector3 nextPos = current.position + dir * segmentLength;
                Branch next = new Branch(nextPos, dir, current);
                current.children.Add(next);
                branches.Add(next);

                GameObject branchGO = RenderBranchMesh(current, next);
                yield return AnimateBranchGrowth(branchGO, segmentLength, growthAnimationSpeed);

                tips.Clear(); tips.Add(next);
                current = next;
                trunkHeight += segmentLength;

                if (trunkHeight >= minTrunkHeight && IsAnyAttractorWithinInfluence(current.position))
                    break;
            }
        }

        // Space-colonization growth
        int iterations = 0;
        while (iterations < maxIterations)
        {
            bool grew = GrowOneIteration();
            iterations++;
            if (!grew) break;
            yield return new WaitForSeconds(growthInterval);
        }

        growthRoutine = null;
    }

    private bool GrowOneIteration()
    {
        if (attractors.Count == 0 || tips.Count == 0) return false;

        foreach (var tip in tips) tip.ResetGrowth();

        foreach (var attractor in attractors)
        {
            Branch closestTip = null;
            float closestDist = float.MaxValue;
            foreach (var tip in tips)
            {
                float dist = Vector3.Distance(attractor.position, tip.position);
                if (dist <= killRadius)
                {
                    attractor.reached = true;
                    closestTip = null;
                    break;
                }
                if (dist <= influenceRadius && dist < closestDist)
                {
                    closestDist = dist;
                    closestTip = tip;
                }
            }
            if (closestTip != null)
            {
                Vector3 dir = (attractor.position - closestTip.position).normalized;
                closestTip.accumulatedDirection += dir;
                closestTip.influenceCount++;
            }
        }

        attractors.RemoveAll(a => a.reached);

        List<Branch> newTips = new List<Branch>();
        HashSet<Vector3Int> occupied = new HashSet<Vector3Int>();

        foreach (var tip in tips)
        {
            if (tip.influenceCount <= 0) continue;
            Vector3 avgDir = tip.accumulatedDirection / tip.influenceCount;
            avgDir += Vector3.up * upwardBias;
            avgDir += Random.insideUnitSphere * randomJitter;
            if (avgDir.sqrMagnitude < 0.000001f) avgDir = tip.direction;
            avgDir.Normalize();

            Vector3 newPos = tip.position + avgDir * segmentLength * (1 - overlapFactor); // overlap
            Vector3Int cell = Quantize(newPos, 0.05f);
            if (!occupied.Contains(cell))
            {
                Branch child = new Branch(newPos, avgDir, tip);
                tip.children.Add(child);
                branches.Add(child);
                newTips.Add(child);

                GameObject branchGO = RenderBranchMesh(tip, child);
                StartCoroutine(AnimateBranchGrowth(branchGO, segmentLength, growthAnimationSpeed));

                occupied.Add(cell);

                // Possibly split branch
                if (Random.value < branchProbability)
                {
                    Vector3 splitDir = Quaternion.Euler(
                        Random.Range(-maxBranchAngle, maxBranchAngle),
                        Random.Range(-maxBranchAngle, maxBranchAngle),
                        Random.Range(-maxBranchAngle, maxBranchAngle)
                    ) * avgDir;

                    Vector3 splitPos = tip.position + splitDir * segmentLength * (1 - overlapFactor);
                    Vector3Int splitCell = Quantize(splitPos, 0.05f);
                    if (!occupied.Contains(splitCell))
                    {
                        Branch splitChild = new Branch(splitPos, splitDir, tip);
                        tip.children.Add(splitChild);
                        branches.Add(splitChild);
                        newTips.Add(splitChild);

                        GameObject splitGO = RenderBranchMesh(tip, splitChild);
                        StartCoroutine(AnimateBranchGrowth(splitGO, segmentLength, growthAnimationSpeed));
                        occupied.Add(splitCell);
                    }
                }
            }
        }

        if (newTips.Count == 0) return false;
        tips.Clear();
        tips.AddRange(newTips);
        return true;
    }

    // -------------------------
    // Mesh and animation
    // -------------------------
    private GameObject RenderBranchMesh(Branch a, Branch b)
    {
        GameObject go = new GameObject($"Branch_{branches.Count}");
        go.transform.SetParent(transform, false);

        MeshFilter mf = go.AddComponent<MeshFilter>();
        MeshRenderer mr = go.AddComponent<MeshRenderer>();
        mr.material = branchMaterial != null ? branchMaterial : new Material(Shader.Find("Standard"));

        // --- Smooth Bezier curve ---
        Vector3 p0 = a.position;
        Vector3 p1 = a.position + a.direction * segmentLength * 0.5f;
        Vector3 p2 = b.position - b.direction * segmentLength * 0.5f;
        Vector3 p3 = b.position;

        int curveSegments = 6; // more = smoother
        List<Vector3> curvePoints = new List<Vector3>();
        for (int i = 0; i <= curveSegments; i++)
        {
            float t = i / (float)curveSegments;
            curvePoints.Add(GetCubicBezierPoint(p0, p1, p2, p3, t));
        }

        // --- Generate mesh along curve ---
        Mesh mesh = CreateMeshAlongCurve(curvePoints, a.depth, b.depth);
        mf.mesh = mesh;

        renderedSegments.Add(go);
        return go;
    }

    private Vector3 GetCubicBezierPoint(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float u = 1 - t;
        return u*u*u*p0 + 3*u*u*t*p1 + 3*u*t*t*p2 + t*t*t*p3;
    }private Mesh CreateMeshAlongCurve(List<Vector3> points, int startDepth, int endDepth)
{
    Mesh mesh = new Mesh();
    List<Vector3> verts = new List<Vector3>();
    List<int> tris = new List<int>();
    List<Vector2> uvs = new List<Vector2>();

    int ringCount = points.Count;
    int ringSegments = Mathf.Max(cylinderSegments, 3);

    // --- Precompute curve length ---
    float[] lengths = new float[ringCount];
    float totalLength = 0f;
    lengths[0] = 0f;
    for (int i = 1; i < ringCount; i++)
    {
        totalLength += Vector3.Distance(points[i - 1], points[i]);
        lengths[i] = totalLength;
    }

    // --- Initial frame ---
    Vector3 forward = (points[1] - points[0]).normalized;
    Vector3 right = Vector3.Cross(forward, Vector3.up);
    if (right.sqrMagnitude < 0.0001f)
        right = Vector3.Cross(forward, Vector3.right);
    right.Normalize();
    Vector3 up = Vector3.Cross(right, forward).normalized;

    for (int i = 0; i < ringCount; i++)
    {
        Vector3 newForward;
        if (i < ringCount - 1)
            newForward = (points[i + 1] - points[i]).normalized;
        else
            newForward = (points[i] - points[i - 1]).normalized;

        if (newForward.sqrMagnitude < 0.0001f)
            newForward = forward;

        // --- Parallel transport (THIS IS THE IMPORTANT FIX) ---
        Quaternion rotation = Quaternion.FromToRotation(forward, newForward);
        right = rotation * right;
        up = rotation * up;
        forward = newForward;

        // --- Radius ---
        float t = i / (float)(ringCount - 1);
        float radius = Mathf.Lerp(baseRadius * Mathf.Pow(radiusDecay, startDepth),
                                  Mathf.Max(baseRadius * Mathf.Pow(radiusDecay, endDepth), minRadius),
                                  t);
        radius = Mathf.Max(radius, 0.01f);

        // --- Create ring (WITH duplicate vertex for UV seam) ---
        for (int j = 0; j <= ringSegments; j++)
        {
            float u = j / (float)ringSegments;
            float angle = u * Mathf.PI * 2f;

            Vector3 offset = right * Mathf.Cos(angle) * radius +
                             up * Mathf.Sin(angle) * radius;

            verts.Add(points[i] + offset);

            float v = lengths[i] / totalLength;
            uvs.Add(new Vector2(u, v));
        }

        // --- Triangles ---
        if (i > 0)
        {
            int prevBase = (i - 1) * (ringSegments + 1);
            int baseIndex = i * (ringSegments + 1);

            for (int j = 0; j < ringSegments; j++)
            {
                int next = j + 1;

                tris.Add(prevBase + j);
                tris.Add(baseIndex + j);
                tris.Add(baseIndex + next);

                tris.Add(prevBase + j);
                tris.Add(baseIndex + next);
                tris.Add(prevBase + next);
            }
        }
    }

    mesh.SetVertices(verts);
    mesh.SetTriangles(tris, 0);
    mesh.SetUVs(0, uvs);
    mesh.RecalculateBounds();
mesh.RecalculateNormals(UnityEngine.Rendering.MeshUpdateFlags.Default);
    return mesh;
}
private IEnumerator AnimateBranchGrowth(GameObject branch, float targetHeight, float duration)
{
    // For now, just wait for the duration (tree is fully visible immediately)
    yield return new WaitForSeconds(duration);
}
}