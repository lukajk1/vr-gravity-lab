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
        [Tooltip("Seconds for the 4D orientation to catch up to the body. Higher is smoother and laggier; zero follows the body exactly.")]
        [Range(0f, 1f)]
        float m_SmoothTime = 0.12f;

        [SerializeField]
        [Tooltip("Degrees per second the smoothed angles may change. Zero is uncapped; a limit keeps a violent throw from spinning the section faster than it can be read.")]
        float m_MaxAngularSpeed = 360f;

        [SerializeField]
        [Tooltip("Also drive the slice's own idle spin. Turn off to let the body be the only source of rotation.")]
        bool m_SuppressIdleSpin = true;

        TesseractSlice m_Slice;

        // Euler angles wrap at 360, so a body turning smoothly past that point reports a jump
        // from 359 to 0. These accumulate the unwrapped angle instead, keeping the fed value
        // continuous however far the object tumbles.
        readonly float[] m_Unwrapped = new float[3];
        readonly float[] m_PreviousEuler = new float[3];

        readonly float[] m_Smoothed = new float[3];
        readonly float[] m_Velocity = new float[3];

        bool m_Initialised;

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

            var euler = m_Body.rotation.eulerAngles;
            var raw = new[] { euler.x, euler.y, euler.z };

            if (!m_Initialised)
            {
                for (var i = 0; i < 3; i++)
                {
                    m_PreviousEuler[i] = raw[i];
                    m_Unwrapped[i] = raw[i];
                    m_Smoothed[i] = raw[i];
                    m_Velocity[i] = 0f;
                }

                m_Initialised = true;
            }
            else
            {
                for (var i = 0; i < 3; i++)
                {
                    // DeltaAngle gives the short way round, so a 359 to 1 step reads as +2
                    // rather than -358 and the running total stays continuous.
                    m_Unwrapped[i] += Mathf.DeltaAngle(m_PreviousEuler[i], raw[i]);
                    m_PreviousEuler[i] = raw[i];

                    m_Smoothed[i] = m_SmoothTime > 0f
                        ? Mathf.SmoothDamp(m_Smoothed[i], m_Unwrapped[i], ref m_Velocity[i],
                            m_SmoothTime, m_MaxAngularSpeed > 0f ? m_MaxAngularSpeed : Mathf.Infinity,
                            Time.deltaTime)
                        : m_Unwrapped[i];
                }
            }

            // The angles are smoothed every frame so the easing stays framerate-independent,
            // but only written when the slice is actually going to rebuild.
            if (!m_Slice.isRebuildDue)
                return;

            m_Slice.SetAngle(TesseractSlice.RotationPlane.XY, m_Smoothed[2] * m_ThreeDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.XZ, m_Smoothed[1] * m_ThreeDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.YZ, m_Smoothed[0] * m_ThreeDInfluence);

            // Each 3D axis also drives a w plane, so a tumble in ordinary space sweeps the
            // cube through the fourth dimension and the section's topology changes.
            m_Slice.SetAngle(TesseractSlice.RotationPlane.XW, m_Smoothed[0] * m_FourDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.YW, m_Smoothed[1] * m_FourDInfluence);
            m_Slice.SetAngle(TesseractSlice.RotationPlane.ZW, m_Smoothed[2] * m_FourDInfluence);
        }
    }
}
