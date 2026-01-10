using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using Bark.Gestures;
using Bark.GUI;
using Bark.Tools;
using Bark.Extensions;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using BepInEx.Configuration;
using Bark.Interaction;

namespace Bark.Modules.Movement
{
    public class GrapplingHooks : BarkModule
    {
        #region Constants
        public static readonly string DisplayName = "Grappling Hooks";

        // Holster positioning relative to player body
        private static readonly Vector3 HOLSTER_OFFSET = new Vector3(0.15f, -0.15f, 0.15f);

        // Configuration multipliers (convert config values 0-10 to useful ranges)
        private const float SPRING_FORCE_MULTIPLIER = 2f;
        private const float STEERING_FORCE_DIVISOR = 2f;
        private const float MAX_LENGTH_MULTIPLIER = 5f;

        // Default config values
        private const int DEFAULT_CONFIG_VALUE = 5;
        #endregion

        private GameObject bananaGunPrefab, bananaGunL, bananaGunR;
        private Transform holsterL, holsterR;

        void Awake()
        {
            if (Plugin.assetBundle == null)
            {
                Logging.Error("AssetBundle is null. Cannot load 'Banana Gun'.");
                return;
            }
            bananaGunPrefab = Plugin.assetBundle.LoadAsset<GameObject>("Banana Gun");
            if (bananaGunPrefab == null)
            {
                Logging.Error("Failed to load 'Banana Gun' prefab from asset bundle.");
            }
        }

        protected override void Start()
        {
            base.Start();
        }

        void Setup()
        {
            try
            {
                if (bananaGunPrefab == null)
                {
                    if (Plugin.assetBundle == null) {
                        Logging.Error("AssetBundle is null. Cannot setup grappling hooks.");
                        return;
                    }
                    bananaGunPrefab = Plugin.assetBundle.LoadAsset<GameObject>("Banana Gun");
                    if (bananaGunPrefab == null) {
                        Logging.Error("Failed to load 'Banana Gun' prefab in Setup. Aborting setup.");
                        return;
                    }
                }

                holsterL = new GameObject($"Holster (Left)").transform;
                bananaGunL = Instantiate(bananaGunPrefab);
                SetupBananaGun(ref holsterL, ref bananaGunL, true);

                holsterR = new GameObject($"Holster (Right)").transform;
                bananaGunR = Instantiate(bananaGunPrefab);
                SetupBananaGun(ref holsterR, ref bananaGunR, false);
                ReloadConfiguration();
            }
            catch (ArgumentException e)
            {
                Logging.Exception(e, "Setup failed: A required prefab or asset was likely null.");
            }
            catch (NullReferenceException e)
            {
                Logging.Exception(e, "Setup failed: A required game object or component was not found.");
            }
        }

        void SetupBananaGun(ref Transform holster, ref GameObject bananaGun, bool isLeft)
        {
            try
            {
                holster.SetParent(Player.Instance.bodyCollider.transform, false);
                var offset = new Vector3(
                    HOLSTER_OFFSET.x * (isLeft ? -1 : 1),
                    HOLSTER_OFFSET.y,
                    HOLSTER_OFFSET.z
                );
                holster.localPosition = offset;

                var gun = bananaGun.AddComponent<BananaGun>();
                gun.name = isLeft ? "Banana Grapple Left" : "Banana Grapple Right";
                gun.Holster(holster);
                gun.SetupInteraction();
            }
            catch (NullReferenceException e)
            {
                Logging.Exception(e, "SetupBananaGun failed. This is likely due to Player.Instance, the holster, or the bananaGun object being null.");
            }
        }
        protected override void Cleanup()
        {
            holsterL?.gameObject?.Obliterate();
            holsterR?.gameObject?.Obliterate();
            bananaGunL?.gameObject?.Obliterate();
            bananaGunR?.gameObject?.Obliterate();
        }

        protected override void OnEnable()
        {
            if (!MenuController.Instance.Built) return;
            base.OnEnable();
            Setup();
        }

        public static ConfigEntry<int> Spring, Steering, MaxLength;
        public static ConfigEntry<string> RopeType;
        protected override void ReloadConfiguration()
        {
            var guns = new BananaGun[] { bananaGunL?.GetComponent<BananaGun>(), bananaGunR?.GetComponent<BananaGun>() };
            foreach (var gun in guns)
            {
                if (!gun) continue;
                gun.pullForce = Spring.Value * SPRING_FORCE_MULTIPLIER;
                gun.ropeType = RopeType.Value == "elastic" ? BananaGun.RopeType.ELASTIC : BananaGun.RopeType.STATIC;
                gun.steerForce = Steering.Value / STEERING_FORCE_DIVISOR;
                gun.maxLength = MaxLength.Value * MAX_LENGTH_MULTIPLIER;
                Logging.Debug(
                    "gun.pullForce:", gun.pullForce,
                    "gun.ropeType:", gun.ropeType,
                    "gun.steerForce:", gun.steerForce,
                    "gun.maxLength:", gun.maxLength
                );
            }
        }

