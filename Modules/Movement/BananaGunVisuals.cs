using UnityEngine;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;

namespace Bark.Modules.Movement
{
    /// <summary>
    /// Handles all visual logic for the BananaGun grappling hook.
    /// Manages LineRenderer updates and model state (open/closed).
    /// Uses dependency injection - references are passed in, not found via transform.Find().
    /// </summary>
    public class BananaGunVisuals
    {
        #region Constants
        // Haptic feedback constants
        private const int GRAPPLE_HAPTIC_AUDIO_ID = 96;
        private const float GRAPPLE_HAPTIC_INTENSITY = 0.05f;
        #endregion

        private readonly GameObject openModel;
        private readonly GameObject closedModel;
        private readonly LineRenderer rope;
        private readonly LineRenderer laser;
        private readonly float baseRopeWidth;
        private readonly float baseLaserWidth;

        /// <summary>
        /// Creates a new BananaGunVisuals instance with pre-resolved references.
        /// This decouples the visuals from the prefab hierarchy.
        /// </summary>
        /// <param name="openModel">The open/firing state model</param>
        /// <param name="closedModel">The closed/holstered state model</param>
        /// <param name="rope">LineRenderer for the grapple rope</param>
        /// <param name="laser">LineRenderer for the targeting laser</param>
        public BananaGunVisuals(GameObject openModel, GameObject closedModel,
                                LineRenderer rope, LineRenderer laser)
        {
            this.openModel = openModel;
            this.closedModel = closedModel;
            this.rope = rope;
            this.laser = laser;

            // Store base widths for scaling
            if (rope != null)
            {
                rope.useWorldSpace = false;
                baseRopeWidth = rope.startWidth;
            }
            if (laser != null)
            {
                laser.useWorldSpace = false;
                baseLaserWidth = laser.startWidth;
            }
        }

        /// <summary>
        /// Sets the visual state to open (firing) or closed (holstered).
        /// Plays haptic feedback when opening.
        /// </summary>
        public void SetGrappleState(bool isOpen)
        {
            if (isOpen)
            {
                openModel?.SetActive(true);
                closedModel?.SetActive(false);
                PlayGrappleHaptic();
            }
            else
            {
                openModel?.SetActive(false);
                closedModel?.SetActive(true);
            }
        }

        /// <summary>
        /// Updates the rope LineRenderer to show the grapple connection.
        /// </summary>
        /// <param name="grapplePoint">World position of the grapple anchor</param>
        public void UpdateRope(Vector3 grapplePoint)
        {
            if (rope == null) return;

            float playerScale = Player.Instance.scale;
            Vector3 start = Vector3.zero;
            Vector3 end = rope.transform.InverseTransformPoint(grapplePoint);

            rope.SetPosition(0, start);
            rope.SetPosition(1, end);
            rope.startWidth = baseRopeWidth * playerScale;
            rope.endWidth = baseRopeWidth * playerScale;
        }

        /// <summary>
        /// Updates the laser LineRenderer to show targeting.
        /// </summary>
        /// <param name="hitPoint">World position where the raycast hit, or null if no hit</param>
        public void UpdateLaser(Vector3? hitPoint)
        {
            if (laser == null) return;

            if (!hitPoint.HasValue)
            {
                laser.enabled = false;
                return;
            }

            float playerScale = Player.Instance.scale;
            Vector3 start = Vector3.zero;
            Vector3 end = laser.transform.InverseTransformPoint(hitPoint.Value);

            laser.enabled = true;
            laser.SetPosition(0, start);
            laser.SetPosition(1, end);
            laser.startWidth = baseLaserWidth * playerScale;
            laser.endWidth = baseLaserWidth * playerScale;
        }

        /// <summary>
        /// Hides the laser (e.g., when holstered or not selected).
        /// </summary>
        public void HideLaser()
        {
            if (laser != null)
                laser.enabled = false;
        }

        /// <summary>
        /// Gets the world position of the rope origin for raycasting.
        /// </summary>
        public Vector3 GetRopeOrigin()
        {
            return rope != null ? rope.transform.position : Vector3.zero;
        }

        /// <summary>
        /// Sets the interaction layer on models for BarkInteractor compatibility.
        /// </summary>
        public void SetInteractionLayer(int layer)
        {
            if (openModel != null)
                openModel.layer = layer;
            if (closedModel != null)
                closedModel.layer = layer;
        }

        private void PlayGrappleHaptic()
        {
            GorillaTagger.Instance?.offlineVRRig?.PlayHandTapLocal(
                GRAPPLE_HAPTIC_AUDIO_ID,
                false,
                GRAPPLE_HAPTIC_INTENSITY
            );
        }
    }
}
