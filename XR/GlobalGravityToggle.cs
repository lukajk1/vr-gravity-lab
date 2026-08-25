using UnityEngine;
using UnityEngine.Events;

namespace GravityLab
{
    /// <summary>
    /// Switches Unity's global gravity on and off. With it off the attractor becomes the only
    /// force acting, so bodies orbit instead of being dragged to the floor.
    ///
    /// This is the final say on whether gravity applies. When a GlobalGravityStrengthControl
    /// is present it owns the magnitude and watches this toggle, so the two do not both write
    /// Physics.gravity; leave m_DriveGravityDirectly off in that setup.
    /// </summary>
    /// <remarks>
    /// Physics.gravity is a project-wide setting rather than a per-scene one, so this
    /// restores the original value when the object is destroyed. Without that, leaving play
    /// mode with gravity off would leave it off for every other scene in the editor.
    /// </remarks>
    public class GlobalGravityToggle : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Gravity applied when switched on. Ignored when a strength control owns the magnitude.")]
        Vector3 m_GravityWhenOn = new Vector3(0f, -9.81f, 0f);

        [SerializeField]
        [Tooltip("Write Physics.gravity from this toggle. Turn OFF when a GlobalGravityStrengthControl is gated on this toggle, so the slider's value is not overwritten.")]
        bool m_DriveGravityDirectly = true;

        [SerializeField]
        [Tooltip("State to apply when the scene loads")]
        bool m_StartEnabled = true;

        [SerializeField]
        [Tooltip("Wake sleeping bodies when gravity changes. PhysX already wakes them on the next step, so this only removes that one step of latency.")]
        bool m_WakeBodiesOnChange;

        [SerializeField]
        [Tooltip("Raised whenever the state changes, with the new state")]
        UnityEvent<bool> m_OnToggled;

        Vector3 m_OriginalGravity;
        bool m_IsOn;

        /// <summary>Raised whenever the state changes, with the new state.</summary>
        public UnityEvent<bool> onToggled => m_OnToggled;

        /// <summary>Whether global gravity is currently applied.</summary>
        public bool isOn => m_IsOn;

        void Awake()
        {
            // Captured before anything is changed, so it can be put back on teardown.
            m_OriginalGravity = Physics.gravity;
        }

        void Start()
        {
            // Applied in Start so listeners wired in the inspector exist to hear it.
            SetEnabled(m_StartEnabled);
        }

        void OnDestroy()
        {
            // Physics.gravity outlives the scene, so hand it back as we found it.
            Physics.gravity = m_OriginalGravity;
        }

        /// <summary>
        /// Flips global gravity between on and off. Wire a button's select event here.
        /// </summary>
        public void Toggle()
        {
            SetEnabled(!m_IsOn);
        }

        /// <summary>
        /// Switches global gravity to an explicit state.
        /// </summary>
        public void SetEnabled(bool value)
        {
            m_IsOn = value;

            // A strength control, when present, is the single writer of Physics.gravity and
            // reacts to onToggled below. Writing here as well would clobber its value.
            if (m_DriveGravityDirectly)
                Physics.gravity = value ? m_GravityWhenOn : Vector3.zero;

            if (m_WakeBodiesOnChange)
                WakeBodies();

            m_OnToggled?.Invoke(value);
        }

        /// <summary>
        /// Nudges sleeping rigidbodies awake. Not strictly required, since changing
        /// Physics.gravity wakes settled bodies on the next step anyway, but it removes
        /// that one step of latency.
        /// </summary>
        void WakeBodies()
        {
            foreach (var body in FindObjectsByType<Rigidbody>(FindObjectsSortMode.None))
            {
                if (body != null && !body.isKinematic)
                    body.WakeUp();
            }
        }
    }
}
