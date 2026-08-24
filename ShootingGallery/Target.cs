using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace ShootingGallery
{
    /// <summary>
    /// A gallery target. When shot it falls flat, awards points, and after a delay pops
    /// back upright ready to be hit again.
    /// </summary>
    public class Target : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Points awarded for hitting this target")]
        int m_PointValue = 10;

        [SerializeField]
        [Tooltip("Seconds the target stays down before resetting")]
        float m_RespawnDelay = 2f;

        [SerializeField]
        [Tooltip("How fast the target falls and rises, in degrees per second")]
        float m_FlipSpeed = 540f;

        [SerializeField]
        [Tooltip("Renderer tinted to show hit feedback. Defaults to this object's renderer.")]
        Renderer m_Renderer = null;

        [SerializeField]
        [Tooltip("Colour flashed when the target is hit")]
        Color m_HitColor = Color.green;

        [SerializeField]
        [Tooltip("Played when the target is hit")]
        AudioSource m_HitAudio = null;

        [Tooltip("Raised when this target is hit, carrying the points scored")]
        public UnityEvent<int> onHit;

        Quaternion m_UprightRotation;
        Color m_RestColor;
        bool m_IsDown;

        public int pointValue => m_PointValue;
        public bool isDown => m_IsDown;

        void Awake()
        {
            m_UprightRotation = transform.localRotation;

            if (m_Renderer == null)
                m_Renderer = GetComponentInChildren<Renderer>();

            if (m_Renderer != null)
                m_RestColor = m_Renderer.material.color;
        }

        public void Hit(Vector3 hitPoint)
        {
            if (m_IsDown)
                return;

            m_IsDown = true;

            if (m_HitAudio != null)
                m_HitAudio.Play();

            onHit?.Invoke(m_PointValue);
            ScoreKeeper.ReportHit(m_PointValue);

            StopAllCoroutines();
            StartCoroutine(FlipRoutine());
        }

        IEnumerator FlipRoutine()
        {
            if (m_Renderer != null)
                m_Renderer.material.color = m_HitColor;

            // Fall backwards flat, wait, then swing back upright.
            var downRotation = m_UprightRotation * Quaternion.Euler(90f, 0f, 0f);
            yield return RotateTo(downRotation);

            yield return new WaitForSeconds(m_RespawnDelay);

            if (m_Renderer != null)
                m_Renderer.material.color = m_RestColor;

            yield return RotateTo(m_UprightRotation);

            m_IsDown = false;
        }

        IEnumerator RotateTo(Quaternion target)
        {
            while (Quaternion.Angle(transform.localRotation, target) > 0.5f)
            {
                transform.localRotation = Quaternion.RotateTowards(
                    transform.localRotation, target, m_FlipSpeed * Time.deltaTime);
                yield return null;
            }

            transform.localRotation = target;
        }
    }
}