        public static void BindConfigEntries()
        {
            RopeType = Plugin.configFile.Bind(
                section: DisplayName,
                key: "rope type",
                defaultValue: "elastic",
                configDescription: new ConfigDescription(
                    "Whether the rope should pull you to the anchor point or not",
                    new AcceptableValueList<string>("elastic", "rope")
                )
            );

            Spring = Plugin.configFile.Bind(
                section: DisplayName,
                key: "springiness",
                defaultValue: DEFAULT_CONFIG_VALUE,
                description: "If ropes are elastic, this is how springy the ropes are"
            );

            Steering = Plugin.configFile.Bind(
                section: DisplayName,
                key: "steering",
                defaultValue: DEFAULT_CONFIG_VALUE,
                description: "How much influence you have over your velocity"
            );

            MaxLength = Plugin.configFile.Bind(
                section: DisplayName,
                key: "max length",
                defaultValue: DEFAULT_CONFIG_VALUE,
                description: "The maximum distance that the grappling hook can reach"
            );
        }

        public override string GetDisplayName()
        {
            return DisplayName;
        }

        public override string Tutorial()
        {
            return "Grab the grappling hook off of your waist with [Grip]. " +
                "Then fire with [Trigger]. " +
                "You can steer in the air by pointing the guns where you want to go.";
        }
    }

    /// <summary>
    /// BananaGun is a coordinator class that manages the grappling hook functionality.
    /// It delegates physics to BananaGunPhysics and visuals to BananaGunVisuals,
    /// following the Single Responsibility Principle.
    /// </summary>
    public class BananaGun : BarkGrabbable
    {
        #region Constants
        private const float GRAPPLE_RAYCAST_RADIUS = 0.5f;
        private static readonly Vector3 GUN_MODEL_LOCAL_POSITION = new Vector3(0.55f, 0, 0.85f);

        // Default configuration values
        private const float DEFAULT_PULL_FORCE = 10f;
        private const float DEFAULT_STEER_FORCE = 5f;
        private const float DEFAULT_MAX_LENGTH = 30f;
        #endregion

        public enum RopeType
        {
            ELASTIC, STATIC
        }

        // Composition - delegate to specialized classes
        private BananaGunPhysics physics;
        private BananaGunVisuals visuals;

        // References
        public Transform holster;
        private GameObject openModel, closedModel;
        private LineRenderer rope, laser;

        // Configuration (set by parent GrapplingHooks module)
        public RopeType ropeType;
        public float pullForce = DEFAULT_PULL_FORCE;
        public float steerForce = DEFAULT_STEER_FORCE;
        public float maxLength = DEFAULT_MAX_LENGTH;

        protected override void Awake()
        {
            base.Awake();
            LocalPosition = GUN_MODEL_LOCAL_POSITION;

            // Find visual components - BananaGun owns hierarchy knowledge
            openModel = transform.Find("Banana Gun Open").gameObject;
            closedModel = transform.Find("Banana Gun Closed").gameObject;
            rope = openModel.GetComponentInChildren<LineRenderer>();
            laser = closedModel.GetComponentInChildren<LineRenderer>();

            // Initialize composed classes with dependency injection
            physics = new BananaGunPhysics(Player.Instance.bodyCollider.attachedRigidbody);
            visuals = new BananaGunVisuals(openModel, closedModel, rope, laser);
        }

        public void Holster(Transform holster)
        {
            Close();
            this.holster = holster;
            transform.SetParent(holster);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;
            visuals.HideLaser();
        }

        public override void OnActivate(BarkInteractor interactor)
        {
            base.OnActivate(interactor);
            Activated = true;
        }

        public override void OnDeactivate(BarkInteractor interactor)
        {
            base.OnDeactivate(interactor);
            Activated = false;
            Close();
        }

        void StartSwing()
        {
            Vector3? hitPoint = TryGetGrapplePoint();
            if (!hitPoint.HasValue) return;

            visuals.SetGrappleState(true);
            physics.StartGrapple(hitPoint.Value, ropeType, pullForce, visuals.GetRopeOrigin());
        }

        /// <summary>
        /// Performs a raycast to find a valid grapple point.
        /// </summary>
        /// <returns>The hit point if found, null otherwise</returns>
        private Vector3? TryGetGrapplePoint()
        {
            Ray ray = new Ray(visuals.GetRopeOrigin(), transform.forward);
            if (UnityEngine.Physics.SphereCast(ray, GRAPPLE_RAYCAST_RADIUS * Player.Instance.scale,
                out RaycastHit hit, maxLength, Teleport.layerMask))
            {
                return hit.point;
            }
            return null;
        }

        void FixedUpdate()
        {
            if (Selected && !physics.IsGrappling && Activated)
            {
                StartSwing();
                return;
            }

            if (physics.IsGrappling)
            {
                physics.ApplySteering(transform.forward, steerForce, Player.Instance.scale);
            }
        }

        void UpdateLineRenderer()
        {
            if (!physics.IsGrappling && Selected)
            {
                // Show targeting laser
                Vector3? hitPoint = TryGetGrapplePoint();
                visuals.UpdateLaser(hitPoint);
            }
            else if (physics.IsGrappling)
            {
                // Show grapple rope
                visuals.UpdateRope(physics.GrapplePoint);
            }
        }

        public override void OnDeselect(BarkInteractor interactor)
        {
            base.OnDeselect(interactor);
            visuals.HideLaser();
            Holster(holster);
        }

        public void SetupInteraction()
        {
            this.throwOnDetach = false;
            gameObject.layer = BarkInteractor.InteractionLayer;
            visuals.SetInteractionLayer(BarkInteractor.InteractionLayer);
        }

        void Close()
        {
            visuals.SetGrappleState(false);
            Activated = false;
            physics.EndGrapple();
        }

        private void OnEnable()
        {
            Application.onBeforeRender += UpdateLineRenderer;
        }

        private void OnDisable()
        {
            Application.onBeforeRender -= UpdateLineRenderer;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            physics?.Cleanup();
        }
    }
}
