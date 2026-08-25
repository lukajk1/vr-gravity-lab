using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Links this body to another with a springy chain and draws the connection. The joint
    /// is slack within a range and only pulls once the bodies drift beyond it, which reads
    /// like a chain rather than a rigid bar.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class PhysicsTether : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Body at the far end of the chain")]
        Rigidbody m_ConnectedBody;

        [SerializeField]
        [Tooltip("Bodies closer than this feel no pull, giving the chain its slack")]
        float m_MinDistance = 0.4f;

        [SerializeField]
        [Tooltip("Beyond this the spring starts pulling them back together")]
        float m_MaxDistance = 1.2f;

        [SerializeField]
        [Tooltip("Stiffness of the pull past the slack range. Higher snaps taut harder.")]
        float m_Spring = 40f;

        [SerializeField]
        [Tooltip("Damping on the spring, which stops the chain oscillating forever")]
        float m_Damper = 4f;

        [SerializeField]
        [Tooltip("Force needed to snap the chain. Infinity never breaks.")]
        float m_BreakForce = Mathf.Infinity;

        [SerializeField]
        [Tooltip("Let the two linked bodies collide with each other")]
        bool m_LinkedBodiesCollide = true;

        [Header("Visualisation")]
        [SerializeField]
        [Tooltip("Draw a line between the two bodies")]
        bool m_DrawLine = true;

        [SerializeField]
        [Tooltip("Number of points in the drawn line. More points let it sag and curve.")]
        [Range(2, 32)]
        int m_LineSegments = 12;

        [SerializeField]
        [Tooltip("How far the slack chain droops at its midpoint, in metres")]
        float m_Sag = 0.15f;

        [SerializeField]
        [Tooltip("Colour when the chain is slack")]
        Color m_SlackColor = new Color(0.6f, 0.6f, 0.65f);

        [SerializeField]
        [Tooltip("Colour when the chain is stretched taut and pulling")]
        Color m_TautColor = new Color(1f, 0.75f, 0.2f);

        [SerializeField]
        [Tooltip("Thickness of the drawn line, in metres")]
        float m_LineWidth = 0.012f;

        [SerializeField]
        [Tooltip("Material for the line. Leave empty to generate an unlit one at runtime.")]
        Material m_LineMaterial;

        Rigidbody m_Rigidbody;
        SpringJoint m_Joint;
        LineRenderer m_Line;
        Material m_RuntimeMaterial;

        /// <summary>The body this one is chained to.</summary>
        public Rigidbody connectedBody => m_ConnectedBody;

        /// <summary>Whether the chain is currently stretched past its slack range.</summary>
        public bool isTaut
        {
            get
            {
                if (m_ConnectedBody == null)
                    return false;

                return Vector3.Distance(m_Rigidbody.worldCenterOfMass, m_ConnectedBody.worldCenterOfMass) > m_MaxDistance;
            }
        }

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
        }

        void Start()
        {
            BuildJoint();

            if (m_DrawLine)
                BuildLine();
        }

        void OnDestroy()
        {
            if (m_Line != null)
                Destroy(m_Line.gameObject);

            if (m_RuntimeMaterial != null)
                Destroy(m_RuntimeMaterial);
        }

        void LateUpdate()
        {
            if (m_Line == null)
                return;

            if (m_ConnectedBody == null)
            {
                m_Line.enabled = false;
                return;
            }

            m_Line.enabled = true;
            DrawChain();
        }

        void BuildJoint()
        {
            if (m_ConnectedBody == null)
                return;

            m_Joint = gameObject.AddComponent<SpringJoint>();
            m_Joint.connectedBody = m_ConnectedBody;

            // Anchors at the centres, so the chain pulls through the bodies' middles.
            m_Joint.autoConfigureConnectedAnchor = false;
            m_Joint.anchor = Vector3.zero;
            m_Joint.connectedAnchor = Vector3.zero;

            // The gap between min and max is the slack: no force is applied inside it.
            m_Joint.minDistance = m_MinDistance;
            m_Joint.maxDistance = m_MaxDistance;
            m_Joint.spring = m_Spring;
            m_Joint.damper = m_Damper;
            m_Joint.breakForce = m_BreakForce;
            m_Joint.enableCollision = m_LinkedBodiesCollide;
        }

        void BuildLine()
        {
            // Parented to the scene root so the line is not distorted by either body's scale.
            var go = new GameObject(name + " Chain");
            go.transform.SetParent(null);

            m_Line = go.AddComponent<LineRenderer>();
            m_Line.useWorldSpace = true;
            m_Line.positionCount = m_LineSegments;
            m_Line.widthMultiplier = m_LineWidth;
            m_Line.numCapVertices = 2;
            m_Line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_Line.receiveShadows = false;
            m_Line.material = ResolveMaterial();
        }

        /// <summary>
        /// Lays the line out between the two bodies with a droop in the middle that shrinks
        /// as the chain is pulled taut, and recolours it once the spring starts pulling.
        /// </summary>
        void DrawChain()
        {
            var from = m_Rigidbody.worldCenterOfMass;
            var to = m_ConnectedBody.worldCenterOfMass;
            var distance = Vector3.Distance(from, to);

            // Slack goes to zero as the chain approaches its limit, so a taut chain is straight.
            var slackFraction = m_MaxDistance > 0f
                ? Mathf.Clamp01(1f - distance / m_MaxDistance)
                : 0f;
            var sag = m_Sag * slackFraction;

            for (var i = 0; i < m_LineSegments; i++)
            {
                var t = i / (float)(m_LineSegments - 1);
                var point = Vector3.Lerp(from, to, t);

                // Parabolic droop, strongest at the midpoint and zero at both ends.
                point.y -= sag * 4f * t * (1f - t);
                m_Line.SetPosition(i, point);
            }

            var color = distance > m_MaxDistance ? m_TautColor : m_SlackColor;
            m_Line.startColor = color;
            m_Line.endColor = color;
        }

        Material ResolveMaterial()
        {
            if (m_LineMaterial != null)
                return m_LineMaterial;

            if (m_RuntimeMaterial == null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
                m_RuntimeMaterial = new Material(shader);
            }

            return m_RuntimeMaterial;
        }

        void OnDrawGizmosSelected()
        {
            if (m_ConnectedBody == null)
                return;

            Gizmos.color = m_TautColor;
            Gizmos.DrawLine(transform.position, m_ConnectedBody.transform.position);
        }
    }
}
