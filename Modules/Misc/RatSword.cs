using Bark.Extensions;
using Bark.Gestures;
using Bark.GUI;
using Bark.Tools;
using System;
using UnityEngine;

namespace Bark.Modules.Misc
{
    public class RatSword : BarkModule
    {
        public static readonly string DisplayName = "Rat Sword";
        private GameObject sword;

        protected override void OnEnable()
        {
            if (!MenuController.Instance.Built) return;
            base.OnEnable();

            try
            {
                sword = Instantiate(Plugin.assetBundle.LoadAsset<GameObject>("Rat Sword"));
                sword.transform.SetParent(GestureTracker.Instance.rightHand.transform, true);
                sword.transform.localPosition = new Vector3(-0.4782f, 0.1f, 0.4f);
                sword.transform.localRotation = Quaternion.Euler(9, 0, 0);
                sword.transform.localScale /= 2;
                sword.SetActive(false);
                GestureTracker.Instance.rightGrip.OnPressed += OnGripPressed;
                GestureTracker.Instance.rightGrip.OnReleased += OnGripReleased;
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        private void OnGripPressed(InputTracker _) => sword?.SetActive(true);
        private void OnGripReleased(InputTracker _) => sword?.SetActive(false);

        protected override void Cleanup()
        {
            GestureTracker.Instance.rightGrip.OnPressed -= OnGripPressed;
            GestureTracker.Instance.rightGrip.OnReleased -= OnGripReleased;
            sword?.Obliterate();
        }

        public override string GetDisplayName()
        {
            return DisplayName;
        }

        public override string Tutorial()
        {
            return "I met a lil' kid in canyons who wanted me to make him a sword.\n" +
                "[Grip] to wield your weapon, rat kid.";
        }
    }
}
