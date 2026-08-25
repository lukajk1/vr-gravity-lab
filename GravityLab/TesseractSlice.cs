using System.Collections.Generic;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Builds the 3D cross-section of a rotating 4D cube (a tesseract) as a live mesh.
    /// The tesseract is rotated in four dimensions, then intersected with the hyperplane
    /// w = sliceW; whatever polyhedron that intersection forms is rebuilt each frame.
    /// </summary>
    /// <remarks>
    /// Rotation in 4D happens in planes, not about axes. Three dimensions has three planes
    /// and three axes, which is why "rotate about Z" reads as a rotation axis there, but the
    /// coincidence ends at 4D: six planes, four axes. The three planes involving w are the
    /// ones that change the slice's shape.
    ///
    /// The section is recomputed rather than deformed because its topology genuinely changes
    /// as it turns, cycling through tetrahedra, cubes, octahedra and prisms with different
    /// vertex counts. A vertex shader cannot do this: it may move vertices but never create
    /// or remove them.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class TesseractSlice : MonoBehaviour, IFourDimensionalRotatable
    {
        // The six planes a 4D rotation can happen in. XY, XZ and YZ are the familiar 3D ones;
        // XW, YW and ZW sweep geometry through the fourth axis and reshape the slice.
        public enum RotationPlane
        {
            XY,
            XZ,
            YZ,
            XW,
            YW,
            ZW,
        }

        [SerializeField]
        [Tooltip("Half-width of the tesseract along each of its four axes")]
        float m_Size = 0.5f;

        [SerializeField]
        [Tooltip("Where along the fourth axis to take the section. Zero cuts through the centre.")]
        float m_SliceW;

        [Header("Rotation")]
        [SerializeField]
        [Tooltip("Angle in each of the six rotation planes, in degrees, in the order XY, XZ, YZ, XW, YW, ZW")]
        float[] m_PlaneAngles = new float[6];

        [SerializeField]
        [Tooltip("Degrees per second added to each plane's angle, in the same order. Two non-zero entries give the classic tumbling cross-section.")]
        float[] m_PlaneSpeeds = { 0f, 0f, 0f, 25f, 0f, 40f };

        [Header("Output")]
        [SerializeField]
        [Tooltip("Rebuild the section this many times a second. Zero rebuilds every frame.")]
        float m_RebuildRate = 60f;

        [SerializeField]
        [Tooltip("Smallest rotation, in degrees, worth rebuilding for. Larger values skip work when the cube is barely moving.")]
        float m_AngleEpsilon = 0.05f;

        [Header("Cell colours")]
        [SerializeField]
        [Tooltip("Tint each face by the tesseract cell it lies in. A cell keeps its colour as the cube turns, so a face fading dark is that cell sweeping away through w.")]
        bool m_ColourByCell = true;

        [SerializeField]
        [Tooltip("Colours for the +X, +Y and +Z cells. Their negative twins use the same hue darkened.")]
        Color[] m_AxisColours =
        {
            new Color(1f, 0.05f, 0.02f),
            new Color(0.05f, 1f, 0.08f),
            new Color(0.06f, 0.2f, 1f),
        };

        [SerializeField]
        [Tooltip("How much darker a negative cell is than its positive twin")]
        [Range(0f, 1f)]
        float m_NegativeCellDarkening = 0.45f;

        [SerializeField]
        [Tooltip("Colour of the +w cell, the one facing along the fourth axis")]
        Color m_PositiveWColour = Color.white;

        [SerializeField]
        [Tooltip("Colour of the -w cell, the one facing away along the fourth axis")]
        Color m_NegativeWColour = new Color(0.06f, 0.06f, 0.09f);

        [SerializeField]
        [Tooltip("Multiplies every cell colour. Above 1 pushes them past white, which survives the lighting and rim without looking washed out.")]
        float m_ColourGain = 1.6f;

        [SerializeField]
        [Tooltip("Log the section's vertex and triangle count whenever its topology changes")]
        bool m_LogTopologyChanges;

        // A tesseract has 16 vertices: every combination of +/-size on four axes.
        readonly Vector4[] m_Corners = new Vector4[16];

        // ...and 32 edges, one for each pair of corners differing in exactly one coordinate.
        readonly List<int> m_EdgeA = new List<int>(32);
        readonly List<int> m_EdgeB = new List<int>(32);

        readonly Vector4[] m_Rotated = new Vector4[16];
        readonly List<Vector3> m_SectionPoints = new List<Vector3>(32);

        // Which of the 8 cells each section point lies in, as a bit per cell. A point sits on
        // an edge, and an edge borders every cell whose fixed coordinate both its ends share.
        readonly List<int> m_PointCells = new List<int>(32);
        readonly List<Color> m_Colours = new List<Color>(128);
        readonly List<Vector3> m_Vertices = new List<Vector3>(128);
        readonly List<int> m_Triangles = new List<int>(256);

        readonly float[] m_LastAngles = new float[6];
        readonly List<HullPlane> m_Planes = new List<HullPlane>(32);
        readonly List<Vector3> m_FacePoints = new List<Vector3>(16);

        Mesh m_Mesh;
        float m_NextRebuildTime;
        int m_LastPointCount = -1;

        /// <summary>Number of corners in the current cross-section, which changes as it turns.</summary>
        public int sectionPointCount => m_SectionPoints.Count;

        /// <summary>
        /// Whether the section is due to be rebuilt this frame. Anything feeding this slice
        /// can check it to avoid doing work whose result would be thrown away.
        /// </summary>
        public bool isRebuildDue => m_RebuildRate <= 0f || Time.time >= m_NextRebuildTime;

        /// <summary>Angle in one rotation plane, in degrees.</summary>
        public float GetAngle(RotationPlane plane) => m_PlaneAngles[(int)plane];

        /// <summary>Sets the angle in one rotation plane, in degrees.</summary>
        public void SetAngle(RotationPlane plane, float degrees)
        {
            m_PlaneAngles[(int)plane] = degrees;
        }

        /// <summary>Sets how fast one rotation plane turns, in degrees per second.</summary>
        public void SetSpeed(RotationPlane plane, float degreesPerSecond)
        {
            m_PlaneSpeeds[(int)plane] = degreesPerSecond;
        }

        void Awake()
        {
            BuildTesseract();

            m_Mesh = new Mesh { name = "Tesseract Section" };

            // The section is rebuilt constantly, so let Unity keep it in fast-write memory.
            m_Mesh.MarkDynamic();
            GetComponent<MeshFilter>().sharedMesh = m_Mesh;
        }

        void OnDestroy()
        {
            if (m_Mesh != null)
                Destroy(m_Mesh);
        }

        void Update()
        {
            // Serialized arrays can be resized in the inspector, so never trust their length.
            if (m_PlaneAngles.Length < 6)
                System.Array.Resize(ref m_PlaneAngles, 6);

            if (m_PlaneSpeeds.Length < 6)
                System.Array.Resize(ref m_PlaneSpeeds, 6);

            for (var i = 0; i < 6; i++)
                m_PlaneAngles[i] += m_PlaneSpeeds[i] * Time.deltaTime;

            if (m_RebuildRate > 0f && Time.time < m_NextRebuildTime)
                return;

            if (m_RebuildRate > 0f)
                m_NextRebuildTime = Time.time + 1f / m_RebuildRate;

            // Rebuilding an unchanged section would produce an identical mesh, so skip it.
            if (!AnglesChanged())
                return;

            Rebuild();
        }

        /// <summary>
        /// Reports whether any plane has turned far enough since the last rebuild to be worth
        /// recomputing, and records the angles when it has.
        /// </summary>
        bool AnglesChanged()
        {
            var changed = false;

            for (var i = 0; i < 6; i++)
            {
                if (Mathf.Abs(m_PlaneAngles[i] - m_LastAngles[i]) > m_AngleEpsilon)
                {
                    changed = true;
                    break;
                }
            }

            if (!changed)
                return false;

            for (var i = 0; i < 6; i++)
                m_LastAngles[i] = m_PlaneAngles[i];

            return true;
        }

        /// <summary>
        /// Lays out the 16 corners and finds the 32 edges. Two corners share an edge when
        /// they differ in exactly one of their four coordinates.
        /// </summary>
        void BuildTesseract()
        {
            for (var i = 0; i < 16; i++)
            {
                m_Corners[i] = new Vector4(
                    (i & 1) == 0 ? -1f : 1f,
                    (i & 2) == 0 ? -1f : 1f,
                    (i & 4) == 0 ? -1f : 1f,
                    (i & 8) == 0 ? -1f : 1f);
            }

            m_EdgeA.Clear();
            m_EdgeB.Clear();

            for (var a = 0; a < 16; a++)
            {
                for (var b = a + 1; b < 16; b++)
                {
                    // Differing in exactly one bit means exactly one coordinate flipped.
                    var diff = a ^ b;

                    if ((diff & (diff - 1)) == 0)
                    {
                        m_EdgeA.Add(a);
                        m_EdgeB.Add(b);
                    }
                }
            }
        }

        /// <summary>
        /// Returns a bit per tesseract cell the given edge lies in. Cells are numbered as two
        /// per axis, negative then positive, so axis a contributes bits 2a and 2a+1. An edge
        /// changes exactly one coordinate, so it lies in the cells of the other three.
        /// </summary>
        static int CellsForEdge(int cornerA, int cornerB)
        {
            var cells = 0;

            for (var axis = 0; axis < 4; axis++)
            {
                var bit = 1 << axis;

                // The axis this edge runs along differs between its ends, so it borders
                // neither of that axis's cells.
                if (((cornerA ^ cornerB) & bit) != 0)
                    continue;

                cells |= 1 << (axis * 2 + ((cornerA & bit) != 0 ? 1 : 0));
            }

            return cells;
        }

        /// <summary>
        /// Colour for one cell index, as numbered by <see cref="CellsForEdge"/>. The three
        /// ordinary axes take their own hue, darkened on the negative side; the w cells are
        /// the extremes of light and dark, so a face sweeping through the fourth dimension
        /// reads as a change in brightness.
        /// </summary>
        Color ColourForCell(int cell)
        {
            var axis = cell / 2;
            var positive = (cell & 1) != 0;

            if (axis == 3)
                return Boost(positive ? m_PositiveWColour : m_NegativeWColour);

            var baseColour = axis < m_AxisColours.Length ? m_AxisColours[axis] : Color.grey;

            if (!positive)
            {
                // Scale the channels only. Multiplying the whole Color would dim alpha too,
                // which is meaningless on an opaque surface and confusing to read back.
                var dim = 1f - m_NegativeCellDarkening;
                baseColour = new Color(baseColour.r * dim, baseColour.g * dim, baseColour.b * dim, baseColour.a);
            }

            return Boost(baseColour);
        }

        /// <summary>
        /// Applies the colour gain, again leaving alpha alone. Values above one are kept
        /// rather than clamped, so a bright cell still reads as its own hue once the rim and
        /// scene lighting have been added on top.
        /// </summary>
        Color Boost(Color colour)
        {
            return new Color(colour.r * m_ColourGain, colour.g * m_ColourGain, colour.b * m_ColourGain, colour.a);
        }

        void Rebuild()
        {
            RotateCorners();
            FindSectionPoints();
            BuildHull();

            m_Mesh.Clear();

            if (m_Vertices.Count >= 3)
            {
                // SetVertices/SetTriangles with a List avoids the array copy that the
                // array overloads force, and Clear already reset the previous contents.
                m_Mesh.SetVertices(m_Vertices);

                if (m_ColourByCell && m_Colours.Count == m_Vertices.Count)
                    m_Mesh.SetColors(m_Colours);

                m_Mesh.SetTriangles(m_Triangles, 0, false);
                m_Mesh.RecalculateNormals();
                m_Mesh.RecalculateBounds();
            }

            if (m_LogTopologyChanges && m_SectionPoints.Count != m_LastPointCount)
            {
                Debug.Log($"[{name}] section changed: {m_SectionPoints.Count} corners, " +
                    $"{m_Vertices.Count} verts, {m_Triangles.Count / 3} tris", this);
                m_LastPointCount = m_SectionPoints.Count;
            }
        }

        /// <summary>
        /// Applies all six plane rotations to every corner. Each one is an ordinary 2D
        /// rotation acting on the two coordinates that name it, leaving the other two alone.
        /// </summary>
        void RotateCorners()
        {
            for (var i = 0; i < 16; i++)
            {
                var v = m_Corners[i] * m_Size;

                v = RotateInPlane(v, 0, 1, m_PlaneAngles[0]); // XY
                v = RotateInPlane(v, 0, 2, m_PlaneAngles[1]); // XZ
                v = RotateInPlane(v, 1, 2, m_PlaneAngles[2]); // YZ
                v = RotateInPlane(v, 0, 3, m_PlaneAngles[3]); // XW
                v = RotateInPlane(v, 1, 3, m_PlaneAngles[4]); // YW
                v = RotateInPlane(v, 2, 3, m_PlaneAngles[5]); // ZW

                m_Rotated[i] = v;
            }
        }

        static Vector4 RotateInPlane(Vector4 v, int axisA, int axisB, float degrees)
        {
            var radians = degrees * Mathf.Deg2Rad;
            var cos = Mathf.Cos(radians);
            var sin = Mathf.Sin(radians);

            var a = v[axisA];
            var b = v[axisB];

            v[axisA] = a * cos - b * sin;
            v[axisB] = a * sin + b * cos;

            return v;
        }

        /// <summary>
        /// Finds where the rotated tesseract's edges cross the slicing hyperplane. None of
        /// the 16 corners generally lies on it, so the section's corners are these crossing
        /// points rather than any subset of the original vertices.
        /// </summary>
        void FindSectionPoints()
        {
            m_SectionPoints.Clear();
            m_PointCells.Clear();

            for (var e = 0; e < m_EdgeA.Count; e++)
            {
                var indexA = m_EdgeA[e];
                var indexB = m_EdgeB[e];
                var p = m_Rotated[indexA];
                var q = m_Rotated[indexB];

                var dp = p.w - m_SliceW;
                var dq = q.w - m_SliceW;

                // Same side of the hyperplane means no crossing.
                if (dp * dq > 0f)
                    continue;

                var denominator = dp - dq;

                if (Mathf.Abs(denominator) < 1e-7f)
                    continue;

                var t = dp / denominator;
                var point = Vector4.Lerp(p, q, t);
                var point3 = new Vector3(point.x, point.y, point.z);

                // Several edges can cross at the same corner; keep one copy so the hull is clean.
                // An edge lies in every cell whose coordinate is the same at both ends, and
                // the crossing point inherits that membership.
                var cells = CellsForEdge(indexA, indexB);

                var duplicate = -1;

                for (var existing = 0; existing < m_SectionPoints.Count; existing++)
                {
                    if ((m_SectionPoints[existing] - point3).sqrMagnitude < 1e-8f)
                    {
                        duplicate = existing;
                        break;
                    }
                }

                if (duplicate >= 0)
                {
                    // A corner shared by several edges belongs to all their cells.
                    m_PointCells[duplicate] |= cells;
                    continue;
                }

                m_SectionPoints.Add(point3);
                m_PointCells.Add(cells);
            }
        }

        /// <summary>
        /// Triangulates the section. The intersection of a convex body with a hyperplane is
        /// convex, so fanning triangles from the centroid to every point pair on each face
        /// is sound; here that is done by building the convex hull of the section points.
        /// </summary>
        void BuildHull()
        {
            m_Vertices.Clear();
            m_Triangles.Clear();
            m_Colours.Clear();
            m_Planes.Clear();

            if (m_SectionPoints.Count < 4)
                return;

            var centroid = Vector3.zero;

            foreach (var p in m_SectionPoints)
                centroid += p;

            centroid /= m_SectionPoints.Count;

            var count = m_SectionPoints.Count;

            // Collect the distinct hull planes. Every triple of points defines a candidate;
            // it is a face when no other point lies on both sides of it. Coplanar triples
            // describe the same face, so they are merged rather than each emitting geometry.
            for (var i = 0; i < count; i++)
            {
                for (var j = i + 1; j < count; j++)
                {
                    for (var k = j + 1; k < count; k++)
                    {
                        var a = m_SectionPoints[i];
                        var b = m_SectionPoints[j];
                        var c = m_SectionPoints[k];

                        var normal = Vector3.Cross(b - a, c - a);

                        if (normal.sqrMagnitude < 1e-10f)
                            continue;

                        normal.Normalize();
                        var offset = Vector3.Dot(normal, a);

                        // Reject as soon as points are found on both sides, rather than
                        // counting every one: most candidate triples fail early.
                        var positive = 0;
                        var negative = 0;
                        var straddles = false;

                        for (var m = 0; m < count; m++)
                        {
                            var side = Vector3.Dot(normal, m_SectionPoints[m]) - offset;

                            if (side > 1e-5f)
                                positive++;
                            else if (side < -1e-5f)
                                negative++;

                            if (positive > 0 && negative > 0)
                            {
                                straddles = true;
                                break;
                            }
                        }

                        if (straddles)
                            continue;

                        // Point the normal outward, so the winding below is consistent.
                        if (positive > 0)
                        {
                            normal = -normal;
                            offset = -offset;
                        }

                        var known = false;

                        foreach (var plane in m_Planes)
                        {
                            if (Vector3.Dot(plane.normal, normal) > 0.9999f &&
                                Mathf.Abs(plane.offset - offset) < 1e-4f)
                            {
                                known = true;
                                break;
                            }
                        }

                        if (!known)
                            m_Planes.Add(new HullPlane { normal = normal, offset = offset });
                    }
                }
            }

            // Fan each face from its own centre, so a square face becomes two triangles
            // rather than one per coplanar triple.
            foreach (var plane in m_Planes)
            {
                m_FacePoints.Clear();

                // A hull face lies in whichever cell all of its points share, so intersect
                // their memberships as they are collected.
                var sharedCells = -1;

                for (var m = 0; m < count; m++)
                {
                    if (Mathf.Abs(Vector3.Dot(plane.normal, m_SectionPoints[m]) - plane.offset) < 1e-4f)
                    {
                        m_FacePoints.Add(m_SectionPoints[m]);
                        sharedCells = sharedCells < 0 ? m_PointCells[m] : sharedCells & m_PointCells[m];
                    }
                }

                if (m_FacePoints.Count < 3)
                    continue;

                var faceColour = Color.white;

                if (m_ColourByCell)
                {
                    // Lowest set bit is the cell this face sits in. A face on more than one
                    // is degenerate, so taking the first is enough.
                    var cell = -1;

                    for (var bit = 0; bit < 8; bit++)
                    {
                        if (sharedCells > 0 && (sharedCells & (1 << bit)) != 0)
                        {
                            cell = bit;
                            break;
                        }
                    }

                    if (cell >= 0)
                        faceColour = ColourForCell(cell);
                }

                var faceCentre = Vector3.zero;

                foreach (var p in m_FacePoints)
                    faceCentre += p;

                faceCentre /= m_FacePoints.Count;

                // Sort the face's points by angle around its centre so the fan does not
                // self-intersect.
                var basisX = (m_FacePoints[0] - faceCentre).normalized;
                var basisY = Vector3.Cross(plane.normal, basisX);

                m_FacePoints.Sort((p, q) =>
                {
                    var pa = Mathf.Atan2(Vector3.Dot(p - faceCentre, basisY), Vector3.Dot(p - faceCentre, basisX));
                    var qa = Mathf.Atan2(Vector3.Dot(q - faceCentre, basisY), Vector3.Dot(q - faceCentre, basisX));
                    return pa.CompareTo(qa);
                });

                for (var f = 0; f < m_FacePoints.Count; f++)
                {
                    var next = (f + 1) % m_FacePoints.Count;

                    m_Vertices.Add(faceCentre);
                    m_Vertices.Add(m_FacePoints[f]);
                    m_Vertices.Add(m_FacePoints[next]);

                    m_Colours.Add(faceColour);
                    m_Colours.Add(faceColour);
                    m_Colours.Add(faceColour);

                    var baseIndex = m_Vertices.Count - 3;
                    m_Triangles.Add(baseIndex);
                    m_Triangles.Add(baseIndex + 1);
                    m_Triangles.Add(baseIndex + 2);
                }
            }
        }

        struct HullPlane
        {
            public Vector3 normal;
            public float offset;
        }
    }
}
