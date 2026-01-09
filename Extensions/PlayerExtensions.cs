using Bark.Modules;
using GorillaLocomotion;
using HarmonyLib;
using Photon.Pun;
using System.Collections.Generic;
using UnityEngine;

namespace Bark.Extensions
{
    public static class PlayerExtensions
    {
        // Cached Traverse instances for performance
        private static Traverse playerTraverse;
        private static GTPlayer cachedPlayer;

        private static Traverse GetPlayerTraverse(GTPlayer player)
        {
            if (cachedPlayer != player || playerTraverse == null)
            {
                cachedPlayer = player;
                playerTraverse = Traverse.Create(player);
            }
            return playerTraverse;
        }

        public static void AddForce(this GTPlayer self, Vector3 v)
        {
            self.bodyCollider.attachedRigidbody.linearVelocity += v;
        }

        public static void SetVelocity(this GTPlayer self, Vector3 v)
        {
            self.bodyCollider.attachedRigidbody.linearVelocity = v;
        }

        /// <summary>
        /// Gets whether the left hand was touching a surface last frame.
        /// Uses Traverse to access the field which may be private in newer game versions.
        /// </summary>
        public static bool WasLeftHandTouching(this GTPlayer self)
        {
            return GetPlayerTraverse(self).Field("wasLeftHandTouching").GetValue<bool>();
        }

        /// <summary>
        /// Gets whether the right hand was touching a surface last frame.
        /// Uses Traverse to access the field which may be private in newer game versions.
        /// </summary>
        public static bool WasRightHandTouching(this GTPlayer self)
        {
            return GetPlayerTraverse(self).Field("wasRightHandTouching").GetValue<bool>();
        }

        /// <summary>
        /// Gets the left controller transform.
        /// Uses Traverse to access the field which may be private in newer game versions.
        /// </summary>
        public static Transform GetLeftControllerTransform(this GTPlayer self)
        {
            return GetPlayerTraverse(self).Field("leftControllerTransform").GetValue<Transform>();
        }

        /// <summary>
        /// Gets the right controller transform.
        /// Uses Traverse to access the field which may be private in newer game versions.
        /// </summary>
        public static Transform GetRightControllerTransform(this GTPlayer self)
        {
            return GetPlayerTraverse(self).Field("rightControllerTransform").GetValue<Transform>();
        }

        /// <summary>
        /// Gets the current velocity of the player.
        /// Uses Traverse to access the field which may be private in newer game versions.
        /// </summary>
        public static Vector3 GetCurrentVelocity(this GTPlayer self)
        {
            return GetPlayerTraverse(self).Field("currentVelocity").GetValue<Vector3>();
        }

        /// <summary>
        /// Sets the player's scale using Traverse (scale property may be read-only in newer game versions).
        /// </summary>
        public static void SetScale(this GTPlayer self, float scale)
        {
            GetPlayerTraverse(self).Field("scale").SetValue(scale);
        }

        /// <summary>
        /// Sets the VRRig's scale factor using Traverse (scaleFactor property may be read-only in newer game versions).
        /// </summary>
        public static void SetScaleFactor(this VRRig rig, float scaleFactor)
        {
            Traverse.Create(rig).Field("scaleFactor").SetValue(scaleFactor);
        }

        public static PhotonView PhotonView(this VRRig rig)
        {
            //return rig.photonView;
            return Traverse.Create(rig).Field("photonView").GetValue<PhotonView>();
        }

        public static T GetProperty<T>(this VRRig rig, string key)
        {
            if(rig?.PhotonView()?.Owner is Photon.Realtime.Player player)
            {
                if (player?.CustomProperties?.TryGetValue(key, out var value) == true)
                    return (T)value;
            }
            return default(T);
        }

        public static bool HasProperty(this VRRig rig, string key)
        {
            if(rig?.PhotonView()?.Owner is Photon.Realtime.Player player)
                return player.HasProperty(key);
            return false;
        }

        public static bool ModuleEnabled(this VRRig rig, string mod)
        {
            if(rig?.PhotonView()?.Owner is Photon.Realtime.Player player)
                return player.ModuleEnabled(mod);
            return false;
        }

        public static T GetProperty<T>(this Photon.Realtime.Player player, string key)
        {
            if (player?.CustomProperties?.TryGetValue(key, out var value) == true)
                return (T)value;
            return default(T);
        }

        public static bool HasProperty(this Photon.Realtime.Player player, string key)
        {
            if (player?.CustomProperties?.TryGetValue(key, out var value) == true)
                return value != null;
            return false;
        }

        public static bool ModuleEnabled(this Photon.Realtime.Player player, string mod)
        {
            if (!player.HasProperty(BarkModule.enabledModulesKey)) return false;
            Dictionary<string, bool> enabledMods = player.GetProperty<Dictionary<string, bool>>(BarkModule.enabledModulesKey);
            if (enabledMods is null || !enabledMods.ContainsKey(mod)) return false;
            return enabledMods[mod];
        }

        public static VRRig Rig(this Photon.Realtime.Player player)
        {
            foreach (var rig in GorillaParent.instance.vrrigs)
            {
                if (rig?.PhotonView()?.Owner == player)
                    return rig;
            }
            return null;
        }
    }
}
