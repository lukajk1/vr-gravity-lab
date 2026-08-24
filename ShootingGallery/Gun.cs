using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace ShootingGallery
{
    /// <summary>
    /// Grabbable gun that launches a rigidbody projectile from the muzzle when the
    /// trigger is pulled. Hook up via the XRGrabInteractable's activated event, or let
    /// this component find the interactable on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class Gun : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("The projectile prefab spawned on each shot. Needs a Rigidbody.")]
        GameObject m_ProjectilePrefab = null;

        [SerializeField]
        [Tooltip("Where projectiles spawn, aimed down its local +Z axis")]
        Transform m_Muzzle = null;

        [SerializeField]
        [Tooltip("Launch speed in metres per second")]
        float m_MuzzleVelocity = 30f;

        [SerializeField]
        [Tooltip("Minimum seconds between shots")]
        float m_FireCooldown = 0.25f;

        [SerializeField]
        [Tooltip("Degrees the gun kicks upward on each shot")]
        float m_RecoilAngle = 6f;

        [SerializeField]
        [Tooltip("How quickly the gun settles back after recoil")]
        float m_RecoilRecoverySpeed = 8f;

        [SerializeField]
        [Tooltip("Played on each shot")]
        AudioSource m_FireAudio = null;

        [SerializeField]
        [Tooltip("Optional muzzle flash, briefly enabled on each shot")]
        ParticleSystem m_MuzzleFlash = null;

        XRGrabInteractable m_Interactable;
        float m_NextFireTime;
        float m_CurrentRecoil;

        void Awake()
        {
            m_Interactable = GetComponent<XRGrabInteractable>();
        }

        void OnEnable()
        {
            m_Interactable.activated.AddListener(OnActivated);
        }

        void OnDisable()
        {
            m_Interactable.activated.RemoveListener(OnActivated);
        }

        void Update()
        {
            // Ease the recoil kick back to zero so the muzzle returns to rest.
            if (m_CurrentRecoil > 0f)
            {
                m_CurrentRecoil = Mathf.Lerp(m_CurrentRecoil, 0f, m_RecoilRecoverySpeed * Time.deltaTime);
                if (m_CurrentRecoil < 0.01f)
                    m_CurrentRecoil = 0f;
            }
        }

        /// <summary>
        /// Stops a freshly spawned round from colliding with the gun that fired it, or
        /// with whatever is currently holding the gun.
        /// </summary>
        void IgnoreCollisionsWithSelf(GameObject projectile)
        {
            var projectileColliders = projectile.GetComponentsInChildren<Collider>();
            if (projectileColliders.Length == 0)
                return;

            foreach (var gunCollider in GetComponentsInChildren<Collider>())
            {
                foreach (var projectileCollider in projectileColliders)
                    Physics.IgnoreCollision(projectileCollider, gunCollider);
            }

            // Also ignore the interactor currently holding the gun, so a hand collider
            // in front of the muzzle does not swallow the shot.
            if (m_Interactable != null && m_Interactable.isSelected)
            {
                foreach (var interactor in m_Interactable.interactorsSelecting)
                {
                    var holder = interactor.transform;
                    if (holder == null)
                        continue;

                    foreach (var holderCollider in holder.GetComponentsInChildren<Collider>())
                    {
                        foreach (var projectileCollider in projectileColliders)
                            Physics.IgnoreCollision(projectileCollider, holderCollider);
                    }
                }
            }
        }

        void OnActivated(ActivateEventArgs args)
        {
            Fire();
        }

        public void Fire()
        {
            if (Time.time < m_NextFireTime)
                return;

            if (m_ProjectilePrefab == null || m_Muzzle == null)
            {
                Debug.LogWarning("Gun is missing a projectile prefab or muzzle transform.", this);
                return;
            }

            m_NextFireTime = Time.time + m_FireCooldown;

            // Apply the accumulated recoil as an upward tilt on the shot direction.
            m_CurrentRecoil = Mathf.Min(m_CurrentRecoil + m_RecoilAngle, m_RecoilAngle * 3f);
            var direction = Quaternion.AngleAxis(-m_CurrentRecoil, m_Muzzle.right) * m_Muzzle.forward;

            var projectile = Instantiate(m_ProjectilePrefab, m_Muzzle.position, Quaternion.LookRotation(direction));

            // The muzzle sits inside the gun's own collider, so without this the round
            // collides with the weapon on its first frame and never leaves the barrel.
            IgnoreCollisionsWithSelf(projectile);

            if (projectile.TryGetComponent(out Rigidbody body))
            {
                body.linearVelocity = direction * m_MuzzleVelocity;
            }
            else
            {
                Debug.LogWarning("Projectile prefab has no Rigidbody, it will not travel.", this);
            }

            if (m_FireAudio != null)
                m_FireAudio.Play();

            if (m_MuzzleFlash != null)
                m_MuzzleFlash.Play();
        }
    }
}
