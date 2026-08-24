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
        [Tooltip("Strength of the pull. Scales the whole force, like G times the attractor's mass.")]
        float m_Gravity = 60f;

        [SerializeField]
        [Tooltip("Bodies beyond this distance are ignored")]
        float m_Range = 30f;

        [SerializeField]
        [Tooltip("Distance floor used in the r^2 term, so the force cannot blow up at the centre")]
        float m_MinDistance = 1.5f;

        [SerializeField]
        [Tooltip("Cap on the acceleration applied per body, as a further stability guard")]
        float m_MaxAcceleration = 100f;

        [SerializeField]
        [Tooltip("Re-scan for rigidbodies at this interval, in seconds. Zero scans every physics step.")]
        float m_RescanInterval = 1f;

        readonly List<Rigidbody> m_Bodies = new List<Rigidbody>();
        float m_NextScanTime;

        public float gravity
        {
            get => m_Gravity;
            set => m_Gravity = value;
        }

        public float range => m_Range;

        public float minDistance => m_MinDistance;

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

            var effectiveDistance = Mathf.Max(distance, m_MinDistance);
            var acceleration = m_Gravity / (effectiveDistance * effectiveDistance);
            acceleration = Mathf.Min(acceleration, m_MaxAcceleration);

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
            Gizmos.DrawWireSphere(transform.position, m_MinDistance);
        }
    }
}
