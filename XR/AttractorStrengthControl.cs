using UnityEngine;
using UnityEngine.Events;

namespace GravityLab
{
    /// <summary>
    /// Maps a normalised 0-1 input onto an attractor's surface gravity. Drive it from a
    /// <see cref="TrackConstrainedGrab"/> so a slider on a rail sets how hard the attractor
    /// pulls.
    /// </summary>
    /// <remarks>
    /// Surface gravity is the acceleration at the attractor's own surface, so the value is
    /// directly comparable to Earth's 9.81. The pull anywhere else falls off with the square
    /// of distance, which is why a figure that looks large here can still be gentle out where
    /// the objects are.
    /// </remarks>
    public class AttractorStrengthControl : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Attractor to drive. Leave empty to find one anywhere in the scene.")]
        GravitationalAttractor m_Attractor;

        [SerializeField]
        [Tooltip("Slider driving this control. Leave empty to use one on this object or a child.")]
        TrackConstrainedGrab m_Slider;

        [SerializeField]
        [Tooltip("Label to keep in step with the current strength")]
        ValueLabel m_Label;

        [SerializeField]
        [Tooltip("Surface gravity at the slider's minimum end, in m/s²")]
        float m_MinSurfaceGravity;

        [SerializeField]
        [Tooltip("Surface gravity at the slider's maximum end, in m/s². The pull falls off with distance squared, so this needs to be well above 9.81 to matter out where the objects are.")]
        float m_MaxSurfaceGravity = 400f;

        [SerializeField]
        [Tooltip("Toggle that gates this control. The toggle enables or disables the attractor itself, so the slider only chooses the strength it will pull with.")]
        AttractorToggle m_Gate;

        [SerializeField]
        [Tooltip("Wake sleeping bodies when the strength changes, so a settled pile reacts at once")]
        bool m_WakeBodiesOnChange = true;

        [SerializeField]
        [Tooltip("Raised whenever the strength changes, with the new surface gravity in m/s²")]
        UnityEvent<float> m_OnStrengthChanged;

        /// <summary>Raised whenever the strength changes, with the new surface gravity in m/s².</summary>
        public UnityEvent<float> onStrengthChanged => m_OnStrengthChanged;

        /// <summary>Current surface gravity in m/s².</summary>
        public float strength => m_Attractor != null ? m_Attractor.surfaceGravity : 0f;

        /// <summary>Whether the gate currently allows the attractor to pull at all.</summary>
        public bool isApplied => m_Gate == null || m_Gate.isOn;

        void Awake()
        {
            if (m_Attractor == null)
                m_Attractor = FindFirstObjectByType<GravitationalAttractor>();

            if (m_Slider == null)
                m_Slider = GetComponentInChildren<TrackConstrainedGrab>();

            if (m_Label == null)
                m_Label = GetComponentInChildren<ValueLabel>();
        }

        void OnEnable()
        {
            if (m_Slider != null)
                m_Slider.onValueChanged.AddListener(OnSliderChanged);
        }

        void OnDisable()
        {
            if (m_Slider != null)
                m_Slider.onValueChanged.RemoveListener(OnSliderChanged);
        }

        void Start()
        {
            // Applied in Start so listeners wired in the inspector exist to hear it.
            if (m_Slider != null)
                OnSliderChanged(m_Slider.value);
        }

        void OnSliderChanged(float normalised)
        {
            SetStrength(Mathf.Lerp(m_MinSurfaceGravity, m_MaxSurfaceGravity, normalised));
        }

        /// <summary>
        /// Sets the attractor's surface gravity directly.
        /// </summary>
        public void SetStrength(float surfaceGravity)
        {
            if (m_Attractor != null)
                m_Attractor.surfaceGravity = surfaceGravity;

            if (m_Label != null)
                m_Label.SetValue(surfaceGravity);

            if (m_WakeBodiesOnChange)
                WakeBodies();

            m_OnStrengthChanged?.Invoke(surfaceGravity);
        }

        /// <summary>
        /// Nudges sleeping rigidbodies awake. The attractor's AddForce wakes them on the next
        /// step anyway, so this only removes that one step of latency.
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
