using UnityEngine;

namespace GravityLab
{
    /// <summary>
    /// Feeds a rigidbody's rotation into a <see cref="TesseractSlice"/>, so tumbling the
    /// physical object turns the 4D cube it contains. The body's three Euler angles drive the
    /// three ordinary planes; each also bleeds into a plane involving w, so a purely 3D spin
    /// still reshapes the cross-section instead of merely re-orienting it.
    /// </summary>
    /// <remarks>
    /// Parenting the slice to the body already carries the finished mesh around in 3D. What
    /// that cannot do is change which 3D shape the section is, since that depends on the
    /// tesseract's orientation in four dimensions. This maps one onto the other.
    /// </remarks>
    [RequireComponent(typeof(TesseractSlice))]
    public class RigidbodyDrivenTesseract : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Body whose rotation drives the 4D orientation. Leave empty to use one on a parent.")]
        Rigidbody m_Body;

        [SerializeField]
        [Tooltip("How much the body's pitch, yaw and roll feed the matching 3D rotation planes")]
        float m_ThreeDInfluence = 1f;

        [SerializeField]
        [Tooltip("How much the body's rotation bleeds into the planes involving w. This is what makes the section change shape rather than just turn.")]
        float m_FourDInfluence = 0.6f;

        [SerializeField]
        [Tooltip("Also drive the slice's own idle spin. Turn off to let the body be the only source of rotation.")]
        bool m_SuppressIdleSpin = true;

        TesseractSlice m_Slice;

        void Awake()
        {
            m_Slice = GetComponent<TesseractSlice>();

            if (m_Body == null)
                m_Body = GetComponentInParent<Rigidbody>();

            if (m_SuppressIdleSpin)
            {
                for (var i = 0; i < 6; i++)
                    m_Slice.SetSpeed((TesseractSlice.RotationPlane)i, 0f);
            }
        }

        void Update()
        {
            if (m_Body == null || m_Slice == null)
                return;

            // Only recompute when the slice itself is due, so this cannot outpace the mesh
            // rebuild it feeds.
            if (!m_Slice.isRebuildDue)
                return;

            var euler = m_Body.rotation.eulerAngles;

            m_Slice.SetAngle(TesseractSlice.RotationPlane.XY, euler.z * m_ThreeDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.XZ, euler.y * m_ThreeDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.YZ, euler.x * m_ThreeDInfluence);

            // Each 3D axis also drives a w plane, so a tumble in ordinary space sweeps the
            // cube through the fourth dimension and the section's topology changes.
            m_Slice.SetAngle(TesseractSlice.RotationPlane.XW, euler.x * m_FourDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.YW, euler.y * m_FourDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.ZW, euler.z * m_FourDInfluence);
        }
    }
}
