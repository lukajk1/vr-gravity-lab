using UnityEngine;

namespace ShootingGallery
{
    /// <summary>
    /// A fired bullet. Reports hits to any Target it strikes, then cleans itself up so
    /// spent rounds do not accumulate in the scene.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class Projectile : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Seconds before an un-hit projectile removes itself")]
        float m_Lifetime = 5f;

        [SerializeField]
        [Tooltip("Optional impact effect spawned where the projectile lands")]
        GameObject m_ImpactEffectPrefab = null;

        bool m_HasHit;

        void Start()
        {
            Destroy(gameObject, m_Lifetime);
        }

        void OnCollisionEnter(Collision collision)
        {
            // Ignore everything after the first contact so one bullet cannot score twice.
            if (m_HasHit)
                return;

            m_HasHit = true;

            var contact = collision.GetContact(0);

            if (m_ImpactEffectPrefab != null)
            {
                var effect = Instantiate(m_ImpactEffectPrefab, contact.point, Quaternion.LookRotation(contact.normal));
                Destroy(effect, 2f);
            }

            // The Target component sits on the pivot while the collider is on a child
            // mesh, so search upward rather than only on the collider itself.
            var target = collision.collider.GetComponentInParent<Target>();
            if (target != null)
                target.Hit(contact.point);

            Destroy(gameObject);
        }
    }
}
