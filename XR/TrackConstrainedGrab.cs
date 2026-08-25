using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GravityLab
{
    /// <summary>
    /// A grabbable that slides along a single axis between two end stops, like a fader on a
    /// track. Grabbing works exactly as it does on any interactable; the difference is that
    /// the pose is projected onto the track every frame instead of following the hand freely.
    /// </summary>
    /// <remarks>
    /// XRI ships no linear slider, only the rotary XRKnob in the VR template. This fills that
    /// gap by overriding ProcessInteractable, which is where XRI expects an interactable to
    /// impose its own movement rules.
    /// </remarks>
    public class TrackConstrainedGrab : XRGrabInteractable
    {
        [Header("Track")]
        [SerializeField]
        [Tooltip("Axis the handle slides along, in the track's local space")]
        Vector3 m_TrackAxis = Vector3.right;

        [SerializeField]
        [Tooltip("Transform defining the track's origin and orientation. Leave empty to use this object's parent, or this object if it has none.")]
        Transform m_TrackRoot;

        [SerializeField]
        [Tooltip("Travel from the track origin toward the negative end, in metres")]
        float m_MinOffset = -0.25f;

        [SerializeField]
        [Tooltip("Travel from the track origin toward the positive end, in metres")]
        float m_MaxOffset = 0.25f;

        [SerializeField]
        [Tooltip("Snap to this many evenly spaced notches along the track. Zero slides continuously.")]
        int m_Notches;

        [SerializeField]
        [Tooltip("Keep the handle's rotation fixed to the track instead of following the hand")]
        bool m_LockRotation = true;

        [Header("Value")]
        [SerializeField]
        [Tooltip("Normalised position along the track, 0 at the minimum end and 1 at the maximum")]
        [Range(0f, 1f)]
        float m_Value = 0.5f;

        [SerializeField]
        [Tooltip("Raised whenever the handle moves, with the new normalised value")]
        UnityEvent<float> m_OnValueChanged;

        /// <summary>Raised whenever the handle moves, with the new normalised value.</summary>
        public UnityEvent<float> onValueChanged => m_OnValueChanged;

        /// <summary>Normalised position along the track, 0 at the minimum end and 1 at the maximum.</summary>
        public float value
        {
            get => m_Value;
            set => SetValue(value);
        }

        Transform trackRoot => m_TrackRoot != null
            ? m_TrackRoot
            : (transform.parent != null ? transform.parent : transform);

        Vector3 worldAxis => trackRoot.TransformDirection(m_TrackAxis).normalized;

        protected override void Awake()
        {
            base.Awake();

            // The handle drives its own pose, so let XRI move it without physics fighting back.
            if (movementType != MovementType.Kinematic)
                movementType = MovementType.Kinematic;

            ApplyValueToTransform();
        }

        public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
        {
            base.ProcessInteractable(updatePhase);

            // Dynamic phase is where XRI has already applied the hand's pose, so this is the
            // point at which to overwrite it with the constrained one.
            if (updatePhase != XRInteractionUpdateOrder.UpdatePhase.Dynamic || !isSelected)
                return;

            var interactor = interactorsSelecting[0];
            SetValue(ProjectToValue(interactor.GetAttachTransform(this).position));
        }

        /// <summary>
        /// Converts a world point to a normalised position by projecting it onto the track
        /// axis, so the handle tracks the component of hand movement that runs along the rail
        /// and ignores the rest.
        /// </summary>
        float ProjectToValue(Vector3 worldPoint)
        {
            var offset = Vector3.Dot(worldPoint - trackRoot.position, worldAxis);
            return Mathf.InverseLerp(m_MinOffset, m_MaxOffset, offset);
        }

        void SetValue(float normalised)
        {
            normalised = Mathf.Clamp01(normalised);

            if (m_Notches > 1)
                normalised = Mathf.Round(normalised * (m_Notches - 1)) / (m_Notches - 1);

            if (Mathf.Approximately(normalised, m_Value))
            {
                // Still re-apply, so a nudge from physics or the hand cannot drag it off rail.
                ApplyValueToTransform();
                return;
            }

            m_Value = normalised;
            ApplyValueToTransform();
            m_OnValueChanged?.Invoke(m_Value);
        }

        void ApplyValueToTransform()
        {
            var offset = Mathf.Lerp(m_MinOffset, m_MaxOffset, m_Value);
            transform.position = trackRoot.position + worldAxis * offset;

            if (m_LockRotation)
                transform.rotation = trackRoot.rotation;
        }

        void OnDrawGizmosSelected()
        {
            var root = trackRoot;
            var axis = root.TransformDirection(m_TrackAxis).normalized;
            var a = root.position + axis * m_MinOffset;
            var b = root.position + axis * m_MaxOffset;

            Gizmos.color = new Color(0.4f, 0.8f, 1f);
            Gizmos.DrawLine(a, b);
            Gizmos.DrawWireSphere(a, 0.015f);
            Gizmos.DrawWireSphere(b, 0.015f);

            if (m_Notches <= 1)
                return;

            Gizmos.color = new Color(1f, 0.8f, 0.3f, 0.7f);

            for (var i = 0; i < m_Notches; i++)
                Gizmos.DrawWireSphere(Vector3.Lerp(a, b, i / (float)(m_Notches - 1)), 0.008f);
        }
    }
}
