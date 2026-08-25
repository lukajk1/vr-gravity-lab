using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GravityLab
{
    /// <summary>
    /// While this object is held by an XR interactor, draws an arrow for each force that
    /// would be acting on it. A held body is kinematic, so nothing is really being applied;
    /// the vectors are computed from the same maths the physics would use, which lets you
    /// see what will happen the moment you let go.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabbedForceVisualiser : MonoBehaviour
    {
        /// <summary>
        /// One drawn vector: a shaft plus two barbs forming the arrowhead, with the
        /// smoothing state that keeps it from snapping around as the object is moved.
        /// </summary>
        class Arrow
        {
            public LineRenderer shaft;
            public LineRenderer leftBarb;
            public LineRenderer rightBarb;

            public Vector3 smoothedDirection;
            public float smoothedLength;
            public float lengthVelocity;
            public bool initialised;

            public void SetEnabled(bool value)
            {
                if (shaft != null)
                    shaft.enabled = value;

                if (leftBarb != null)
                    leftBarb.enabled = value;

                if (rightBarb != null)
                    rightBarb.enabled = value;
            }

            public void Destroy()
            {
                if (shaft != null)
                    Object.Destroy(shaft.gameObject);

                if (leftBarb != null)
                    Object.Destroy(leftBarb.gameObject);

                if (rightBarb != null)
                    Object.Destroy(rightBarb.gameObject);
            }
        }

        [SerializeField]
        [Tooltip("Colour of the constant downward gravity vector. Only drawn when the rigidbody has useGravity enabled.")]
        Color m_GravityColor = new Color(0.2f, 0.6f, 1f);

        [SerializeField]
        [Tooltip("Colour of the pull from each attractor")]
        Color m_AttractorColor = Color.black;

        [SerializeField]
        [Tooltip("Metres drawn per unit of acceleration. Purely visual scaling.")]
        float m_MetresPerUnit = 0.05f;

        [SerializeField]
        [Tooltip("Vectors shorter than this are not drawn, to avoid specks at the origin")]
        float m_MinDrawLength = 0.02f;

        [SerializeField]
        [Tooltip("Longest a vector may be drawn, so a strong pull cannot fill the play space")]
        float m_MaxDrawLength = 2f;

        [SerializeField]
        [Tooltip("Thickness of the drawn lines, in metres")]
        float m_LineWidth = 0.01f;

        [SerializeField]
        [Tooltip("Seconds for the arrow direction to catch up. Higher is smoother and laggier; zero is instant.")]
        float m_DirectionSmoothTime = 0.15f;

        [SerializeField]
        [Tooltip("Seconds for the arrow length to catch up as the force changes magnitude")]
        float m_LengthSmoothTime = 0.2f;

        [SerializeField]
        [Tooltip("Length of each arrowhead barb, as a fraction of the shaft length")]
        [Range(0.05f, 0.5f)]
        float m_HeadLengthFraction = 0.18f;

        [SerializeField]
        [Tooltip("Longest an arrowhead barb may be, in metres, so long arrows do not grow huge heads")]
        float m_MaxHeadLength = 0.12f;

        [SerializeField]
        [Tooltip("Angle between each barb and the shaft, in degrees")]
        [Range(10f, 60f)]
        float m_HeadAngle = 25f;

        [SerializeField]
        [Tooltip("Material for the lines. Leave empty to generate an unlit one at runtime.")]
        Material m_LineMaterial;

        Rigidbody m_Rigidbody;
        XRGrabInteractable m_Grab;
        Material m_RuntimeMaterial;

        Arrow m_GravityArrow;
        readonly List<Arrow> m_AttractorArrows = new List<Arrow>();
        readonly List<GravitationalAttractor> m_Attractors = new List<GravitationalAttractor>();

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            m_Grab = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            m_Grab.selectEntered.AddListener(OnGrabbed);
            m_Grab.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            m_Grab.selectEntered.RemoveListener(OnGrabbed);
            m_Grab.selectExited.RemoveListener(OnReleased);
            SetArrowsVisible(false);
        }

        void OnDestroy()
        {
            if (m_RuntimeMaterial != null)
                Destroy(m_RuntimeMaterial);
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            // Attractors can be added or removed between grabs, so rebuild each time.
            m_Attractors.Clear();
            m_Attractors.AddRange(FindObjectsByType<GravitationalAttractor>(FindObjectsSortMode.None));

            EnsureArrows();

            // Start from the true values so the first frame does not sweep in from stale state.
            ResetSmoothing();
            SetArrowsVisible(true);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            SetArrowsVisible(false);
        }

        void LateUpdate()
        {
            if (!m_Grab.isSelected)
                return;

            var origin = m_Rigidbody.worldCenterOfMass;

            // Unity applies gravity only when the body asks for it.
            if (m_GravityArrow != null)
                DrawArrow(m_GravityArrow, origin, m_Rigidbody.useGravity ? Physics.gravity : Vector3.zero);

            for (var i = 0; i < m_AttractorArrows.Count; i++)
            {
                var attractor = m_Attractors[i];
                var acceleration = attractor == null
                    ? Vector3.zero
                    : attractor.GetAccelerationAt(origin);

                DrawArrow(m_AttractorArrows[i], origin, acceleration);
            }
        }

        /// <summary>
        /// Lays out one arrow along an acceleration vector, scaled for readability. Direction
        /// and length are smoothed separately so the arrow swings rather than snaps while the
        /// object is being waved about. A vector below the minimum length hides the arrow.
        /// </summary>
        void DrawArrow(Arrow arrow, Vector3 origin, Vector3 acceleration)
        {
            var rawLength = Mathf.Min(acceleration.magnitude * m_MetresPerUnit, m_MaxDrawLength);
            var rawDirection = acceleration.sqrMagnitude > 1e-8f
                ? acceleration.normalized
                : arrow.smoothedDirection;

            if (!arrow.initialised)
            {
                arrow.smoothedDirection = rawDirection;
                arrow.smoothedLength = rawLength;
                arrow.lengthVelocity = 0f;
                arrow.initialised = true;
            }
            else
            {
                // Slerp keeps the tip sweeping along an arc instead of cutting across.
                arrow.smoothedDirection = m_DirectionSmoothTime > 0f
                    ? Vector3.Slerp(arrow.smoothedDirection, rawDirection,
                        1f - Mathf.Exp(-Time.deltaTime / m_DirectionSmoothTime)).normalized
                    : rawDirection;

                arrow.smoothedLength = m_LengthSmoothTime > 0f
                    ? Mathf.SmoothDamp(arrow.smoothedLength, rawLength, ref arrow.lengthVelocity,
                        m_LengthSmoothTime, Mathf.Infinity, Time.deltaTime)
                    : rawLength;
            }

            if (arrow.smoothedLength < m_MinDrawLength || arrow.smoothedDirection.sqrMagnitude < 1e-8f)
            {
                arrow.SetEnabled(false);
                return;
            }

            arrow.SetEnabled(true);

            var direction = arrow.smoothedDirection;
            var tip = origin + direction * arrow.smoothedLength;

            arrow.shaft.SetPosition(0, origin);
            arrow.shaft.SetPosition(1, tip);

            // Barbs sweep back from the tip, splayed either side of the shaft. Any axis
            // perpendicular to the shaft works; pick the more stable of two candidates so
            // the head does not flip when the arrow points near world up.
            var reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
                ? Vector3.right
                : Vector3.up;
            var side = Vector3.Normalize(Vector3.Cross(direction, reference));

            var headLength = Mathf.Min(arrow.smoothedLength * m_HeadLengthFraction, m_MaxHeadLength);
            var back = -direction * Mathf.Cos(m_HeadAngle * Mathf.Deg2Rad) * headLength;
            var out1 = side * Mathf.Sin(m_HeadAngle * Mathf.Deg2Rad) * headLength;

            arrow.leftBarb.SetPosition(0, tip);
            arrow.leftBarb.SetPosition(1, tip + back + out1);

            arrow.rightBarb.SetPosition(0, tip);
            arrow.rightBarb.SetPosition(1, tip + back - out1);
        }

        void ResetSmoothing()
        {
            if (m_GravityArrow != null)
                m_GravityArrow.initialised = false;

            foreach (var arrow in m_AttractorArrows)
                arrow.initialised = false;
        }

        void EnsureArrows()
        {
            if (m_GravityArrow == null)
                m_GravityArrow = CreateArrow("Gravity Vector", m_GravityColor);

            // Grow or shrink the pool to match the attractors present.
            while (m_AttractorArrows.Count < m_Attractors.Count)
                m_AttractorArrows.Add(CreateArrow("Attractor Vector " + m_AttractorArrows.Count, m_AttractorColor));

            for (var i = m_AttractorArrows.Count - 1; i >= m_Attractors.Count; i--)
            {
                m_AttractorArrows[i].Destroy();
                m_AttractorArrows.RemoveAt(i);
            }
        }

        Arrow CreateArrow(string arrowName, Color color)
        {
            return new Arrow
            {
                shaft = CreateLine(arrowName + " Shaft", color),
                leftBarb = CreateLine(arrowName + " Barb L", color),
                rightBarb = CreateLine(arrowName + " Barb R", color),
            };
        }

        LineRenderer CreateLine(string lineName, Color color)
        {
            // Parented to the scene root, not this object, so the line is positioned in
            // world space and does not inherit the held object's scale or rotation.
            var go = new GameObject(lineName);
            go.transform.SetParent(null);

            var line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.widthMultiplier = m_LineWidth;
            line.numCapVertices = 2;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            line.material = ResolveMaterial();
            line.startColor = color;
            line.endColor = color;
            line.enabled = false;

            return line;
        }

        Material ResolveMaterial()
        {
            if (m_LineMaterial != null)
                return m_LineMaterial;

            if (m_RuntimeMaterial == null)
            {
                // Unlit so the vectors read as diagram lines rather than lit geometry, and
                // so black stays black instead of picking up scene lighting.
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                m_RuntimeMaterial = new Material(shader);
            }

            return m_RuntimeMaterial;
        }

        void SetArrowsVisible(bool visible)
        {
            if (m_GravityArrow != null)
                m_GravityArrow.SetEnabled(visible);

            foreach (var arrow in m_AttractorArrows)
                arrow.SetEnabled(visible);
        }
    }
}
