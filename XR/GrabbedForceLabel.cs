using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GravityLab
{
    /// <summary>
    /// While this object is held, shows a worldspace label that tracks it and reports the
    /// object's own properties plus the net acceleration on it. The per-force breakdown
    /// lives on the arrow heads instead, drawn by <see cref="GrabbedForceVisualiser"/>.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabbedForceLabel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Label prefab to spawn while held. Needs a TMP_Text somewhere in its hierarchy.")]
        GameObject m_LabelPrefab;

        [SerializeField]
        [Tooltip("Offset from the object's centre of mass, in metres, where the label sits")]
        Vector3 m_Offset = new Vector3(0f, 0.25f, 0f);

        [SerializeField]
        [Tooltip("Seconds the label takes to catch up to the object. Higher is looser and floatier; zero pins it rigidly.")]
        float m_FollowSmoothTime = 0.35f;

        [SerializeField]
        [Tooltip("Cap on how fast the label may travel, in metres per second. Zero or less is uncapped.")]
        float m_MaxFollowSpeed;

        [SerializeField]
        [Tooltip("Jump straight to the target on grab instead of flying in from the last position")]
        bool m_SnapOnGrab = true;

        [SerializeField]
        [Tooltip("Show the resultant of gravity and every attractor as a net line")]
        bool m_ShowNet = true;

        [SerializeField]
        [Tooltip("Show this object's mass. Mass does not affect the pull, since the attractor applies acceleration, but it does affect collisions.")]
        bool m_ShowMass = true;

        [SerializeField]
        [Tooltip("Seconds between refreshes. Zero updates every frame.")]
        float m_RefreshInterval = 0.05f;

        Rigidbody m_Rigidbody;
        XRGrabInteractable m_Grab;
        GameObject m_LabelInstance;
        TMP_Text m_Text;
        float m_NextRefreshTime;
        Vector3 m_FollowVelocity;

        readonly List<GravitationalAttractor> m_Attractors = new List<GravitationalAttractor>();
        readonly StringBuilder m_Builder = new StringBuilder();

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
            HideLabel();
        }

        void OnDestroy()
        {
            if (m_LabelInstance != null)
                Destroy(m_LabelInstance);
        }

        void OnGrabbed(SelectEnterEventArgs args)
        {
            // Attractors can come and go between grabs, so rebuild the list each time.
            m_Attractors.Clear();
            m_Attractors.AddRange(FindObjectsByType<GravitationalAttractor>(FindObjectsSortMode.None));

            if (m_LabelInstance == null && m_LabelPrefab != null)
            {
                // Parented to the scene root so the label does not inherit this object's
                // scale or spin with it.
                m_LabelInstance = Instantiate(m_LabelPrefab);
                m_Text = m_LabelInstance.GetComponentInChildren<TMP_Text>();
            }

            if (m_LabelInstance != null)
            {
                m_LabelInstance.SetActive(true);

                // Without this the label would sail in from wherever it was left last time.
                if (m_SnapOnGrab)
                {
                    m_LabelInstance.transform.position = m_Rigidbody.worldCenterOfMass + m_Offset;
                    m_FollowVelocity = Vector3.zero;
                }
            }

            m_NextRefreshTime = 0f;
        }

        void OnReleased(SelectExitEventArgs args)
        {
            HideLabel();
        }

        void LateUpdate()
        {
            if (!m_Grab.isSelected || m_LabelInstance == null)
                return;

            var origin = m_Rigidbody.worldCenterOfMass;
            var targetPosition = origin + m_Offset;
            var labelTransform = m_LabelInstance.transform;

            if (m_FollowSmoothTime > 0f)
            {
                // Critically damped follow, so the label trails the object and settles
                // without overshooting no matter how hard the object is thrown around.
                labelTransform.position = m_MaxFollowSpeed > 0f
                    ? Vector3.SmoothDamp(labelTransform.position, targetPosition, ref m_FollowVelocity,
                        m_FollowSmoothTime, m_MaxFollowSpeed, Time.deltaTime)
                    : Vector3.SmoothDamp(labelTransform.position, targetPosition, ref m_FollowVelocity,
                        m_FollowSmoothTime, Mathf.Infinity, Time.deltaTime);
            }
            else
            {
                labelTransform.position = targetPosition;
            }

            if (m_Text == null)
                return;

            if (m_RefreshInterval > 0f && Time.time < m_NextRefreshTime)
                return;

            m_NextRefreshTime = Time.time + m_RefreshInterval;
            m_Text.text = BuildReadout(origin);
        }

        /// <summary>
        /// Composes the readout. Every figure is an acceleration in m/s^2, because the
        /// attractor applies force in acceleration mode and gravity is an acceleration too,
        /// so the terms are directly comparable without involving mass.
        /// </summary>
        string BuildReadout(Vector3 origin)
        {
            m_Builder.Clear();
            m_Builder.Append(name);

            if (m_ShowMass)
                m_Builder.Append($"   {m_Rigidbody.mass:0.##} kg");

            var net = Vector3.zero;

            // Unity only applies gravity when the body asks for it.
            if (m_Rigidbody.useGravity)
                net += Physics.gravity;

            foreach (var attractor in m_Attractors)
            {
                if (attractor != null)
                    net += attractor.GetAccelerationAt(origin);
            }

            // Each force is spelled out on its own arrow head, so only the resultant is
            // worth repeating here.
            if (m_ShowNet)
                m_Builder.Append($"\nnet  {net.magnitude:0.0} m/s²");

            return m_Builder.ToString();
        }

        void HideLabel()
        {
            if (m_LabelInstance != null)
                m_LabelInstance.SetActive(false);
        }
    }
}
