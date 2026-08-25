using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Moves an object up and down between its starting position and a point directly above
    /// it. The position the object is authored at is the low point, so a bobbing attractor
    /// never sinks below where it was placed in the scene.
    /// </summary>
    public class VerticalBob : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("How far above the starting position the object rises, in metres")]
        float m_Height = 1f;

        [SerializeField]
        [Tooltip("Seconds for one full round trip, up and back down")]
        float m_Period = 4f;

        [SerializeField]
        [Tooltip("Fraction of a cycle to start at, so several bobbers can be offset from each other")]
        [Range(0f, 1f)]
        float m_PhaseOffset;

        [SerializeField]
        [Tooltip("Ease into each end of the travel instead of moving at a constant speed")]
        bool m_Smooth = true;

        [SerializeField]
        [Tooltip("Direction to travel. Normalised on use, so only the direction matters.")]
        Vector3 m_Axis = Vector3.up;

        [SerializeField]
        [Tooltip("Run in edit mode, so the travel can be previewed without entering play mode")]
        bool m_PreviewInEditMode;

        Vector3 m_LowPoint;
        bool m_HasLowPoint;

        /// <summary>The authored position the bob starts from and returns to.</summary>
        public Vector3 lowPoint => m_LowPoint;

        /// <summary>How far above the low point the object travels, in metres.</summary>
        public float height
        {
            get => m_Height;
            set => m_Height = value;
        }

        void Awake()
        {
            CaptureLowPoint();
        }

        void OnEnable()
        {
            // Re-capture after a domain reload, which clears the cached value.
            if (!m_HasLowPoint)
                CaptureLowPoint();
        }

        void OnDisable()
        {
            // Leave it where it was authored rather than stranded mid-travel.
            if (m_HasLowPoint)
                transform.position = m_LowPoint;
        }

        void Update()
        {
            if (!Application.isPlaying && !m_PreviewInEditMode)
                return;

            if (!m_HasLowPoint || m_Period <= 0f)
                return;

            // 0 at the low point, 1 at the top, back to 0. Starting at phase 0 means the
            // object begins exactly where it was placed.
            var cycle = (Time.time / m_Period + m_PhaseOffset) % 1f;
            var t = Mathf.PingPong(cycle * 2f, 1f);

            if (m_Smooth)
                t = Mathf.SmoothStep(0f, 1f, t);

            transform.position = m_LowPoint + m_Axis.normalized * (t * m_Height);
        }

        void CaptureLowPoint()
        {
            m_LowPoint = transform.position;
            m_HasLowPoint = true;
        }

        void OnDrawGizmosSelected()
        {
            // Before Awake runs the low point is simply wherever the object sits.
            var low = m_HasLowPoint ? m_LowPoint : transform.position;
            var high = low + m_Axis.normalized * m_Height;

            Gizmos.color = new Color(0.4f, 0.9f, 1f);
            Gizmos.DrawLine(low, high);
            Gizmos.DrawWireCube(low, Vector3.one * 0.08f);
            Gizmos.DrawWireSphere(high, 0.06f);
        }
    }
}
