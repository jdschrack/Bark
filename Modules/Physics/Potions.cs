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
using System.Collections.Generic;
using Bark.Networking;
using HarmonyLib;

namespace Bark.Modules.Physics
{
    public class Potions : BarkModule
    {
        public static readonly string DisplayName = "Potions";

        // Scale constants
        private const float MinPlayerScale = 0.03f;
        private const float MaxPlayerScale = 20f;
        private const float ShrinkMultiplier = 0.99f;
        private const float GrowMultiplier = 1.01f;
        private const float StaticEasing = 0.5f;
        private const float ScaleLerpSpeed = 0.75f;

        // Audio pitch mapping
        private const float MinShrinkPitch = 1.5f;
        private const float MaxShrinkPitch = 1f;
        private const float MinGrowPitch = 1f;
        private const float MaxGrowPitch = 0.5f;

        // Holster positioning relative to player body
        private static readonly Vector3 HOLSTER_OFFSET = new Vector3(0.15f, -0.15f, 0.15f);

        // Pool configuration
        private const string POTION_POOL_KEY = "Bark_Potions";
        private const int POTION_POOL_SIZE = 2;

        private GameObject bottlePrefab, shrinkPotion, growPotion;
        private Material shrinkMaterial, growMaterial;
        private Transform holsterL, holsterR;
        private ObjectPool<SizePotion> potionPool;
        private SizePotion shrinkPotionComponent, growPotionComponent;
        public static SizeChanger sizeChanger;
        public static Traverse sizeChangerTraverse, minScale, maxScale;
        public static Potions Instance;
        public static bool active;

        // Networking
        public static readonly string playerSizeKey = "BarkPlayerSize";
        public static Dictionary<VRRig, SizeChanger> sizeChangers = new Dictionary<VRRig, SizeChanger>();

