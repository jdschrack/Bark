using Bark.GUI;
using Bark.Tools;
using BepInEx.Configuration;
using GorillaLocomotion;
using GorillaNetworking;
using Player = GorillaLocomotion.GTPlayer;
using System;
using HarmonyLib;

namespace Bark.Modules
{
    public class SpeedBoost : BarkModule
    {
        public static readonly string DisplayName = "Speed Boost";
        public static float baseVelocityLimit, scale = 1.5f;
        public static bool active = false;

        /// <summary>
        /// Gets the current game mode string using Traverse to access potentially private API.
        /// Returns the game mode or null if not available.
        /// </summary>
        private static string GetGameMode()
        {
            try
            {
                // Try using GorillaComputer.instance.currentGameMode first (most reliable)
                if (GorillaComputer.instance != null)
                {
                    // currentGameMode might be a WatchableStringSO now, so use Traverse
                    var currentGameModeField = Traverse.Create(GorillaComputer.instance).Field("currentGameMode");
                    var value = currentGameModeField.GetValue();
                    if (value is string strValue)
                        return strValue;
                    // If it's a WatchableStringSO, try to get its Value property
                    if (value != null)
                    {
                        var valueProperty = Traverse.Create(value).Property("Value");
                        return valueProperty.GetValue<string>();
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
            return null;
        }

        void FixedUpdate()
        {
            try
            {
                var gameMode = GetGameMode();
                if (active && (gameMode is null || gameMode == "NONE" || gameMode == "CASUAL"))
                {
                    Player.Instance.jumpMultiplier = 1.3f * scale;
                    Player.Instance.maxJumpSpeed = 8.5f * scale;
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        protected override void OnEnable()
        {
            if (!MenuController.Instance.Built) return;
            base.OnEnable();
            active = true;
            baseVelocityLimit = Player.Instance.velocityLimit;
            ReloadConfiguration();
        }

        protected override void Cleanup()
        {
            if (active)
            {
                scale = 1;
                Player.Instance.velocityLimit = baseVelocityLimit;
                active = false;
            }
        }
        protected override void ReloadConfiguration()
        {
            scale = 1 + (Speed.Value / 10f);
            if(this.enabled)
                Player.Instance.velocityLimit = baseVelocityLimit * scale;
        }

        public static ConfigEntry<int> Speed;
        public static void BindConfigEntries()
        {
            Speed = Plugin.configFile.Bind(
                section: DisplayName,
                key: "speed",
                defaultValue: 5,
                description: "How fast you run while speed boost is active"
            );
        }

        public override string GetDisplayName()
        {
            return DisplayName;
        }
        public override string Tutorial()
        {
            return "Effect: Increases your jump strength.";
        }

    }
}
