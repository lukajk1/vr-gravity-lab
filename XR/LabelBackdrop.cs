using TMPro;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Puts a translucent panel behind a worldspace label and keeps it sized to whatever the
    /// text currently says, plus a margin. Worldspace text over a busy scene is hard to read;
    /// a dark backing gives it consistent contrast wherever the object drifts.
    /// </summary>
    [RequireComponent(typeof(TMP_Text))]
    [ExecuteAlways]
    public class LabelBackdrop : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Panel colour. Alpha controls how much of the scene shows through.")]
        Color m_Color = new Color(0f, 0f, 0f, 0.65f);

        [SerializeField]
        [Tooltip("Extra width beyond the text, split between left and right, in local units")]
        float m_HorizontalMargin = 0.35f;

        [SerializeField]
        [Tooltip("Extra height beyond the text, split between top and bottom, in local units")]
        float m_VerticalMargin = 0.2f;

        [SerializeField]
        [Tooltip("How far behind the text the panel sits, so the two never z-fight")]
        float m_Depth = 0.01f;

        [SerializeField]
        [Tooltip("Draw the panel on top of scene geometry, matching an overlay label")]
        bool m_DrawOnTop = true;

        [SerializeField]
        [Tooltip("Sorting order for the panel. Keep it below the text's own order so the words stay in front.")]
        int m_SortingOrder = 90;

        TMP_Text m_Text;
        Transform m_Panel;
        MeshRenderer m_PanelRenderer;
        Material m_PanelMaterial;
        Vector2 m_LastSize = new Vector2(-1f, -1f);

        void Awake()
        {
            m_Text = GetComponent<TMP_Text>();
        }

        void OnEnable()
        {
            if (m_Text == null)
                m_Text = GetComponent<TMP_Text>();

            EnsurePanel();
        }

        void OnDisable()
        {
            if (m_Panel != null)
                m_Panel.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (m_PanelMaterial != null)
            {
                if (Application.isPlaying)
                    Destroy(m_PanelMaterial);
                else
                    DestroyImmediate(m_PanelMaterial);
            }
        }

        void LateUpdate()
        {
            if (m_Text == null || m_Panel == null)
                return;

            m_Panel.gameObject.SetActive(true);
            Resize();
        }

        /// <summary>
        /// Fits the panel to the text's rendered bounds. TextMeshPro reports these in the
        /// text object's local space, so the panel can simply be a child using the same units.
        /// </summary>
        void Resize()
        {
            // ForceMeshUpdate would be needed for bounds on the same frame the text changes,
            // but the label refresh already runs before this, so the bounds are current.
            var bounds = m_Text.textBounds;

            var width = bounds.size.x + m_HorizontalMargin;
            var height = bounds.size.y + m_VerticalMargin;

            // Empty text reports a degenerate bound; hide rather than draw a sliver.
            if (string.IsNullOrEmpty(m_Text.text) || width <= 0f || height <= 0f)
            {
                m_PanelRenderer.enabled = false;
                return;
            }

            m_PanelRenderer.enabled = true;

            var size = new Vector2(width, height);

            if (size != m_LastSize)
            {
                m_Panel.localScale = new Vector3(width, height, 1f);
                m_LastSize = size;
            }

            // Centre on the text, then push back so the words render in front.
            m_Panel.localPosition = new Vector3(bounds.center.x, bounds.center.y, m_Depth);
            m_Panel.localRotation = Quaternion.identity;
        }

        void EnsurePanel()
        {
            if (m_Panel != null)
                return;

            // Reuse an existing panel when re-enabling, so repeated enable/disable does not
            // pile up quads.
            var existing = transform.Find("Backdrop");

            if (existing != null)
            {
                m_Panel = existing;
                m_PanelRenderer = m_Panel.GetComponent<MeshRenderer>();
            }
            else
            {
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Backdrop";
                quad.hideFlags = HideFlags.DontSave;

                // Purely decorative, so it must never take part in physics or raycasts.
                var collider = quad.GetComponent<Collider>();

                if (collider != null)
                {
                    if (Application.isPlaying)
                        Destroy(collider);
                    else
                        DestroyImmediate(collider);
                }

                m_Panel = quad.transform;
                m_Panel.SetParent(transform, false);
                m_PanelRenderer = quad.GetComponent<MeshRenderer>();
            }

            m_PanelRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            m_PanelRenderer.receiveShadows = false;
            m_PanelRenderer.sortingOrder = m_SortingOrder;
            m_PanelRenderer.sharedMaterial = ResolveMaterial();
        }

        Material ResolveMaterial()
        {
            if (m_PanelMaterial != null)
                return m_PanelMaterial;

            var shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            m_PanelMaterial = new Material(shader) { name = "Label Backdrop" };

            // Transparent surface setup for URP's unlit shader.
            m_PanelMaterial.SetFloat("_Surface", 1f);
            m_PanelMaterial.SetFloat("_Blend", 0f);
            m_PanelMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m_PanelMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m_PanelMaterial.SetFloat("_ZWrite", 0f);
            m_PanelMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m_PanelMaterial.DisableKeyword("_ALPHATEST_ON");

            if (m_DrawOnTop)
                m_PanelMaterial.SetFloat("_ZTest", (float)UnityEngine.Rendering.CompareFunction.Always);

            m_PanelMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay - 1;

            if (m_PanelMaterial.HasProperty("_BaseColor"))
                m_PanelMaterial.SetColor("_BaseColor", m_Color);

            if (m_PanelMaterial.HasProperty("_Color"))
                m_PanelMaterial.SetColor("_Color", m_Color);

            return m_PanelMaterial;
        }
    }
}
