using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Interpolates a detached visual between the last two physics poses, by hand. FixedUpdate
    /// records where the body was and where it now is; LateUpdate, which runs every rendered
    /// frame, blends between them using how far the clock has advanced into the current
    /// physics step.
    /// </summary>
    /// <remarks>
    /// This is what Rigidbody.Interpolate already does internally, so use that unless you
    /// need something it will not give you: a per-object smoothing curve, a deliberate visual
    /// offset, or a visual that lags its collider on purpose. Like any interpolation it renders
    /// one physics step behind, because it can only blend between poses it has already seen.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public class ManualInterpolatedVisual : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Visual root to detach and drive. Leave empty to use the first child holding a renderer.")]
        Transform m_Visual;

        [SerializeField]
        [Tooltip("Extra easing on top of the interpolation, in seconds. Zero gives the exact reconstructed path, which is normally what you want.")]
        [Range(0f, 0.1f)]
        float m_ExtraSmoothTime;

        [SerializeField]
        [Tooltip("Snap instead of blending when the body jumps further than this in one step, so a teleport does not sweep the visual across the room")]
        float m_SnapDistance = 0.75f;

        [SerializeField]
        [Tooltip("Turn Rigidbody.Interpolate off on start, since running both at once would interpolate twice")]
        bool m_DisableRigidbodyInterpolation = true;

        Rigidbody m_Rigidbody;

        // The two most recent physics poses, which is all interpolation needs.
        Vector3 m_PreviousPosition;
        Quaternion m_PreviousRotation;
        Vector3 m_CurrentPosition;
        Quaternion m_CurrentRotation;

        Vector3 m_SmoothVelocity;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();

            if (m_Visual == null)
            {
                var renderer = GetComponentInChildren<Renderer>();

                if (renderer != null && renderer.transform != transform)
                    m_Visual = renderer.transform;
            }

            if (m_Visual == null)
            {
                enabled = false;
                return;
            }

            // Detached so the physics transform can step underneath without dragging it.
            m_Visual.SetParent(null, true);

            if (m_DisableRigidbodyInterpolation)
                m_Rigidbody.interpolation = RigidbodyInterpolation.None;

            m_PreviousPosition = m_CurrentPosition = m_Rigidbody.position;
            m_PreviousRotation = m_CurrentRotation = m_Rigidbody.rotation;
        }

        void OnDestroy()
        {
            // The visual is no longer our child, so it would outlive us.
            if (m_Visual != null)
                Destroy(m_Visual.gameObject);
        }

        void FixedUpdate()
        {
            // Runs at the physics rate. Roll the newest pose into the history.
            m_PreviousPosition = m_CurrentPosition;
            m_PreviousRotation = m_CurrentRotation;
            m_CurrentPosition = m_Rigidbody.position;
            m_CurrentRotation = m_Rigidbody.rotation;
        }

        void LateUpdate()
        {
            if (m_Visual == null)
                return;

            if (Vector3.Distance(m_PreviousPosition, m_CurrentPosition) > m_SnapDistance)
            {
                m_Visual.SetPositionAndRotation(m_CurrentPosition, m_CurrentRotation);
                m_SmoothVelocity = Vector3.zero;
                return;
            }

            // How far the clock has advanced into the step that has not been simulated yet.
            // Unity exposes exactly this as Time.fixedTime versus Time.time.
            var alpha = Mathf.Clamp01((Time.time - Time.fixedTime) / Time.fixedDeltaTime);

            var position = Vector3.Lerp(m_PreviousPosition, m_CurrentPosition, alpha);
            var rotation = Quaternion.Slerp(m_PreviousRotation, m_CurrentRotation, alpha);

            if (m_ExtraSmoothTime > 0f)
                position = Vector3.SmoothDamp(m_Visual.position, position, ref m_SmoothVelocity,
                    m_ExtraSmoothTime, Mathf.Infinity, Time.deltaTime);

            m_Visual.SetPositionAndRotation(position, rotation);
        }
    }
}