        void Awake()
        {
            try
            {
                Instance = this;
                NetworkPropertyHandler.Instance?.ChangeProperty(playerSizeKey, Player.Instance.scale);
                bottlePrefab = Plugin.assetBundle.LoadAsset<GameObject>("Potion Bottle");
                shrinkMaterial = Plugin.assetBundle.LoadAsset<Material>("Portal A Material");
                growMaterial = Plugin.assetBundle.LoadAsset<Material>("Portal B Material");
                Patches.VRRigCachePatches.OnRigCached += OnRigCached;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnRigCached(Player player, VRRig rig)
        {
            try
            {
                rig.transform.localScale = Vector3.one;
                rig.SetScaleFactor(1);
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void Setup()
        {
            try
            {
                if (!bottlePrefab)
                    bottlePrefab = Plugin.assetBundle.LoadAsset<GameObject>("Potion Bottle");

                NetworkPropertyHandler.Instance?.ChangeProperty(playerSizeKey, Player.Instance.scale);

                // Cache SizeChanger - create once and reuse
                EnsureSizeChangerExists();
                minScale.SetValue(Player.Instance.scale);
                maxScale.SetValue(Player.Instance.scale);

                // Get or create the potion pool
                potionPool = PoolManager.GetOrCreatePool(
                    POTION_POOL_KEY,
                    createFunc: CreatePotion,
                    onGet: OnPotionGet,
                    onRelease: OnPotionRelease,
                    initialSize: 0,
                    maxSize: POTION_POOL_SIZE
                );

                holsterL = new GameObject($"Holster (Left)").transform;
                shrinkPotionComponent = potionPool.Get();
                shrinkPotion = shrinkPotionComponent.gameObject;
                SetupPotion(ref holsterL, shrinkPotionComponent, true);

                holsterR = new GameObject($"Holster (Right)").transform;
                growPotionComponent = potionPool.Get();
                growPotion = growPotionComponent.gameObject;
                SetupPotion(ref holsterR, growPotionComponent, false);
                ReloadConfiguration();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void EnsureSizeChangerExists()
        {
            if (sizeChanger == null)
            {
                sizeChanger = new GameObject("Bark Size Changer").AddComponent<SizeChanger>();
                sizeChangerTraverse = Traverse.Create(sizeChanger);
                minScale = sizeChangerTraverse.Field("minScale");
                maxScale = sizeChangerTraverse.Field("maxScale");
                sizeChangerTraverse.Field("myType").SetValue(SizeChanger.ChangerType.Static);
                sizeChangerTraverse.Field("staticEasing").SetValue(StaticEasing);
            }
            sizeChanger.gameObject.SetActive(true);
        }

        SizePotion CreatePotion()
        {
            var potionObj = Instantiate(bottlePrefab);
            potionObj.name = "Bark Potion (Pooled)";
            return potionObj.AddComponent<SizePotion>();
        }

        void OnPotionGet(SizePotion potion)
        {
            if (potion != null && potion.gameObject != null)
            {
                potion.gameObject.SetActive(true);
            }
        }

        void OnPotionRelease(SizePotion potion)
        {
            if (potion != null && potion.gameObject != null)
            {
                // Force deselect to avoid dangling references in interactors
                potion.ForceDeselect();
                potion.OnDrink = null;
                potion.gameObject.SetActive(false);
            }
        }

        void SetupPotion(ref Transform holster, SizePotion sizePotion, bool isLeft)
        {
            try
            {
                holster.SetParent(Player.Instance.bodyCollider.transform, false);
                holster.localScale = Vector3.one;
                var offset = new Vector3(
                    HOLSTER_OFFSET.x * (isLeft ? -1 : 1),
                    HOLSTER_OFFSET.y,
                    HOLSTER_OFFSET.z
                );
                holster.localPosition = offset;

                sizePotion.name = isLeft ? "Bark Shrink Potion" : "Bark Grow Potion";
                sizePotion.Holster(holster);
                sizePotion.OnDrink += DrinkPotion;
                sizePotion.GetComponent<Renderer>().material = isLeft ? shrinkMaterial : growMaterial;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void DrinkPotion(SizePotion potion)
        {
            bool shrink = potion.gameObject == shrinkPotion;
            if (!shrink && !PositionValidator.Instance.isValidAndStable) return;

            // Capture current scale once to avoid race conditions between reads
            float currentScale = sizeChanger.MinScale;
            float multiplier = shrink ? ShrinkMultiplier : GrowMultiplier;
            float newScale = Mathf.Clamp(currentScale * multiplier, MinPlayerScale, MaxPlayerScale);

            if (newScale < 1)
                potion.gulp.pitch = MathExtensions.Map(currentScale, 0, 1, MinShrinkPitch, MaxShrinkPitch);
            else
                potion.gulp.pitch = MathExtensions.Map(currentScale, 1, MaxPlayerScale, MinGrowPitch, MaxGrowPitch);

            minScale.SetValue(newScale);
            maxScale.SetValue(newScale);
            active = true;
        }

        float cachedSize;
        void FixedUpdate()
        {
            if (cachedSize == Player.Instance.scale) return;
            NetworkPropertyHandler.Instance.ChangeProperty(playerSizeKey, Player.Instance.scale);
            cachedSize = Player.Instance.scale;
        }

        protected override void Cleanup()
        {
            try
            {
                active = false;

                // Destroy holsters (lightweight transforms, not worth pooling)
                holsterL?.gameObject?.Obliterate();
                holsterR?.gameObject?.Obliterate();

                // Return potions to pool instead of destroying
                if (potionPool != null)
                {
                    if (shrinkPotionComponent != null)
                    {
                        potionPool.Release(shrinkPotionComponent);
                        shrinkPotionComponent = null;
                        shrinkPotion = null;
                    }
                    if (growPotionComponent != null)
                    {
                        potionPool.Release(growPotionComponent);
                        growPotionComponent = null;
                        growPotion = null;
                    }
                }

                // Deactivate SizeChanger instead of destroying (keeps it cached)
                if (sizeChanger != null)
                {
                    sizeChanger.gameObject.SetActive(false);
                }
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (VRRig rig in GorillaParent.instance.vrrigs)
            {
                try
                {
                    rig.transform.localScale = Vector3.one;
                    rig.SetScaleFactor(1);
                }
                catch (Exception e) { Logging.Exception(e); };
            }
            foreach (SizeManager manager in FindObjectsByType<SizeManager>(FindObjectsSortMode.None))
            {
                Traverse managerTraverse = Traverse.Create(manager);
                Traverse scaleFromChanger = managerTraverse.Method("ScaleFromChanger");
                Traverse controllingChanger = managerTraverse.Method("ControllingChanger");
                try
                {
                    if (manager.myType != SizeManager.SizeChangerType.LocalOffline)
                    {
                        var t = manager.targetRig?.transform;
                        if (!t) continue;
                        float scale = scaleFromChanger.GetValue<float>(controllingChanger.GetValue<SizeChanger>(t), t);
                        t.localScale = Vector3.one * scale;
                        manager.targetRig.SetScaleFactor(scale);
                        NetworkPropertyHandler.Instance?.ChangeProperty(playerSizeKey, Player.Instance.scale);
                    }
                    else
                    {
                        var t = manager.mainCameraTransform;
                        var player = manager.targetPlayer;
                        float scale = scaleFromChanger.GetValue<float>(controllingChanger.GetValue<SizeChanger>(t), t);
                        player.turnParent.transform.localScale = Vector3.one * scale;
                        player.SetScale(scale);
                    }
                }
                catch (Exception e) { Logging.Exception(e); };
            }
        }

        protected override void OnEnable()
        {
            if (!MenuController.Instance.Built) return;
            NetworkPropertyHandler.Instance.ChangeProperty(playerSizeKey, Player.Instance.scale);
            base.OnEnable();
            active = false;
            Setup();

        }

        public override string GetDisplayName()
        {
            return DisplayName;
        }

        public override string Tutorial()
        {
            return string.Format("- Grab the potion off of your waist with [Grip].\n" +
                "- Pop the cork with the other [Grip].\n" +
                "- Tilt the potion to drink it.\n\n" +
                "Current size: {0:0.##}x", Player.Instance.scale);
        }

        public static ConfigEntry<bool> ShowNetworkedSizes;
        protected override void ReloadConfiguration() { }

        public static void BindConfigEntries()
        {
            ShowNetworkedSizes = Plugin.configFile.Bind(
                section: DisplayName,
                key: "show networked size",
                defaultValue: true,
                description: "Whether or not to show how big other players using the Potions module are"
            );
        }
        public static void TryGetSizeChangerForRig(VRRig rig, out SizeChanger sc)
        {
            float size = rig.GetProperty<float>(Potions.playerSizeKey);
            if (!rig.HasProperty(Potions.playerSizeKey))
            {
                sc = null;
                return;
            }
            if (sizeChangers.ContainsKey(rig))
            {
                sc = sizeChangers[rig];
                var sizeChangerTraverse = Traverse.Create(sc);
                var minScale = sizeChangerTraverse.Field("minScale");
                var maxScale = sizeChangerTraverse.Field("maxScale");

                size = Mathf.Lerp(sc.MinScale, size, ScaleLerpSpeed * Time.fixedDeltaTime);
                minScale.SetValue(size);
                maxScale.SetValue(size);
            }
            else
            {
                size = Mathf.Lerp(rig.scaleFactor, size, ScaleLerpSpeed * Time.fixedDeltaTime);
                sc = CreateSizeChanger(size);
                sizeChangers.Add(rig, sc);
            }
        }

        public static SizeChanger CreateSizeChanger(float scale)
        {
            var sizeChanger = new GameObject("Bark Size Changer").AddComponent<SizeChanger>();
            var sizeChangerTraverse = Traverse.Create(sizeChanger);
            var minScale = sizeChangerTraverse.Field("minScale");
            var maxScale = sizeChangerTraverse.Field("maxScale");
            sizeChangerTraverse.Field("myType").SetValue(SizeChanger.ChangerType.Static);
            sizeChangerTraverse.Field("staticEasing").SetValue(StaticEasing);
            minScale.SetValue(scale);
            maxScale.SetValue(scale);
            return sizeChanger;
        }
    }


    public class SizePotion : BarkGrabbable
    {
        #region Constants
        // Potion positioning relative to hand
        private static readonly Vector3 POTION_LOCAL_POSITION = new Vector3(0.55f, 0f, 0.425f);
        private static readonly Vector3 POTION_LOCAL_ROTATION = new Vector3(8f, 0f, 0f);

        // Mouth detection for drinking
        private static readonly Vector3 MOUTH_OFFSET = new Vector3(0f, -0.05f, 0.1f);
        private const float DRINKING_RANGE = 0.15f;
        #endregion

        public Transform holster;
        Vector3 corkOffset, corkScale;
        Cork cork;
        ParticleSystem drip;
        public AudioSource gulp;
        public Action<SizePotion> OnDrink;

        protected override void Awake()
        {
            try
            {
                base.Awake();
                gulp = this.GetComponent<AudioSource>();
                cork = this.transform.Find("Cork").gameObject.AddComponent<Cork>();
                cork.enabled = false;
                drip = this.transform.Find("Drip").GetComponent<ParticleSystem>();
                try
                {
                    drip.gameObject.GetComponent<ParticleSystemRenderer>().material =
                        this.GetComponent<Renderer>().material;
                }
                catch (Exception e) { Logging.Exception(e); }
                corkOffset = cork.transform.localPosition;
                corkScale = cork.transform.localScale;
                this.LocalPosition = POTION_LOCAL_POSITION;
                this.LocalRotation = POTION_LOCAL_ROTATION;
                this.throwOnDetach = false;
                this.OnSelectExit += (_, __) =>
                {
                    gulp.Stop();
                };
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        bool isFlipped, wasFlipped, inRange;
        Vector3 mouthPosition, bottlePosition;
        void FixedUpdate()
        {
            try
            {
                if (IsCorked())
                {
                    wasFlipped = false;
                    return;
                }
                isFlipped = Vector3.Dot(transform.up, Vector3.down) > 0;
                if (!wasFlipped && isFlipped)
                    drip.Play();
                if (!isFlipped && wasFlipped)
                    drip.Stop();
                wasFlipped = isFlipped;


                mouthPosition = Player.Instance.headCollider.transform.TransformPoint(MOUTH_OFFSET);
                bottlePosition = transform.position;

                Vector3 delta = bottlePosition - mouthPosition;
                inRange = Vector3.Dot(delta, Vector3.up) > 0f && delta.magnitude < DRINKING_RANGE * Player.Instance.scale;
                if (isFlipped && inRange)
                {
                    if(!gulp.isPlaying)
                        gulp.Play();
                    OnDrink?.Invoke(this);
                }
                else
                {
                    if(gulp.isPlaying)
                        gulp.Stop();
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        bool IsCorked()
        {
            return cork.transform.parent == this.transform;
        }

        public override void OnSelect(BarkInteractor interactor)
        {
            base.OnSelect(interactor);
            if (cork)
                cork.enabled = true;
        }

        public override void OnDeselect(BarkInteractor interactor)
        {
            base.OnDeselect(interactor);
            Holster(this.holster);
        }

        public override void OnPrimaryReleased(BarkInteractor interactor)
        {
            base.OnPrimaryReleased(interactor);
            if (IsCorked())
            {
                cork.Pop();
                cork.enabled = false;
            }
        }

        public override void OnActivate(BarkInteractor interactor)
        {
            base.OnActivate(interactor);
        }

        public void Holster(Transform holster)
        {
            drip.Stop();
            this.holster = holster;
            this.GetComponent<Rigidbody>().isKinematic = true;
            transform.SetParent(holster);
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            transform.localScale = Vector3.one;

            cork.enabled = false;
            cork.rb.isKinematic = true;
            cork.transform.SetParent(this.transform);
            cork.transform.localPosition = corkOffset;
            cork.transform.localScale = corkScale;
            cork.transform.localRotation = Quaternion.identity;
            cork.shouldPlayPopSound = true;
        }

        /// <summary>
        /// Forces all interactors to deselect this potion.
        /// Call before deactivating to avoid dangling references.
        /// </summary>
        public void ForceDeselect()
        {
            // Create copy to avoid modifying collection while iterating
            var selectorsToNotify = new List<BarkInteractor>(selectors);
            foreach (var selector in selectorsToNotify)
            {
                if (selector != null)
                {
                    selector.Deselect(this);
                }
            }
            selectors.Clear();

            // Reset transform state
            transform.SetParent(null);
            var rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.linearVelocity = Vector3.zero;
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            cork?.gameObject.Obliterate();
        }
    }

    public class Cork : BarkGrabbable
    {
        #region Constants
        // Cork positioning relative to hand
        private static readonly Vector3 CORK_LOCAL_POSITION = new Vector3(0.5f, 0.5f, 0.425f);
        private static readonly Vector3 CORK_LOCAL_ROTATION = new Vector3(8f, 0f, 0f);

        // Pop physics
        private const float POP_VELOCITY = 2.5f;
        #endregion

        public Rigidbody rb;
        AudioSource popSource;
        public bool shouldPlayPopSound = true;
        protected override void Awake()
        {
            base.Awake();
            this.LocalPosition = CORK_LOCAL_POSITION;
            this.LocalRotation = CORK_LOCAL_ROTATION;
            this.throwOnDetach = true;
            rb = this.GetComponent<Rigidbody>();
            rb.isKinematic = true;
            popSource = GetComponent<AudioSource>();
        }

        public override void OnSelect(BarkInteractor interactor)
        {
            base.OnSelect(interactor);
            if (shouldPlayPopSound)
                popSource.Play();
            shouldPlayPopSound = false;
        }

        public void Pop()
        {
            transform.SetParent(null);
            rb.isKinematic = false;
            rb.linearVelocity = this.transform.up * POP_VELOCITY;
            popSource.Play();
        }
    }
}
