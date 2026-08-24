using TMPro;
using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Turns this object to face the camera every frame, so worldspace text stays readable
    /// from anywhere in the play space. Unlike XRI's LazyFollow this does not move the
    /// object or ease the rotation, which suits a label pinned to something that is itself
    /// being thrown around.
    /// </summary>
    [ExecuteAlways]
    public class BillboardLabel : MonoBehaviour
    {
        /// <summary>How the label is allowed to rotate to meet the camera.</summary>
        public enum BillboardMode
        {
            /// <summary>Rotate on every axis. The label always squarely faces the eye.</summary>
            FullyFaceCamera,

            /// <summary>Rotate around world up only, so the label stays upright.</summary>
            UprightYawOnly,
        }

        [SerializeField]
        [Tooltip("Camera to face. Leave empty to use Camera.main, which is the XR head camera at runtime.")]
        Transform m_Target;

        [SerializeField]
        [Tooltip("How the label rotates. Upright yaw keeps text level, which reads better for a HUD-style tag.")]
        BillboardMode m_Mode = BillboardMode.UprightYawOnly;

        [SerializeField]
        [Tooltip("Face away from the camera instead of toward it. Needed when the visual's forward axis points backwards, as TextMeshPro's does.")]
        bool m_InvertForward = true;

        [SerializeField]
        [Tooltip("Keep the label the same on-screen size regardless of distance")]
        bool m_ConstantScreenSize;

        [SerializeField]
        [Tooltip("Metres of scale per metre of distance, when constant screen size is on")]
        float m_ScalePerMetre = 0.05f;

        [SerializeField]
        [Tooltip("Hide the label beyond this distance. Zero or less keeps it always visible.")]
        float m_MaxVisibleDistance;

        [SerializeField]
        [Tooltip("Optional text component this label drives, so other scripts can set the text through here")]
        TMP_Text m_Text;

        Transform m_Transform;
        Renderer[] m_Renderers;

        /// <summary>
        /// The label's displayed text, when a text component is assigned.
        /// </summary>
        public string text
        {
            get => m_Text != null ? m_Text.text : string.Empty;
            set
            {
                if (m_Text != null)
                    m_Text.text = value;
            }
        }

        void Awake()
        {
            m_Transform = transform;
            m_Renderers = GetComponentsInChildren<Renderer>(true);
        }

        void OnEnable()
        {
            // Domain reload can leave these null when entering play mode.
            if (m_Transform == null)
                m_Transform = transform;

            if (m_Renderers == null || m_Renderers.Length == 0)
                m_Renderers = GetComponentsInChildren<Renderer>(true);
        }

        void LateUpdate()
        {
            var target = ResolveTarget();

            if (target == null)
                return;

            var toCamera = m_Transform.position - target.position;

            if (m_Mode == BillboardMode.UprightYawOnly)
            {
                // Flatten so the label never pitches or rolls, only spins to follow.
                toCamera.y = 0f;

                if (toCamera.sqrMagnitude < 1e-6f)
                    return;
            }

            if (toCamera.sqrMagnitude < 1e-6f)
                return;

            var forward = m_InvertForward ? toCamera : -toCamera;
            m_Transform.rotation = Quaternion.LookRotation(forward.normalized, Vector3.up);

            var distance = Vector3.Distance(m_Transform.position, target.position);

            if (m_ConstantScreenSize)
                m_Transform.localScale = Vector3.one * Mathf.Max(distance * m_ScalePerMetre, 1e-4f);

            if (m_MaxVisibleDistance > 0f)
                SetRenderersEnabled(distance <= m_MaxVisibleDistance);
        }

        Transform ResolveTarget()
        {
            if (m_Target != null)
                return m_Target;

            var mainCamera = Camera.main;

            if (mainCamera != null)
            {
                // Cache so the Camera.main lookup does not run every frame.
                m_Target = mainCamera.transform;
                return m_Target;
            }

#if UNITY_EDITOR
            // In edit mode there is often no tagged main camera, so face the scene view
            // to make the label previewable while authoring.
            if (!Application.isPlaying)
            {
                var sceneView = UnityEditor.SceneView.lastActiveSceneView;

                if (sceneView != null && sceneView.camera != null)
                    return sceneView.camera.transform;
            }
#endif

            return null;
        }

        void SetRenderersEnabled(bool value)
        {
            foreach (var renderer in m_Renderers)
            {
                if (renderer != null && renderer.enabled != value)
                    renderer.enabled = value;
            }
        }
    }
}
