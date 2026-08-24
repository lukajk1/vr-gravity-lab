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
    /// accelerations acting on it. A held body is kinematic so nothing is really applied;
    /// these are the values that resume the moment it is released, matching the vectors
    /// drawn by <see cref="GrabbedForceVisualiser"/>.
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
                m_LabelInstance.SetActive(true);

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
            m_LabelInstance.transform.position = origin + m_Offset;

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
            {
                var gravity = Physics.gravity;
                net += gravity;
                m_Builder.Append($"\ngravity      {gravity.magnitude:0.0} m/s²");
            }
            else
            {
                m_Builder.Append("\ngravity      off");
            }

            for (var i = 0; i < m_Attractors.Count; i++)
            {
                var attractor = m_Attractors[i];

                if (attractor == null)
                    continue;

                var acceleration = attractor.GetAccelerationAt(origin);
                net += acceleration;

                var distance = Vector3.Distance(origin, attractor.transform.position);
                var label = m_Attractors.Count > 1 ? $"attractor {i + 1}" : "attractor";
                m_Builder.Append($"\n{label}   {acceleration.magnitude:0.0} m/s²  @ {distance:0.0} m");
            }

            if (m_ShowNet)
                m_Builder.Append($"\nnet          {net.magnitude:0.0} m/s²");

            return m_Builder.ToString();
        }

        void HideLabel()
        {
            if (m_LabelInstance != null)
                m_LabelInstance.SetActive(false);
        }
    }
}
