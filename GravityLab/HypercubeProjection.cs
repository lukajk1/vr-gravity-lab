using System.Collections.Generic;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Draws a whole 4D hypercube projected into 3D, the way a 3D object is projected onto a
    /// 2D screen: dividing by the depth coordinate. Where <see cref="TesseractSlice"/> shows
    /// one 3D cross-section, this shows all sixteen vertices and all eight cells at once, so
    /// the familiar cube-within-a-cube appears and turns itself inside out as it rotates.
    /// </summary>
    /// <remarks>
    /// Unlike the slice, the connectivity here never changes: a hypercube always has 16
    /// vertices, 32 edges and 24 square faces however it is turned. Only their projected
    /// positions move, so there is no hull to search and the per-frame cost is a handful of
    /// transforms rather than the slice's triple loop.
    /// </remarks>
    [RequireComponent(typeof(MeshFilter))]
    [RequireComponent(typeof(MeshRenderer))]
    public class HypercubeProjection : MonoBehaviour, IFourDimensionalRotatable
    {
        [SerializeField]
        [Tooltip("Half-width of the hypercube along each of its four axes")]
        float m_Size = 0.3f;

        [Header("Projection")]
        [SerializeField]
        [Tooltip("Distance of the 4D viewpoint along w. Larger flattens the projection; as it approaches the hypercube's own size the inner cube swells dramatically.")]
        float m_ViewerDistance = 2.5f;

        [Header("Rotation")]
        [SerializeField]
        [Tooltip("Angle in each of the six rotation planes, in degrees, in the order XY, XZ, YZ, XW, YW, ZW")]
        float[] m_PlaneAngles = new float[6];

        [SerializeField]
        [Tooltip("Degrees per second added to each plane's angle, in the same order")]
        float[] m_PlaneSpeeds = { 0f, 0f, 0f, 18f, 0f, 27f };

        [Header("Geometry")]
        [SerializeField]
        [Tooltip("Draw the 24 square faces as translucent quads")]
        bool m_DrawFaces = true;

        [SerializeField]
        [Tooltip("Draw the 32 edges as solid bars, which keeps the structure readable through the transparency")]
        bool m_DrawEdges = true;

        [SerializeField]
        [Tooltip("Thickness of each edge bar, in metres")]
        float m_EdgeThickness = 0.006f;

        [SerializeField]
        [Tooltip("How much brighter the edges are than the faces they border")]
        float m_EdgeBrightness = 2.2f;

        [Header("Cell colours")]
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
        [Tooltip("Colour of the +w cell")]
        Color m_PositiveWColour = Color.white;

        [SerializeField]
        [Tooltip("Colour of the -w cell")]
        Color m_NegativeWColour = new Color(0.25f, 0.2f, 0.5f);

        [SerializeField]
        [Tooltip("Multiplies every cell colour, so they survive the blend without looking washed out")]
        float m_ColourGain = 1.4f;

        [Header("Output")]
        [SerializeField]
        [Tooltip("Rebuild this many times a second. Zero rebuilds every frame.")]
        float m_RebuildRate = 60f;

        readonly Vector4[] m_Corners = new Vector4[16];
        readonly Vector3[] m_Projected = new Vector3[16];

        // The 24 square faces. Each is fixed on two axes and spans the other two, so it is
        // found by choosing the two spanning axes and the sign of each fixed one.
        readonly List<int[]> m_Faces = new List<int[]>(24);
        readonly List<int> m_FaceCell = new List<int>(24);

        readonly List<int> m_EdgeA = new List<int>(32);
        readonly List<int> m_EdgeB = new List<int>(32);

        readonly List<Vector3> m_Vertices = new List<Vector3>(512);
        readonly List<Color> m_Colours = new List<Color>(512);
        readonly List<int> m_Triangles = new List<int>(1024);

        Mesh m_Mesh;
        float m_NextRebuildTime;

        void Awake()
        {
            BuildTopology();

            m_Mesh = new Mesh { name = "Hypercube Projection" };
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

            Rebuild();
        }

        /// <summary>
        /// Whether the mesh is due to be rebuilt this frame. Anything feeding this projection
        /// can check it to avoid work whose result would be discarded.
        /// </summary>
        public bool isRebuildDue => m_RebuildRate <= 0f || Time.time >= m_NextRebuildTime;

        /// <summary>Angle in one rotation plane, in degrees.</summary>
        public float GetAngle(TesseractSlice.RotationPlane plane) => m_PlaneAngles[(int)plane];

        /// <summary>Sets the angle in one rotation plane, in degrees.</summary>
        public void SetAngle(TesseractSlice.RotationPlane plane, float degrees)
        {
            m_PlaneAngles[(int)plane] = degrees;
        }

        /// <summary>Sets how fast one rotation plane turns on its own, in degrees per second.</summary>
        public void SetSpeed(TesseractSlice.RotationPlane plane, float degreesPerSecond)
        {
            if (m_PlaneSpeeds.Length < 6)
                System.Array.Resize(ref m_PlaneSpeeds, 6);

            m_PlaneSpeeds[(int)plane] = degreesPerSecond;
        }

        /// <summary>
        /// Works out the fixed connectivity once: which corners form edges, and which four
        /// corners bound each square face.
        /// </summary>
        void BuildTopology()
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
                    var diff = a ^ b;

                    if ((diff & (diff - 1)) == 0)
                    {
                        m_EdgeA.Add(a);
                        m_EdgeB.Add(b);
                    }
                }
            }

            m_Faces.Clear();
            m_FaceCell.Clear();

            // A square face spans two axes and is pinned on the other two. Walking every pair
            // of spanning axes and every combination of the fixed ones yields all 24.
            for (var axisU = 0; axisU < 4; axisU++)
            {
                for (var axisV = axisU + 1; axisV < 4; axisV++)
                {
                    var fixedAxes = new List<int>();

                    for (var axis = 0; axis < 4; axis++)
                    {
                        if (axis != axisU && axis != axisV)
                            fixedAxes.Add(axis);
                    }

                    for (var combination = 0; combination < 4; combination++)
                    {
                        var baseIndex = 0;

                        for (var f = 0; f < fixedAxes.Count; f++)
                        {
                            if ((combination & (1 << f)) != 0)
                                baseIndex |= 1 << fixedAxes[f];
                        }

                        var bitU = 1 << axisU;
                        var bitV = 1 << axisV;

                        // Wound around the square rather than across it, so the quad is not
                        // an hourglass.
                        m_Faces.Add(new[]
                        {
                            baseIndex,
                            baseIndex | bitU,
                            baseIndex | bitU | bitV,
                            baseIndex | bitV,
                        });

                        // Attribute the face to the first fixed axis, which gives each of the
                        // eight cells its own share of the 24 faces.
                        var cellAxis = fixedAxes[0];
                        var cellPositive = (baseIndex & (1 << cellAxis)) != 0;
                        m_FaceCell.Add(cellAxis * 2 + (cellPositive ? 1 : 0));
                    }
                }
            }
        }

        void Rebuild()
        {
            ProjectCorners();

            m_Vertices.Clear();
            m_Colours.Clear();
            m_Triangles.Clear();

            if (m_DrawFaces)
                BuildFaces();

            if (m_DrawEdges)
                BuildEdges();

            m_Mesh.Clear();

            if (m_Vertices.Count >= 3)
            {
                m_Mesh.SetVertices(m_Vertices);
                m_Mesh.SetColors(m_Colours);
                m_Mesh.SetTriangles(m_Triangles, 0, false);
                m_Mesh.RecalculateNormals();
                m_Mesh.RecalculateBounds();
            }
        }

        /// <summary>
        /// Rotates every corner in four dimensions, then divides by its distance from the 4D
        /// viewpoint. This is the same perspective divide that projects 3D onto a screen, one
        /// dimension up: a corner further along w shrinks toward the centre, which is what
        /// makes the hypercube look like a small cube nested inside a large one.
        /// </summary>
        void ProjectCorners()
        {
            for (var i = 0; i < 16; i++)
            {
                var v = m_Corners[i] * m_Size;

                v = RotateInPlane(v, 0, 1, m_PlaneAngles[0]);
                v = RotateInPlane(v, 0, 2, m_PlaneAngles[1]);
                v = RotateInPlane(v, 1, 2, m_PlaneAngles[2]);
                v = RotateInPlane(v, 0, 3, m_PlaneAngles[3]);
                v = RotateInPlane(v, 1, 3, m_PlaneAngles[4]);
                v = RotateInPlane(v, 2, 3, m_PlaneAngles[5]);

                // Guarded so a corner passing through the viewpoint cannot divide by zero.
                var denominator = Mathf.Max(m_ViewerDistance - v.w, 0.05f);
                var scale = m_ViewerDistance / denominator;

                m_Projected[i] = new Vector3(v.x, v.y, v.z) * scale;
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

        void BuildFaces()
        {
            for (var f = 0; f < m_Faces.Count; f++)
            {
                var quad = m_Faces[f];
                var colour = ColourForCell(m_FaceCell[f]);

                var a = m_Projected[quad[0]];
                var b = m_Projected[quad[1]];
                var c = m_Projected[quad[2]];
                var d = m_Projected[quad[3]];

                // Two triangles per quad, and again reversed, so the face is visible from
                // both sides without relying on the shader's cull mode.
                AddTriangle(a, b, c, colour);
                AddTriangle(a, c, d, colour);
                AddTriangle(a, c, b, colour);
                AddTriangle(a, d, c, colour);
            }
        }

        /// <summary>
        /// Builds each edge as a thin bar. Camera-facing quads would be cheaper, but a bar
        /// keeps its thickness when seen from any direction and needs no per-frame billboard.
        /// </summary>
        void BuildEdges()
        {
            for (var e = 0; e < m_EdgeA.Count; e++)
            {
                var start = m_Projected[m_EdgeA[e]];
                var end = m_Projected[m_EdgeB[e]];
                var direction = end - start;

                if (direction.sqrMagnitude < 1e-8f)
                    continue;

                direction.Normalize();

                // Any two axes perpendicular to the edge will do for its cross-section.
                var reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
                    ? Vector3.right
                    : Vector3.up;

                var side = Vector3.Normalize(Vector3.Cross(direction, reference)) * m_EdgeThickness;
                var up = Vector3.Normalize(Vector3.Cross(direction, side)) * m_EdgeThickness;

                // Edges belong to several cells at once, so they take a neutral bright colour
                // rather than any one cell's.
                var colour = Color.white * m_EdgeBrightness;
                colour.a = 1f;

                AddQuad(start - side, start + side, end + side, end - side, colour);
                AddQuad(start - up, start + up, end + up, end - up, colour);
            }
        }

        void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color colour)
        {
            AddTriangle(a, b, c, colour);
            AddTriangle(a, c, d, colour);
            AddTriangle(a, c, b, colour);
            AddTriangle(a, d, c, colour);
        }

        void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color colour)
        {
            var baseIndex = m_Vertices.Count;

            m_Vertices.Add(a);
            m_Vertices.Add(b);
            m_Vertices.Add(c);

            m_Colours.Add(colour);
            m_Colours.Add(colour);
            m_Colours.Add(colour);

            m_Triangles.Add(baseIndex);
            m_Triangles.Add(baseIndex + 1);
            m_Triangles.Add(baseIndex + 2);
        }

        Color ColourForCell(int cell)
        {
            var axis = cell / 2;
            var positive = (cell & 1) != 0;

            if (axis == 3)
                return Boost(positive ? m_PositiveWColour : m_NegativeWColour);

            var baseColour = axis < m_AxisColours.Length ? m_AxisColours[axis] : Color.grey;

            if (!positive)
            {
                var dim = 1f - m_NegativeCellDarkening;
                baseColour = new Color(baseColour.r * dim, baseColour.g * dim, baseColour.b * dim, baseColour.a);
            }

            return Boost(baseColour);
        }

        Color Boost(Color colour)
        {
            return new Color(colour.r * m_ColourGain, colour.g * m_ColourGain, colour.b * m_ColourGain, colour.a);
        }
    }
}
