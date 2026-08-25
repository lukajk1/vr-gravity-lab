using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Switches the Fresnel rim on a renderer's material on and off. Wire an
    /// <see cref="AttractorToggle"/> to this so the attractor visibly glows only while it
    /// is actually pulling.
    /// </summary>
    public class FresnelToggleDriver : MonoBehaviour
    {
        const string k_FresnelKeyword = "_FRESNEL_ON";
        const string k_FresnelEnabledProperty = "_FresnelEnabled";

        [SerializeField]
        [Tooltip("Renderers whose material should have its rim switched. Leave empty to use the renderers on this object and its children.")]
        Renderer[] m_Renderers;

        [SerializeField]
        [Tooltip("Attractor toggle to follow. Leave empty to find one on this object or a parent.")]
        AttractorToggle m_Toggle;

        [SerializeField]
        [Tooltip("Invert the relationship, so the rim shows while the attractor is off")]
        bool m_Invert;

        [SerializeField]
        [Tooltip("State to apply if no toggle is found to follow")]
        bool m_DefaultState = true;

        // Instanced so switching the rim on one attractor does not affect every object
        // sharing the same material asset.
        Material[] m_Materials;

        void Awake()
        {
            if (m_Renderers == null || m_Renderers.Length == 0)
                m_Renderers = GetComponentsInChildren<Renderer>(true);

            if (m_Toggle == null)
                m_Toggle = GetComponentInParent<AttractorToggle>();

            // Renderer.material returns an instance, so each attractor gets its own copy.
            m_Materials = new Material[m_Renderers.Length];

            for (var i = 0; i < m_Renderers.Length; i++)
            {
                if (m_Renderers[i] != null)
                    m_Materials[i] = m_Renderers[i].material;
            }
        }

        void OnEnable()
        {
            if (m_Toggle == null)
            {
                SetFresnel(m_DefaultState);
                return;
            }

            m_Toggle.onToggled.AddListener(OnToggled);

            // The toggle raises its first event from Start, which may already have run.
            OnToggled(m_Toggle.isOn);
        }

        void OnDisable()
        {
            if (m_Toggle != null)
                m_Toggle.onToggled.RemoveListener(OnToggled);
        }

        void OnDestroy()
        {
            // Renderer.material created these instances, so they are ours to clean up.
            if (m_Materials == null)
                return;

            foreach (var material in m_Materials)
            {
                if (material != null)
                    Destroy(material);
            }
        }

        void OnToggled(bool isOn)
        {
            SetFresnel(m_Invert ? !isOn : isOn);
        }

        /// <summary>
        /// Switches the rim on or off. Public so it can also be driven straight from a
        /// UnityEvent in the inspector.
        /// </summary>
        public void SetFresnel(bool value)
        {
            if (m_Materials == null)
                return;

            foreach (var material in m_Materials)
            {
                if (material == null)
                    continue;

                // The keyword compiles the rim out entirely; the float keeps the material
                // inspector's toggle in step with what the shader is doing.
                if (value)
                    material.EnableKeyword(k_FresnelKeyword);
                else
                    material.DisableKeyword(k_FresnelKeyword);

                if (material.HasProperty(k_FresnelEnabledProperty))
                    material.SetFloat(k_FresnelEnabledProperty, value ? 1f : 0f);
            }
        }
    }
}
