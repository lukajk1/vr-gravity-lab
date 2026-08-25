using TMPro;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Shows a caption and a live number, formatted from a template string. Generic on
    /// purpose: anything that produces a float can drive it through <see cref="SetValue"/>,
    /// so a slider, a dial or a script all use the same label.
    /// </summary>
    public class ValueLabel : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Text component to write into. Leave empty to use one in this hierarchy.")]
        TMP_Text m_Text;

        [SerializeField]
        [Tooltip("Fixed caption shown above the value, for example the control's name")]
        string m_Caption = "VALUE";

        [SerializeField]
        [Tooltip("Format for the value line. {0} is the value; standard .NET numeric formats work, e.g. \"{0:0.0} m/s²\".")]
        string m_Format = "{0:0.0}";

        [SerializeField]
        [Tooltip("Colour of the value line")]
        Color m_ValueColor = new Color(0.45f, 0.85f, 1f);

        [SerializeField]
        [Tooltip("Value shown before anything drives the label")]
        float m_InitialValue;

        float m_Value;

        /// <summary>The number currently displayed.</summary>
        public float value
        {
            get => m_Value;
            set => SetValue(value);
        }

        /// <summary>The caption shown above the value.</summary>
        public string caption
        {
            get => m_Caption;
            set
            {
                m_Caption = value;
                Refresh();
            }
        }

        void Awake()
        {
            if (m_Text == null)
                m_Text = GetComponentInChildren<TMP_Text>();

            m_Value = m_InitialValue;
        }

        void OnEnable()
        {
            Refresh();
        }

        /// <summary>
        /// Sets the displayed number. Public and float-typed so a slider's value event can be
        /// wired straight to it in the inspector.
        /// </summary>
        public void SetValue(float newValue)
        {
            m_Value = newValue;
            Refresh();
        }

        /// <summary>
        /// Sets the displayed number from a normalised 0-1 input, mapping it onto a range.
        /// Lets a slider that reports 0-1 show the real units it stands for.
        /// </summary>
        public void SetValueFromNormalised(float normalised, float min, float max)
        {
            SetValue(Mathf.Lerp(min, max, normalised));
        }

        void Refresh()
        {
            if (m_Text == null)
                return;

            var hex = ColorUtility.ToHtmlStringRGB(m_ValueColor);
            var formatted = string.Format(m_Format, m_Value);

            m_Text.text = string.IsNullOrEmpty(m_Caption)
                ? $"<color=#{hex}>{formatted}</color>"
                : $"{m_Caption}\n<color=#{hex}>{formatted}</color>";
        }
    }
}
