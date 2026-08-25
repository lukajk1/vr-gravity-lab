using UnityEngine;
using UnityEngine.Events;

namespace GravityLab
{
    /// <summary>
    /// Turns an attractor's pull on and off. Only the attractor is affected; Unity's own
    /// downward gravity is left alone, so with the attractor off the objects simply fall
    /// to the floor as normal.
    /// </summary>
    public class AttractorToggle : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Attractor to switch. Leave empty to find one on this object or a parent, then anywhere in the scene.")]
        GravitationalAttractor m_Attractor;

        [SerializeField]
        [Tooltip("State the attractor starts in when the scene loads")]
        bool m_StartEnabled = true;

        [SerializeField]
        [Tooltip("Wake sleeping bodies when the attractor is switched on. AddForce already wakes them on the next physics step, so this only removes that one step of latency.")]
        bool m_WakeBodiesOnEnable;

        [SerializeField]
        [Tooltip("Raised whenever the state changes, with the new state. Handy for driving a label or an indicator light.")]
        UnityEvent<bool> m_OnToggled;

        /// <summary>Raised whenever the state changes, with the new state.</summary>
        public UnityEvent<bool> onToggled => m_OnToggled;

        /// <summary>Whether the attractor is currently pulling.</summary>
        public bool isOn => m_Attractor != null && m_Attractor.enabled;

        void Awake()
        {
            if (m_Attractor == null)
                m_Attractor = GetComponentInParent<GravitationalAttractor>();

            if (m_Attractor == null)
                m_Attractor = FindFirstObjectByType<GravitationalAttractor>();
        }

        void Start()
        {
            // Applied in Start so any listeners wired in the inspector exist to hear it.
            SetEnabled(m_StartEnabled);
        }

        /// <summary>
        /// Flips the attractor between on and off. Wire a button's select event here.
        /// </summary>
        public void Toggle()
        {
            SetEnabled(!isOn);
        }

        /// <summary>
        /// Switches the attractor to an explicit state.
        /// </summary>
        public void SetEnabled(bool value)
        {
            if (m_Attractor == null)
                return;

            m_Attractor.enabled = value;

            if (value && m_WakeBodiesOnEnable)
                WakeBodies();

            m_OnToggled?.Invoke(value);
        }

        /// <summary>
        /// Nudges sleeping rigidbodies awake. Not strictly required, since AddForce on a
        /// sleeping body wakes it anyway, but it removes the one physics step of latency
        /// before a settled pile starts to lift.
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
