using HarmonyLib;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using System;
using Bark.Tools;
using Bark.Modules.Physics;
using UnityEngine;
using Bark.Gestures;
using Photon.Pun;
using System.Collections.Generic;
using System.Reflection;
using Bark.Extensions;

namespace Bark.Patches
{
    [HarmonyPatch]
    public class VRRigCachePatches
    {
        public static Action<GTPlayer, VRRig> OnRigCached;

        static IEnumerable<MethodBase> TargetMethods()
        {
            return new MethodBase[] {
                AccessTools.Method("VRRigCache:RemoveRigFromGorillaParent")
            };
        }

        private static void Prefix(GTPlayer player, VRRig vrrig)
        {
            OnRigCached?.Invoke(player, vrrig);
        }
    }
}
