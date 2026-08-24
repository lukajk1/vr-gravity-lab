using TMPro;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Worldspace readout for an attractor's strength. The attractor's tuning value is a
    /// gravitational parameter in m^3/s^2, not an acceleration, so this also reports the
    /// acceleration it actually produces at a reference distance where that is more useful
    /// to read.
    /// </summary>
    public class AttractorStrengthLabel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Attractor to report on. Leave empty to use one on this object or a parent.")]
        GravitationalAttractor m_Attractor;

        [SerializeField]
        [Tooltip("Text component to write into")]
        TMP_Text m_Text;

        [SerializeField]
        [Tooltip("Also show the acceleration produced at a fixed distance, which is easier to compare against 9.81")]
        bool m_ShowAccelerationAtDistance = true;

        [SerializeField]
        [Tooltip("Distance in metres used for that sample reading")]
        float m_SampleDistance = 2f;

        [SerializeField]
        [Tooltip("Seconds between refreshes. Zero updates every frame.")]
        float m_RefreshInterval = 0.1f;

        float m_NextRefreshTime;

        void Awake()
        {
            if (m_Attractor == null)
                m_Attractor = GetComponentInParent<GravitationalAttractor>();

            if (m_Text == null)
                m_Text = GetComponentInChildren<TMP_Text>();
        }

        void Update()
        {
            if (m_Attractor == null || m_Text == null)
                return;

            if (m_RefreshInterval > 0f && Time.time < m_NextRefreshTime)
                return;

            m_NextRefreshTime = Time.time + m_RefreshInterval;

            // The tuning value has units of m^3/s^2, so label it as such rather than as g.
            var readout = $"μ  {m_Attractor.gravity:0.#} m³/s²";

            if (m_ShowAccelerationAtDistance)
            {
                var samplePoint = m_Attractor.transform.position + Vector3.right * m_SampleDistance;
                var acceleration = m_Attractor.GetAccelerationAt(samplePoint).magnitude;
                readout += $"\n{acceleration:0.#} m/s² at {m_SampleDistance:0.#} m";
            }

            m_Text.text = readout;
        }
    }
}
