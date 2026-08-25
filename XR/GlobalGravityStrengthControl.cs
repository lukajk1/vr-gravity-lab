using UnityEngine;
using UnityEngine.Events;

namespace GravityLab
{
    /// <summary>
    /// Maps a normalised 0-1 input onto the magnitude of Unity's global gravity, keeping its
    /// direction. Drive it from a <see cref="TrackConstrainedGrab"/> so a slider on a rail
    /// sets how hard everything falls.
    ///
    /// The slider only chooses the value; a <see cref="GlobalGravityToggle"/> decides whether
    /// it is applied at all. Moving the slider while the toggle is off updates the label and
    /// remembers the setting, but leaves gravity at zero until the toggle is switched back on.
    /// </summary>
    /// <remarks>
    /// Physics.gravity is a project-wide setting rather than a per-scene one, so the original
    /// value is restored on destroy. Without that, leaving play mode mid-drag would leave
    /// every other scene with whatever gravity the slider happened to be showing.
    /// </remarks>
    public class GlobalGravityStrengthControl : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Slider driving this control. Leave empty to use one on this object or a child.")]
        TrackConstrainedGrab m_Slider;

        [SerializeField]
        [Tooltip("Label to keep in step with the current strength")]
        ValueLabel m_Label;

        [SerializeField]
        [Tooltip("Gravity magnitude at the slider's minimum end, in m/s²")]
        float m_MinGravity;

        [SerializeField]
        [Tooltip("Gravity magnitude at the slider's maximum end, in m/s²")]
        float m_MaxGravity = 20f;

        [SerializeField]
        [Tooltip("Direction gravity pulls. Normalised on use, so only the direction matters.")]
        Vector3 m_Direction = Vector3.down;

        [SerializeField]
        [Tooltip("Toggle that gates this control. While it is off, gravity stays at zero no matter where the slider sits. Leave empty to apply unconditionally.")]
        GlobalGravityToggle m_Gate;

        [SerializeField]
        [Tooltip("Wake sleeping bodies when the strength changes, so a settled pile reacts at once")]
        bool m_WakeBodiesOnChange = true;

        [SerializeField]
        [Tooltip("Raised whenever the strength changes, with the new magnitude in m/s²")]
        UnityEvent<float> m_OnStrengthChanged;

        Vector3 m_OriginalGravity;

        // The slider's chosen magnitude, kept even while the gate is off so switching back
        // on restores what the slider says rather than a stale value.
        float m_Magnitude;

        /// <summary>Raised whenever the strength changes, with the new magnitude in m/s².</summary>
        public UnityEvent<float> onStrengthChanged => m_OnStrengthChanged;

        /// <summary>The magnitude the slider is set to, whether or not it is currently applied.</summary>
        public float strength => m_Magnitude;

        /// <summary>Whether the gate currently allows the value through.</summary>
        public bool isApplied => m_Gate == null || m_Gate.isOn;

        void Awake()
        {
            // Captured before anything is changed, so it can be put back on teardown.
            m_OriginalGravity = Physics.gravity;

            if (m_Slider == null)
                m_Slider = GetComponentInChildren<TrackConstrainedGrab>();

            if (m_Label == null)
                m_Label = GetComponentInChildren<ValueLabel>();
        }

        void OnEnable()
        {
            if (m_Slider != null)
                m_Slider.onValueChanged.AddListener(OnSliderChanged);

            // The toggle is the final say, so re-apply whenever it flips.
            if (m_Gate != null)
                m_Gate.onToggled.AddListener(OnGateToggled);
        }

        void OnDisable()
        {
            if (m_Slider != null)
                m_Slider.onValueChanged.RemoveListener(OnSliderChanged);

            if (m_Gate != null)
                m_Gate.onToggled.RemoveListener(OnGateToggled);
        }

        void OnGateToggled(bool isOn)
        {
            Apply();
        }

        void Start()
        {
            // Applied in Start so listeners wired in the inspector exist to hear it.
            if (m_Slider != null)
                OnSliderChanged(m_Slider.value);
        }

        void OnDestroy()
        {
            // Physics.gravity outlives the scene, so hand it back as we found it.
            Physics.gravity = m_OriginalGravity;
        }

        void OnSliderChanged(float normalised)
        {
            SetStrength(Mathf.Lerp(m_MinGravity, m_MaxGravity, normalised));
        }

        /// <summary>
        /// Sets the gravity magnitude directly, keeping the configured direction.
        /// </summary>
        public void SetStrength(float magnitude)
        {
            m_Magnitude = magnitude;

            // The label reports what the slider is set to, even while the gate holds it back,
            // so you can dial in a value before switching gravity on.
            if (m_Label != null)
                m_Label.SetValue(magnitude);

            Apply();
            m_OnStrengthChanged?.Invoke(magnitude);
        }

        /// <summary>
        /// Pushes the stored magnitude to the physics engine, or zero when the gate is off.
        /// This is the only place gravity is written, so the toggle and the slider cannot
        /// fight over it.
        /// </summary>
        void Apply()
        {
            Physics.gravity = isApplied ? m_Direction.normalized * m_Magnitude : Vector3.zero;

            if (m_WakeBodiesOnChange)
                WakeBodies();
        }

        /// <summary>
        /// Nudges sleeping rigidbodies awake. Changing Physics.gravity wakes settled bodies on
        /// the next step anyway, so this only removes that one step of latency.
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
