using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace GravityLab
{
    /// <summary>
    /// Drives the Fresnel rim on a grabbable object from its interaction state: shown only
    /// while a hand is close enough to grab it, and cleared once it is actually held. XRI
    /// raises hover exactly when an interactor considers the object a valid target, which
    /// makes it the "this is grabbable" cue; once you have hold of it the cue has done its
    /// job, so the rim gets out of the way.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class GrabFresnelHighlight : MonoBehaviour
    {
        const string k_FresnelKeyword = "_FRESNEL_ON";
        const string k_FresnelEnabledProperty = "_FresnelEnabled";
        const string k_FresnelColorProperty = "_FresnelColor";
        const string k_FresnelPowerProperty = "_FresnelPower";
        const string k_FresnelStrengthProperty = "_FresnelStrength";

        [SerializeField]
        [Tooltip("Renderers to highlight. Leave empty to use the renderers on this object and its children.")]
        Renderer[] m_Renderers;

        [SerializeField]
        [Tooltip("Rim colour while a hand is hovering and could grab this")]
        [ColorUsage(true, true)]
        Color m_HoverColor = new Color(1f, 0.85f, 0.35f) * 1.6f;

        [SerializeField]
        [Tooltip("Fresnel power while hovering. Higher confines the rim to the silhouette.")]
        float m_HoverPower = 3f;

        [SerializeField]
        [Tooltip("Rim intensity while hovering")]
        float m_HoverStrength = 1f;

        XRGrabInteractable m_Grab;
        Material[] m_Materials;

        void Awake()
        {
            m_Grab = GetComponent<XRGrabInteractable>();

            if (m_Renderers == null || m_Renderers.Length == 0)
                m_Renderers = GetComponentsInChildren<Renderer>(true);

            // Renderer.material returns an instance, so each object gets its own copy and
            // highlighting one does not light up every object sharing the material.
            m_Materials = new Material[m_Renderers.Length];

            for (var i = 0; i < m_Renderers.Length; i++)
            {
                if (m_Renderers[i] != null)
                    m_Materials[i] = m_Renderers[i].material;
            }
        }

        void OnEnable()
        {
            m_Grab.hoverEntered.AddListener(OnHoverEntered);
            m_Grab.hoverExited.AddListener(OnHoverExited);
            m_Grab.selectEntered.AddListener(OnSelectEntered);
            m_Grab.selectExited.AddListener(OnSelectExited);

            Refresh();
        }

        void OnDisable()
        {
            m_Grab.hoverEntered.RemoveListener(OnHoverEntered);
            m_Grab.hoverExited.RemoveListener(OnHoverExited);
            m_Grab.selectEntered.RemoveListener(OnSelectEntered);
            m_Grab.selectExited.RemoveListener(OnSelectExited);

            SetFresnel(false, Color.black, 1f, 0f);
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

        void OnHoverEntered(HoverEnterEventArgs args) => Refresh();

        void OnHoverExited(HoverExitEventArgs args) => Refresh();

        void OnSelectEntered(SelectEnterEventArgs args) => Refresh();

        void OnSelectExited(SelectExitEventArgs args) => Refresh();

        /// <summary>
        /// Picks the rim to show for the current state. Being held clears it: the holding
        /// hand still counts as hovering, so this check has to come first.
        /// </summary>
        void Refresh()
        {
            if (!m_Grab.isSelected && m_Grab.isHovered)
            {
                SetFresnel(true, m_HoverColor, m_HoverPower, m_HoverStrength);
                return;
            }

            SetFresnel(false, Color.black, 1f, 0f);
        }

        void SetFresnel(bool enabled, Color color, float power, float strength)
        {
            if (m_Materials == null)
                return;

            foreach (var material in m_Materials)
            {
                if (material == null || !material.HasProperty(k_FresnelEnabledProperty))
                    continue;

                // The keyword compiles the rim out when off; the float keeps the material
                // inspector's toggle in step with what the shader is doing.
                if (enabled)
                    material.EnableKeyword(k_FresnelKeyword);
                else
                    material.DisableKeyword(k_FresnelKeyword);

                material.SetFloat(k_FresnelEnabledProperty, enabled ? 1f : 0f);

                if (!enabled)
                    continue;

                material.SetColor(k_FresnelColorProperty, color);
                material.SetFloat(k_FresnelPowerProperty, power);
                material.SetFloat(k_FresnelStrengthProperty, strength);
            }
        }
    }
}
