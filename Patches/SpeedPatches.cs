using HarmonyLib;
using Bark.Modules;
using Bark.Tools;
using System;
using GorillaLocomotion;
using UnityEngine;

namespace Bark.Patches
{
    internal static class SpeedPatchHelper
    {
        public static void ApplySpeedMultiplier(ref float[] result)
        {
            if (!SpeedBoost.active) return;

            for (int i = 0; i < result.Length; i++)
                result[i] *= SpeedBoost.scale;
        }
    }

    [HarmonyPatch(typeof(GorillaTagManager))]
    [HarmonyPatch("LocalPlayerSpeed", MethodType.Normal)]
    internal class TagSpeedPatch
    {
        private static void Postfix(ref float[] __result)
        {
            try
            {
                SpeedPatchHelper.ApplySpeedMultiplier(ref __result);
            }
            catch (Exception e) { Logging.Exception(e); }
        }
    }

    [HarmonyPatch(typeof(GorillaGameManager))]
    [HarmonyPatch("LocalPlayerSpeed", MethodType.Normal)]
    internal class GenericSpeedPatch
    {
        private static void Postfix(ref float[] __result)
        {
            try
            {
                SpeedPatchHelper.ApplySpeedMultiplier(ref __result);
            }
            catch (Exception e) { Logging.Exception(e); }
        }
    }

    // GorillaBattleManager no longer exists in the current game version

    [HarmonyPatch(typeof(GorillaHuntManager))]
    [HarmonyPatch("LocalPlayerSpeed", MethodType.Normal)]
    internal class HuntSpeedPatch
    {
        private static void Postfix(ref float[] __result)
        {
            try
            {
                SpeedPatchHelper.ApplySpeedMultiplier(ref __result);
            }
            catch (Exception e) { Logging.Exception(e); }
        }
    }

    [HarmonyPatch(typeof(GTPlayer))]
    [HarmonyPatch("GetSwimmingVelocityForHand", MethodType.Normal)]
    internal class SwimmingVelocityPatch
    {
        private static void Postfix(ref Vector3 swimmingVelocityChange)
        {
            try
            {
                if (!SpeedBoost.active) return;
                swimmingVelocityChange *= SpeedBoost.scale;
            }
            catch (Exception e) { Logging.Exception(e); }
        }
    }
}
