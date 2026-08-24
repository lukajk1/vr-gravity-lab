using UnityEngine;

using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

namespace DioramaXR
{
    /// <summary>
    /// A teleportation area that only accepts destinations on surfaces flat enough to
    /// stand on. Lets a single area component cover a whole environment without letting
    /// the player land on walls, steep roofs, or other unwalkable geometry.
    /// </summary>
    public class SlopeFilteredTeleportationArea : TeleportationArea
    {
        [SerializeField]
        [Tooltip("Steepest surface, in degrees from horizontal, that can be teleported onto")]
        float m_MaxSlopeAngle = 40f;

        [SerializeField]
        [Tooltip("Reject destinations on colliders in these layers, e.g. water")]
        LayerMask m_BlockedLayers = 0;

        [SerializeField]
        [Tooltip("Reject destinations on colliders whose name contains any of these (case-insensitive)")]
        string[] m_BlockedNameFragments = { "ocean", "water" };

        [SerializeField]
        [Tooltip("Lift the destination slightly to avoid landing inside the floor")]
        float m_SurfaceOffset = 0.02f;

        public float maxSlopeAngle
        {
            get => m_MaxSlopeAngle;
            set => m_MaxSlopeAngle = value;
        }

        protected override bool GenerateTeleportRequest(UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor interactor, RaycastHit raycastHit, ref TeleportRequest teleportRequest)
        {
            if (!IsValidDestination(raycastHit))
                return false;

            if (!base.GenerateTeleportRequest(interactor, raycastHit, ref teleportRequest))
                return false;

            teleportRequest.destinationPosition += Vector3.up * m_SurfaceOffset;
            return true;
        }

        bool IsValidDestination(RaycastHit hit)
        {
            // Too steep to stand on.
            if (Vector3.Angle(hit.normal, Vector3.up) > m_MaxSlopeAngle)
                return false;

            var hitCollider = hit.collider;
            if (hitCollider == null)
                return true;

            if ((m_BlockedLayers.value & (1 << hitCollider.gameObject.layer)) != 0)
                return false;

            if (m_BlockedNameFragments != null)
            {
                // Walk up the hierarchy so a blocked parent (e.g. the ocean root) also
                // rejects its child tiles.
                for (var current = hitCollider.transform; current != null; current = current.parent)
                {
                    var name = current.name.ToLowerInvariant();
                    foreach (var fragment in m_BlockedNameFragments)
                    {
                        if (!string.IsNullOrEmpty(fragment) && name.Contains(fragment.ToLowerInvariant()))
                            return false;
                    }
                }
            }

            return true;
        }
    }
}
