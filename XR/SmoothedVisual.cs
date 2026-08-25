using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Decouples an object's visual from its rigidbody, so the renderer can be eased at
    /// display rate instead of stepping at the physics rate. The rigidbody and its colliders
    /// are untouched, so physics, centre of mass and every system that reads the body behave
    /// exactly as before; only what you see is smoothed.
    /// </summary>
    /// <remarks>
    /// This smooths, it does not add information. The target pose still only changes at the
    /// physics rate, so easing trades stutter for lag rather than removing both. Prefer a
    /// small smoothing time, and note that Rigidbody.Interpolate already solves the common
    /// case with no lag beyond one physics step.
    /// </remarks>
    [RequireComponent(typeof(Rigidbody))]
    public class SmoothedVisual : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Visual root to detach and ease. Leave empty to use the first child holding a renderer.")]
        Transform m_Visual;

        [SerializeField]
        [Tooltip("Seconds for the visual to catch up in position. Small values only; this is lag you can feel.")]
        [Range(0f, 0.15f)]
        float m_PositionSmoothTime = 0.02f;

        [SerializeField]
        [Tooltip("Seconds for the visual to catch up in rotation")]
        [Range(0f, 0.15f)]
        float m_RotationSmoothTime = 0.02f;

        [SerializeField]
        [Tooltip("Snap instead of easing when the body moves further than this in one frame, so a teleport or respawn does not drag the visual across the room")]
        float m_SnapDistance = 0.75f;

        [SerializeField]
        [Tooltip("Turn off Rigidbody.Interpolate on start. Leave interpolation on and this off to compare the two approaches.")]
        bool m_DisableRigidbodyInterpolation = true;

        Rigidbody m_Rigidbody;
        Vector3 m_PositionVelocity;

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

            // Detached from the body so the physics transform can step underneath without
            // dragging the visual with it.
            m_Visual.SetParent(null, true);

            if (m_DisableRigidbodyInterpolation)
                m_Rigidbody.interpolation = RigidbodyInterpolation.None;
        }

        void OnDestroy()
        {
            // The visual is no longer parented to us, so it would outlive the object.
            if (m_Visual != null)
                Destroy(m_Visual.gameObject);
        }

        void LateUpdate()
        {
            if (m_Visual == null)
                return;

            var targetPosition = m_Rigidbody.position;
            var targetRotation = m_Rigidbody.rotation;

            if (Vector3.Distance(m_Visual.position, targetPosition) > m_SnapDistance)
            {
                m_Visual.SetPositionAndRotation(targetPosition, targetRotation);
                m_PositionVelocity = Vector3.zero;
                return;
            }

            m_Visual.position = m_PositionSmoothTime > 0f
                ? Vector3.SmoothDamp(m_Visual.position, targetPosition, ref m_PositionVelocity,
                    m_PositionSmoothTime, Mathf.Infinity, Time.deltaTime)
                : targetPosition;

            // Exponential blend, so the rate is the same regardless of frame time.
            m_Visual.rotation = m_RotationSmoothTime > 0f
                ? Quaternion.Slerp(m_Visual.rotation, targetRotation,
                    1f - Mathf.Exp(-Time.deltaTime / m_RotationSmoothTime))
                : targetRotation;
        }
    }
}
