using System.Collections.Generic;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Pulls nearby rigidbodies toward this point with an inverse-square falloff, on top
    /// of Unity's normal downward gravity.
    /// </summary>
    public class GravitationalAttractor : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Acceleration at the attractor's surface, in m/s^2. Earth's surface is 9.81, so 19.62 is 2G. Negative values push bodies away instead of pulling them in.")]
        float m_SurfaceGravity = 19.62f;

        [SerializeField]
        [Tooltip("Radius of the attractor's surface, in metres. A Unity sphere primitive is 0.5 at scale 1, so scale 0.6 gives 0.3.")]
        float m_SurfaceRadius = 0.3f;

        [SerializeField]
        [Tooltip("Bodies beyond this distance are ignored")]
        float m_Range = 30f;

        [SerializeField]
        [Tooltip("Cap on the acceleration applied per body, as a further stability guard")]
        float m_MaxAcceleration = 100f;

        [SerializeField]
        [Tooltip("Re-scan for rigidbodies at this interval, in seconds. Zero scans every physics step.")]
        float m_RescanInterval = 1f;

        readonly List<Rigidbody> m_Bodies = new List<Rigidbody>();
        float m_NextScanTime;

        /// <summary>
        /// Acceleration at the surface, in m/s^2. This is the tuning knob: 9.81 matches
        /// Earth's surface, so 19.62 is 2G.
        /// </summary>
        public float surfaceGravity
        {
            get => m_SurfaceGravity;
            set => m_SurfaceGravity = value;
        }

        /// <summary>Radius of the surface that <see cref="surfaceGravity"/> refers to, in metres.</summary>
        public float surfaceRadius
        {
            get => m_SurfaceRadius;
            set => m_SurfaceRadius = value;
        }

        /// <summary>
        /// The gravitational parameter (G times mass) in m^3/s^2, derived from the surface
        /// figures via a = mu / r^2. This is what the inverse-square falloff actually uses.
        /// </summary>
        public float gravitationalParameter => m_SurfaceGravity * m_SurfaceRadius * m_SurfaceRadius;

        public float range => m_Range;

        /// <summary>
        /// Distance floor used in the r^2 term. The field is clamped at the surface, so the
        /// pull peaks there rather than blowing up towards the centre.
        /// </summary>
        public float minDistance => m_SurfaceRadius;

        public float maxAcceleration => m_MaxAcceleration;

        /// <summary>
        /// Acceleration this attractor would apply to a body at the given world position,
        /// using the same maths as <see cref="FixedUpdate"/>. Returns zero when the point is
        /// out of range. Lets callers such as force visualisers report the real value
        /// without duplicating the falloff.
        /// </summary>
        public Vector3 GetAccelerationAt(Vector3 worldPosition)
        {
            var offset = transform.position - worldPosition;
            var distance = offset.magnitude;

            if (distance > m_Range || distance < 1e-4f)
                return Vector3.zero;

            // Clamp at the surface, so the pull peaks there instead of blowing up at the centre.
            var effectiveDistance = Mathf.Max(distance, m_SurfaceRadius);
            var acceleration = gravitationalParameter / (effectiveDistance * effectiveDistance);

            // Clamp the magnitude, not the signed value: a negative surface gravity makes
            // this a repulsor, and Mathf.Min on a negative number would let it through
            // unlimited instead of capping it.
            acceleration = Mathf.Sign(acceleration) * Mathf.Min(Mathf.Abs(acceleration), m_MaxAcceleration);

            return offset / distance * acceleration;
        }

        void OnEnable()
        {
            Rescan();
        }

        void FixedUpdate()
        {
            if (m_RescanInterval <= 0f || Time.time >= m_NextScanTime)
                Rescan();

            for (var i = m_Bodies.Count - 1; i >= 0; i--)
            {
                var body = m_Bodies[i];

                // Bodies can be destroyed between scans.
                if (body == null)
                {
                    m_Bodies.RemoveAt(i);
                    continue;
                }

                if (body.isKinematic)
                    continue;

                // Shared with GetAccelerationAt so visualisers report exactly what is applied.
                var acceleration = GetAccelerationAt(body.worldCenterOfMass);

                if (acceleration == Vector3.zero)
                    continue;

                // Acceleration mode ignores mass, so heavy and light objects fall alike.
                body.AddForce(acceleration, ForceMode.Acceleration);
            }
        }

        void Rescan()
        {
            m_NextScanTime = Time.time + m_RescanInterval;

            m_Bodies.Clear();
            foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (body != null && !body.isKinematic)
                    m_Bodies.Add(body);
            }
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.4f, 0.7f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, m_Range);
            Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
            Gizmos.DrawWireSphere(transform.position, m_SurfaceRadius);
        }
    }
}
