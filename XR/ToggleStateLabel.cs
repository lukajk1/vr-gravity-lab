using TMPro;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Shows an attractor toggle's current state as text. The XRI push button only animates
    /// while it is being touched, so without a readout like this there is nothing to tell you
    /// whether the attractor is currently on.
    /// </summary>
    public class ToggleStateLabel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Toggle to report on. Leave empty to find one on this object or a parent.")]
        AttractorToggle m_Toggle;

        [SerializeField]
        [Tooltip("Text component to write into. Leave empty to use one in this hierarchy.")]
        TMP_Text m_Text;

        [SerializeField]
        [Tooltip("Fixed caption shown above the state, for example the control's name")]
        string m_Caption = "ATTRACTOR";

        [SerializeField]
        [Tooltip("Text shown when the attractor is pulling")]
        string m_OnText = "ON";

        [SerializeField]
        [Tooltip("Text shown when the attractor is switched off")]
        string m_OffText = "OFF";

        [SerializeField]
        [Tooltip("Colour of the state word when on")]
        Color m_OnColor = new Color(0.3f, 0.9f, 0.4f);

        [SerializeField]
        [Tooltip("Colour of the state word when off")]
        Color m_OffColor = new Color(0.9f, 0.35f, 0.3f);

        void Awake()
        {
            if (m_Toggle == null)
                m_Toggle = GetComponentInParent<AttractorToggle>();

            if (m_Text == null)
                m_Text = GetComponentInChildren<TMP_Text>();
        }

        void OnEnable()
        {
            if (m_Toggle == null)
                return;

            m_Toggle.onToggled.AddListener(Refresh);

            // The toggle raises its first event from Start, which may already have run.
            Refresh(m_Toggle.isOn);
        }

        void OnDisable()
        {
            if (m_Toggle != null)
                m_Toggle.onToggled.RemoveListener(Refresh);
        }

        /// <summary>
        /// Rewrites the label for a given state. Public so it can also be driven straight
        /// from the toggle's event in the inspector.
        /// </summary>
        public void Refresh(bool isOn)
        {
            if (m_Text == null)
                return;

            var state = isOn ? m_OnText : m_OffText;
            var color = isOn ? m_OnColor : m_OffColor;

            // Colour only the state word, so the caption stays neutral.
            var hex = ColorUtility.ToHtmlStringRGB(color);
            m_Text.text = string.IsNullOrEmpty(m_Caption)
                ? $"<color=#{hex}>{state}</color>"
                : $"{m_Caption}\n<color=#{hex}>{state}</color>";
        }
    }
}
