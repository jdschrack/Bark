using UnityEngine;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using Bark.Extensions;

namespace Bark.Modules.Movement
{
    /// <summary>
    /// Handles all physics-related logic for the BananaGun grappling hook.
    /// Manages SpringJoint creation, configuration, and steering forces.
    /// Each BananaGun instance should have its own BananaGunPhysics instance.
    /// </summary>
    public class BananaGunPhysics
    {
        #region Constants
        // Elastic rope physics constants
        private const float ELASTIC_MAX_DISTANCE = 0.8f;
        private const float ELASTIC_MIN_DISTANCE = 0.25f;
        private const float ELASTIC_DAMPER = 7f;
        private const float ELASTIC_MASS_SCALE = 4.5f;

        // Static rope physics constants
        private const float STATIC_SPRING_MULTIPLIER = 2f;
        private const float STATIC_DAMPER = 100f;
        private const float STATIC_MASS_SCALE = 4.5f;
        #endregion

        private SpringJoint joint;
        private readonly Rigidbody playerRigidbody;

        public bool IsGrappling { get; private set; }
        public Vector3 GrapplePoint { get; private set; }

        public BananaGunPhysics(Rigidbody playerRb)
        {
            playerRigidbody = playerRb;
        }

        /// <summary>
        /// Starts a grapple at the specified point with the given configuration.
        /// </summary>
        /// <param name="point">World position of the grapple anchor</param>
        /// <param name="ropeType">Type of rope physics to use</param>
        /// <param name="pullForce">Spring force for the joint</param>
        /// <param name="ropeOrigin">Position of the rope origin (for distance calculation)</param>
        public void StartGrapple(Vector3 point, BananaGun.RopeType ropeType, float pullForce, Vector3 ropeOrigin)
        {
            GrapplePoint = point;
            IsGrappling = true;

            EnsureJointExists();
            ConfigureJoint(ropeType, pullForce, point, ropeOrigin);
        }

        /// <summary>
        /// Ends the current grapple and destroys the joint.
        /// </summary>
        public void EndGrapple()
        {
            IsGrappling = false;
            if (joint != null)
            {
                joint.Obliterate();
                joint = null;
            }
        }

        /// <summary>
        /// Applies steering force to the player in the specified direction.
        /// </summary>
        /// <param name="direction">Direction to steer (typically transform.forward)</param>
        /// <param name="steerForce">Magnitude of steering force</param>
        /// <param name="playerScale">Current player scale multiplier</param>
        public void ApplySteering(Vector3 direction, float steerForce, float playerScale)
        {
            if (IsGrappling && playerRigidbody != null)
            {
                playerRigidbody.linearVelocity += direction * steerForce * Time.fixedDeltaTime * playerScale;
            }
        }

        /// <summary>
        /// Ensures the SpringJoint exists on the player.
        /// Creates one if it doesn't exist.
        /// </summary>
        private void EnsureJointExists()
        {
            if (joint == null)
            {
                joint = Player.Instance.gameObject.AddComponent<SpringJoint>();
            }
        }

        /// <summary>
        /// Configures the SpringJoint based on rope type and physics parameters.
        /// </summary>
        private void ConfigureJoint(BananaGun.RopeType ropeType, float pullForce, Vector3 anchorPoint, Vector3 ropeOrigin)
        {
            joint.autoConfigureConnectedAnchor = false;
            joint.connectedAnchor = anchorPoint;

            float distanceFromPoint = Vector3.Distance(ropeOrigin, anchorPoint);

            switch (ropeType)
            {
                case BananaGun.RopeType.ELASTIC:
                    joint.maxDistance = ELASTIC_MAX_DISTANCE;
                    joint.minDistance = ELASTIC_MIN_DISTANCE;
                    joint.spring = pullForce;
                    joint.damper = ELASTIC_DAMPER;
                    joint.massScale = ELASTIC_MASS_SCALE;
                    break;

                case BananaGun.RopeType.STATIC:
                    joint.maxDistance = distanceFromPoint;
                    joint.minDistance = distanceFromPoint;
                    joint.spring = pullForce * STATIC_SPRING_MULTIPLIER;
                    joint.damper = STATIC_DAMPER;
                    joint.massScale = STATIC_MASS_SCALE;
                    break;
            }
        }

        /// <summary>
        /// Cleans up the SpringJoint. Call this when the BananaGun is destroyed.
        /// </summary>
        public void Cleanup()
        {
            if (joint != null)
            {
                joint.Obliterate();
                joint = null;
            }
        }
    }
}
