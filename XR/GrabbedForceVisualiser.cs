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

            public GameObject label;
            public TMPro.TMP_Text text;
            public Vector3 labelVelocity;
            public bool labelPlaced;

            public Vector3 smoothedDirection;
            public float smoothedLength;
            public float lengthVelocity;

            public Vector3 smoothedOrigin;
            public Vector3 originVelocity;

            // Kept between frames so the arrowhead plane drifts instead of snapping when
            // the shaft swings past the axis used to derive it.
            public Vector3 barbAxis;

            public bool initialised;

            public void SetEnabled(bool value)
            {
                if (shaft != null)
                    shaft.enabled = value;

                if (leftBarb != null)
                    leftBarb.enabled = value;

                if (rightBarb != null)
                    rightBarb.enabled = value;

                if (label != null)
                    label.SetActive(value);
            }

            public void Destroy()
            {
                if (shaft != null)
                    Object.Destroy(shaft.gameObject);

                if (leftBarb != null)
                    Object.Destroy(leftBarb.gameObject);

                if (rightBarb != null)
                    Object.Destroy(rightBarb.gameObject);

                if (label != null)
                    Object.Destroy(label);
            }
        }

        [SerializeField]
        [Tooltip("Colour of the constant downward gravity vector. Only drawn when the rigidbody has useGravity enabled.")]
        Color m_GravityColor = new Color(0.2f, 0.6f, 1f);

        [SerializeField]
        [Tooltip("Colour of the pull from each attractor. Yellow reads clearly against both the dark scene and the pale objects.")]
        Color m_AttractorColor = new Color(1f, 0.85f, 0.15f);

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
        [Tooltip("Seconds for the arrow's tail to catch up to the object. Smooths out the jitter of a tumbling body; zero pins the tail rigidly.")]
        float m_OriginSmoothTime = 0.08f;

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

        [Header("Arrow labels")]
        [SerializeField]
        [Tooltip("Label prefab pinned to each arrow's head. Needs a TMP_Text in its hierarchy.")]
        GameObject m_LabelPrefab;

        [SerializeField]
        [Tooltip("Offset from the arrow tip where its label sits, in metres")]
        Vector3 m_LabelOffset = new Vector3(0f, 0.12f, 0f);

        [SerializeField]
        [Tooltip("Seconds the label takes to catch up to the arrow head. Matches the object labels' generous damping.")]
        float m_LabelSmoothTime = 0.35f;

        [SerializeField]
        [Tooltip("Seconds between label text refreshes. Zero updates every frame.")]
        float m_LabelRefreshInterval = 0.05f;

        [SerializeField]
        [Tooltip("Log each arrow's computed state once per grab, to diagnose an arrow that will not appear")]
        bool m_LogArrowState;

        Rigidbody m_Rigidbody;
        XRGrabInteractable m_Grab;
        Material m_RuntimeMaterial;

        float m_NextLabelRefreshTime;

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

            if (m_LogArrowState)
                LogArrowState();
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
            var refresh = m_LabelRefreshInterval <= 0f || Time.time >= m_NextLabelRefreshTime;

            if (refresh)
                m_NextLabelRefreshTime = Time.time + m_LabelRefreshInterval;

            // Unity applies gravity only when the body asks for it.
            if (m_GravityArrow != null)
            {
                var gravity = m_Rigidbody.useGravity ? Physics.gravity : Vector3.zero;
                DrawArrow(m_GravityArrow, origin, gravity);

                if (refresh)
                    SetLabel(m_GravityArrow, $"gravity\n{gravity.magnitude:0.0} m/s²");
            }

            for (var i = 0; i < m_AttractorArrows.Count; i++)
            {
                var attractor = m_Attractors[i];
                var acceleration = attractor == null
                    ? Vector3.zero
                    : attractor.GetAccelerationAt(origin);

                DrawArrow(m_AttractorArrows[i], origin, acceleration);

                if (!refresh)
                    continue;

                var caption = m_AttractorArrows.Count > 1 ? $"attractor {i + 1}" : "attractor";

                if (attractor == null)
                {
                    SetLabel(m_AttractorArrows[i], caption);
                    continue;
                }

                var distance = Vector3.Distance(origin, attractor.transform.position);
                SetLabel(m_AttractorArrows[i], $"{caption}\n{acceleration.magnitude:0.0} m/s²\n@ {distance:0.0} m");
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
                arrow.smoothedOrigin = origin;
                arrow.originVelocity = Vector3.zero;
                arrow.barbAxis = Vector3.zero;
                arrow.initialised = true;
            }
            else
            {
                // The tail follows the body rather than being welded to it, so a tumbling
                // object does not transmit its jitter into the whole arrow.
                arrow.smoothedOrigin = m_OriginSmoothTime > 0f
                    ? Vector3.SmoothDamp(arrow.smoothedOrigin, origin, ref arrow.originVelocity,
                        m_OriginSmoothTime, Mathf.Infinity, Time.deltaTime)
                    : origin;

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
            var start = arrow.smoothedOrigin;
            var tip = start + direction * arrow.smoothedLength;

            arrow.shaft.SetPosition(0, start);
            arrow.shaft.SetPosition(1, tip);

            // Barbs sweep back from the tip, splayed either side of the shaft. Any axis
            // perpendicular to the shaft works, so carry the previous frame's axis forward
            // and only re-derive it when the shaft has swung too close to it. Switching on a
            // hard threshold instead would spin the arrowhead as the arrow passed vertical.
            var side = Vector3.ProjectOnPlane(arrow.barbAxis, direction);

            if (side.sqrMagnitude < 1e-6f)
            {
                var reference = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) > 0.95f
                    ? Vector3.right
                    : Vector3.up;
                side = Vector3.Cross(direction, reference);
            }

            side = side.normalized;
            arrow.barbAxis = side;

            var headLength = Mathf.Min(arrow.smoothedLength * m_HeadLengthFraction, m_MaxHeadLength);
            var back = -direction * Mathf.Cos(m_HeadAngle * Mathf.Deg2Rad) * headLength;
            var out1 = side * Mathf.Sin(m_HeadAngle * Mathf.Deg2Rad) * headLength;

            arrow.leftBarb.SetPosition(0, tip);
            arrow.leftBarb.SetPosition(1, tip + back + out1);

            arrow.rightBarb.SetPosition(0, tip);
            arrow.rightBarb.SetPosition(1, tip + back - out1);

            PlaceLabel(arrow, tip);
        }

        /// <summary>
        /// Eases the arrow's label toward its head. The arrow itself is already smoothed, so
        /// this is mostly to keep the text from tracking every twitch of the tip.
        /// </summary>
        void PlaceLabel(Arrow arrow, Vector3 tip)
        {
            if (arrow.label == null)
                return;

            var target = tip + m_LabelOffset;
            var labelTransform = arrow.label.transform;

            // First frame after a grab snaps, so the label does not fly in from the last one.
            if (!arrow.labelPlaced || m_LabelSmoothTime <= 0f)
            {
                labelTransform.position = target;
                arrow.labelVelocity = Vector3.zero;
                arrow.labelPlaced = true;
                return;
            }

            labelTransform.position = Vector3.SmoothDamp(labelTransform.position, target,
                ref arrow.labelVelocity, m_LabelSmoothTime, Mathf.Infinity, Time.deltaTime);
        }

        void SetLabel(Arrow arrow, string value)
        {
            if (arrow.text != null)
                arrow.text.text = value;
        }

        /// <summary>
        /// Reports what each arrow was actually given, so an arrow that never appears can be
        /// traced to its source: no renderer, a zero vector, or a length below the cull.
        /// </summary>
        void LogArrowState()
        {
            var origin = m_Rigidbody.worldCenterOfMass;
            var gravity = m_Rigidbody.useGravity ? Physics.gravity : Vector3.zero;

            Debug.Log($"[{name}] useGravity={m_Rigidbody.useGravity} gravity={gravity} " +
                $"drawLen={Mathf.Min(gravity.magnitude * m_MetresPerUnit, m_MaxDrawLength):F4} " +
                $"minDraw={m_MinDrawLength} " +
                $"shaft={(m_GravityArrow?.shaft != null ? "ok" : "NULL")} " +
                $"enabled={(m_GravityArrow?.shaft != null && m_GravityArrow.shaft.enabled)}", this);

            for (var i = 0; i < m_AttractorArrows.Count; i++)
            {
                var a = m_Attractors[i] == null ? Vector3.zero : m_Attractors[i].GetAccelerationAt(origin);
                Debug.Log($"[{name}] attractor {i} accel={a.magnitude:F3} " +
                    $"drawLen={Mathf.Min(a.magnitude * m_MetresPerUnit, m_MaxDrawLength):F4} " +
                    $"shaft={(m_AttractorArrows[i].shaft != null ? "ok" : "NULL")}", this);
            }
        }

        void ResetSmoothing()
        {
            if (m_GravityArrow != null)
            {
                m_GravityArrow.initialised = false;
                m_GravityArrow.labelPlaced = false;
            }

            foreach (var arrow in m_AttractorArrows)
            {
                arrow.initialised = false;
                arrow.labelPlaced = false;
            }

            m_NextLabelRefreshTime = 0f;
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
            var arrow = new Arrow
            {
                shaft = CreateLine(arrowName + " Shaft", color),
                leftBarb = CreateLine(arrowName + " Barb L", color),
                rightBarb = CreateLine(arrowName + " Barb R", color),
            };

            if (m_LabelPrefab != null)
            {
                // Parented to the scene root so the label keeps world scale and does not
                // inherit the held object's rotation.
                arrow.label = Instantiate(m_LabelPrefab);
                arrow.label.name = arrowName + " Label";
                arrow.text = arrow.label.GetComponentInChildren<TMPro.TMP_Text>();
                arrow.label.SetActive(false);
            }

            return arrow;
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

            // Below the labels' 100, so text still wins where the two overlap.
            line.sortingOrder = 50;
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
                // The overlay variant ignores depth, so an arrow stays visible through the
                // object it describes. Unlit either way, so black stays black rather than
                // picking up scene lighting.
                var shader = Shader.Find("GravityLab/Line Overlay")
                    ?? Shader.Find("Universal Render Pipeline/Unlit")
                    ?? Shader.Find("Unlit/Color");
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
