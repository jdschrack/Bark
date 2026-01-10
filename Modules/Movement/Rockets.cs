using System;
using UnityEngine;
using Bark.Gestures;
using Bark.GUI;
using Bark.Tools;
using Bark.Extensions;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using BepInEx.Configuration;
using Bark.Interaction;
using Random = UnityEngine.Random;

namespace Bark.Modules.Movement
{
    public class Rockets : BarkModule
    {
        #region Constants
        public static readonly string DisplayName = "Rockets";

        // Rocket positioning relative to hand
        private static readonly Vector3 ROCKET_LOCAL_POSITION = new Vector3(0.51f, -3f, 0f);
        private static readonly Vector3 ROCKET_LOCAL_ROTATION = new Vector3(0f, 0f, -90f);

        // Configuration multipliers
        private const float POWER_MULTIPLIER = 2f;
        private const int DEFAULT_POWER = 5;
        private const int DEFAULT_VOLUME_CONFIG = 10;

        // Volume config range
        private const int VOLUME_CONFIG_MIN = 0;
        private const int VOLUME_CONFIG_MAX = 10;
        private const float VOLUME_OUTPUT_MIN = 0f;
        private const float VOLUME_OUTPUT_MAX = 1f;
        #endregion

        public static Rockets Instance;
        private GameObject rocketPrefab;
        Rocket rocketL, rocketR;

        void Awake()
        {
            try
            {
                Instance = this;
                rocketPrefab = Plugin.assetBundle.LoadAsset<GameObject>("Rocket");
            }
            catch (Exception e)
            {
                Logging.Exception(e);
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
                if (!rocketPrefab)
                    rocketPrefab = Plugin.assetBundle.LoadAsset<GameObject>("Rocket");

                rocketL = SetupRocket(Instantiate(rocketPrefab), true);
                rocketR = SetupRocket(Instantiate(rocketPrefab), false);
                ReloadConfiguration();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        Rocket SetupRocket(GameObject rocketObj, bool isLeft)
        {
            try
            {
                rocketObj.name = isLeft ? "Bark Rocket Left" : "Bark Rocket Right";
                var rocket = rocketObj.AddComponent<Rocket>().Init(isLeft);
                rocket.LocalPosition = ROCKET_LOCAL_POSITION;
                rocket.LocalRotation = ROCKET_LOCAL_ROTATION;
                return rocket;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
            return null;
        }

        public Vector3 AddedVelocity()
        {
            return rocketL.force + rocketR.force;
        }

        protected override void Cleanup()
        {
            try
            {
                rocketL?.gameObject?.Obliterate();
                rocketR?.gameObject?.Obliterate();
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        protected override void OnEnable()
        {
            if (!MenuController.Instance.Built) return;
            base.OnEnable();
            Setup();
        }

        public static ConfigEntry<int> Power;
        public static ConfigEntry<int> Volume;
        protected override void ReloadConfiguration()
        {
            var rockets = new Rocket[] { rocketL?.GetComponent<Rocket>(), rocketR?.GetComponent<Rocket>() };
            foreach (var rocket in rockets)
            {
                if (!rocket) continue;
                rocket.power = Power.Value * POWER_MULTIPLIER;
                rocket.volume = Mathf.Clamp(
                    MathExtensions.Map(Volume.Value, VOLUME_CONFIG_MIN, VOLUME_CONFIG_MAX, VOLUME_OUTPUT_MIN, VOLUME_OUTPUT_MAX),
                    VOLUME_OUTPUT_MIN,
                    VOLUME_OUTPUT_MAX
                );
            }
        }

        public static void BindConfigEntries()
        {
            Power = Plugin.configFile.Bind(
                section: DisplayName,
                key: "power",
                defaultValue: DEFAULT_POWER,
                description: "The power of each rocket"
            );

            Volume = Plugin.configFile.Bind(
                section: DisplayName,
                key: "thruster volume",
                defaultValue: DEFAULT_VOLUME_CONFIG,
                description: "How loud the thrusters sound"
            );
        }

        public override string GetDisplayName()
        {
            return DisplayName;
        }

        public override string Tutorial()
        {
            return $"Hold either [Grip] to summon a rocket.";

        }
    }

    public class Rocket : BarkGrabbable
    {
        #region Constants
        // Physics constants for unselected rocket behavior
        private const float UNSELECTED_VELOCITY_MULTIPLIER = 10f;

        // Audio distance attenuation
        private const float AUDIO_FALLOFF_DISTANCE = 20f;
        private const float MAX_VOLUME_GAIN = 0.5f;
        private const float MIN_VOLUME_GAIN = 0f;

        // Default values
        private const float DEFAULT_POWER = 5f;
        private const float DEFAULT_VOLUME_MULTIPLIER = 0.2f;
        #endregion

        public float power = DEFAULT_POWER, volume = DEFAULT_VOLUME_MULTIPLIER;
        public Vector3 force { get; private set; }
        bool isLeft;
        GestureTracker gt;
        Rigidbody rb;
        public AudioSource exhaustSound;

        protected override void Awake()
        {
            base.Awake();
            this.rb = GetComponent<Rigidbody>();
            this.exhaustSound = GetComponent<AudioSource>();
            this.exhaustSound.Stop();
        }

        public Rocket Init(bool isLeft)
        {
            this.isLeft = isLeft;
            gt = GestureTracker.Instance;

            if(isLeft)
                gt.leftGrip.OnPressed += Attach;
            else
                gt.rightGrip.OnPressed += Attach;
            return this;
        }

        void Attach(InputTracker _)
        {
            var parent = (isLeft ? gt.leftPalmInteractor : gt.rightPalmInteractor);
            if (!this.CanBeSelected(parent)) return;
            this.transform.parent = null;
            this.transform.localScale = Vector3.one * Player.Instance.scale;
            parent.Select(this);
            exhaustSound.Stop();
            exhaustSound.time = Random.Range(0, exhaustSound.clip.length);
            exhaustSound.Play();
        }

        void FixedUpdate()
        {
            Player player = Player.Instance;
            force = this.transform.forward * this.power * Time.fixedDeltaTime * Player.Instance.scale;
            if (Selected)
                player.AddForce(force);
            else
            {
                rb.linearVelocity += force * UNSELECTED_VELOCITY_MULTIPLIER;
                force = Vector3.zero;
                transform.Rotate(Random.insideUnitSphere);
            }
            this.exhaustSound.volume = Mathf.Lerp(MAX_VOLUME_GAIN, MIN_VOLUME_GAIN, Vector3.Distance(
                player.headCollider.transform.position,
                transform.position
            ) / AUDIO_FALLOFF_DISTANCE) * volume;
        }

        public override void OnDeselect(BarkInteractor interactor)
        {
            base.OnDeselect(interactor);
            rb.linearVelocity = Player.Instance.GetCurrentVelocity();
        }

        public void SetupInteraction()
        {
            this.throwOnDetach = true;
            gameObject.layer = BarkInteractor.InteractionLayer;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (!gt) return;
            gt.leftGrip.OnPressed -= Attach;
            gt.rightGrip.OnPressed -= Attach;
        }
    }
}
