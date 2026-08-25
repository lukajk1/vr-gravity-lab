using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// A physical collider that follows a tracked controller so you can bat objects around.
    /// It deliberately is not parented to the controller: a parented collider teleports with
    /// the tracking pose each frame, which PhysX reads as a discontinuous jump and turns into
    /// missed hits or absurd launch speeds. Instead this is a kinematic rigidbody driven with
    /// MovePosition, so the solver derives a sensible velocity from the movement.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class ControllerPhysicsHand : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Tracked transform to follow, normally the controller or a hand joint")]
        Transform m_Target;

        [SerializeField]
        [Tooltip("Offset from the target, in the target's own space")]
        Vector3 m_LocalOffset;

        [SerializeField]
        [Tooltip("How quickly the collider closes the gap to the target. Lower is softer and lets objects push back; 1 follows as closely as the solver allows.")]
        [Range(0.05f, 1f)]
        float m_FollowStrength = 0.6f;

        [SerializeField]
        [Tooltip("Also match the target's rotation. Turn off for a sphere, where rotation does not matter.")]
        bool m_FollowRotation = true;

        [SerializeField]
        [Tooltip("Metres past which the collider gives up smoothing and teleports. Stops it dragging a trail across the room after a tracking jump or a teleport move.")]
        float m_TeleportDistance = 1f;

        [SerializeField]
        [Tooltip("Hide the collider while it is not tracking, so a stale one does not sit in the scene")]
        bool m_HideWhenUntracked = true;

        Rigidbody m_Rigidbody;
        Renderer[] m_Renderers;
        bool m_Visible = true;

        void Awake()
        {
            m_Rigidbody = GetComponent<Rigidbody>();

            // Kinematic so tracking drives it and nothing can knock it out of your hand.
            m_Rigidbody.isKinematic = true;
            m_Rigidbody.useGravity = false;

            // Without interpolation the collider stutters between physics steps, which reads
            // badly when it is attached to your hand.
            m_Rigidbody.interpolation = RigidbodyInterpolation.Interpolate;
            m_Rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            m_Renderers = GetComponentsInChildren<Renderer>(true);
        }

        void FixedUpdate()
        {
            if (m_Target == null)
            {
                SetVisible(!m_HideWhenUntracked);
                return;
            }

            SetVisible(true);

            var targetPosition = m_Target.TransformPoint(m_LocalOffset);
            var offset = targetPosition - m_Rigidbody.position;

            // A big jump means tracking recovered or the rig teleported; sweeping there
            // would shove every object in between out of the way.
            if (offset.magnitude > m_TeleportDistance)
            {
                m_Rigidbody.position = targetPosition;
                m_Rigidbody.rotation = m_Target.rotation;
                return;
            }

            // MovePosition gives the solver an implied velocity, so struck objects pick up
            // momentum from how fast you actually swung.
            m_Rigidbody.MovePosition(m_Rigidbody.position + offset * m_FollowStrength);

            if (m_FollowRotation)
                m_Rigidbody.MoveRotation(Quaternion.Slerp(m_Rigidbody.rotation, m_Target.rotation, m_FollowStrength));
        }

        void SetVisible(bool value)
        {
            if (m_Visible == value || m_Renderers == null)
                return;

            m_Visible = value;

            foreach (var renderer in m_Renderers)
            {
                if (renderer != null)
                    renderer.enabled = value;
            }
        }
    }
}
