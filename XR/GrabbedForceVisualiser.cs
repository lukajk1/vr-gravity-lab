using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GravityLab
{
    /// <summary>
    /// While this object is held by an XR interactor, draws a line renderer for each force
    /// that would be acting on it. A held body is kinematic, so nothing is really being
    /// applied; the vectors are computed from the same maths the physics would use, which
    /// lets you see what will happen the moment you let go.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabbedForceVisualiser : MonoBehaviour
    {
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
        [Tooltip("Material for the lines. Leave empty to generate an unlit one at runtime.")]
        Material m_LineMaterial;

        Rigidbody m_Rigidbody;
        XRGrabInteractable m_Grab;
        Material m_RuntimeMaterial;

        // One renderer for gravity, then one per attractor found in the scene.
        LineRenderer m_GravityLine;
        readonly List<LineRenderer> m_AttractorLines = new List<LineRenderer>();
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
            SetLinesVisible(false);
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

            EnsureLines();
            SetLinesVisible(true);
        }

        void OnReleased(SelectExitEventArgs args)
        {
            SetLinesVisible(false);
        }

        void LateUpdate()
        {
            if (!m_Grab.isSelected)
                return;

            var origin = m_Rigidbody.worldCenterOfMass;

            // Unity applies gravity only when the body asks for it.
            if (m_GravityLine != null)
                DrawVector(m_GravityLine, origin, m_Rigidbody.useGravity ? Physics.gravity : Vector3.zero);

            for (var i = 0; i < m_AttractorLines.Count; i++)
            {
                var attractor = m_Attractors[i];
                var acceleration = attractor == null
                    ? Vector3.zero
                    : attractor.GetAccelerationAt(origin);

                DrawVector(m_AttractorLines[i], origin, acceleration);
            }
        }

        /// <summary>
        /// Points one line renderer along an acceleration vector, scaled for readability.
        /// A vector below the minimum length hides the renderer rather than drawing a dot.
        /// </summary>
        void DrawVector(LineRenderer line, Vector3 origin, Vector3 acceleration)
        {
            var length = acceleration.magnitude * m_MetresPerUnit;

            if (length < m_MinDrawLength)
            {
                line.enabled = false;
                return;
            }

            length = Mathf.Min(length, m_MaxDrawLength);

            line.enabled = true;
            line.SetPosition(0, origin);
            line.SetPosition(1, origin + acceleration.normalized * length);
        }

        void EnsureLines()
        {
            if (m_GravityLine == null)
                m_GravityLine = CreateLine("Gravity Vector", m_GravityColor);

            // Grow or shrink the pool to match the attractors present.
            while (m_AttractorLines.Count < m_Attractors.Count)
                m_AttractorLines.Add(CreateLine("Attractor Vector " + m_AttractorLines.Count, m_AttractorColor));

            for (var i = m_AttractorLines.Count - 1; i >= m_Attractors.Count; i--)
            {
                if (m_AttractorLines[i] != null)
                    Destroy(m_AttractorLines[i].gameObject);

                m_AttractorLines.RemoveAt(i);
            }
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

        void SetLinesVisible(bool visible)
        {
            if (m_GravityLine != null)
                m_GravityLine.enabled = visible;

            foreach (var line in m_AttractorLines)
            {
                if (line != null)
                    line.enabled = visible;
            }
        }
    }
}
