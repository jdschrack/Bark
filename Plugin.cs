using BepInEx;
using System;
using UnityEngine;
using Utilla;
using Bark.GUI;
using Bark.Tools;
using Bark.Extensions;
using BepInEx.Configuration;
using System.IO;
using Bark.Modules;
using System.Reflection;
using Bark.Gestures;
using Bark.Networking;
using GorillaLocomotion;
using Player = GorillaLocomotion.GTPlayer;
using UnityEngine.UI;
using HarmonyLib;
using System.Collections;
using GorillaNetworking;
using Photon.Pun;

namespace Bark
{
    [ModdedGamemode]
    [BepInDependency("org.legoandmars.gorillatag.utilla", "1.5.0")]
    [BepInPlugin(PluginInfo.GUID, PluginInfo.Name, PluginInfo.Version)]

    public class Plugin : BaseUnityPlugin
    {
        public static Plugin Instance;
        public static bool initialized, inRoom;
        bool pluginEnabled = false;
        public static AssetBundle assetBundle;
        public static MenuController menuController;
        public static GameObject monkeMenuPrefab;
        public static ConfigFile configFile;
        public static bool IsSteam { get; protected set; }
        public static bool DebugMode { get; protected set; } = false;
        GestureTracker gt;
        NetworkPropertyHandler nph;


        public void Setup()
        {
            if (menuController || !pluginEnabled || !inRoom) return;
            Logging.Debug("Menu:", menuController, "Plugin Enabled:", pluginEnabled, "InRoom:", inRoom);
            try
            {
                gt = this.gameObject.GetOrAddComponent<GestureTracker>();
                nph = this.gameObject.GetOrAddComponent<NetworkPropertyHandler>();  
                menuController = Instantiate(monkeMenuPrefab).AddComponent<MenuController>();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        public void Cleanup()
        {
            try
            {
                Logging.Debug("Cleaning up");
                menuController?.gameObject?.Obliterate();
                gt?.Obliterate();
                nph?.Obliterate();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void Awake()
        {
            try
            {
                Instance = this;
                Logging.Init();
                CI.Init();
                configFile = new ConfigFile(Path.Combine(Paths.ConfigPath, "Bark.cfg"), true);
                MenuController.BindConfigEntries();
                Logging.Debug("Found", BarkModule.GetBarkModuleTypes().Count, "modules");
                foreach (Type moduleType in BarkModule.GetBarkModuleTypes())
                {
                    MethodInfo bindConfigs = moduleType.GetMethod("BindConfigEntries");
                    if (bindConfigs is null) continue;
                    bindConfigs.Invoke(null, null);
                }
            }
            catch (Exception e) { Logging.Exception(e); }
        }

        void Start()
        {
            try
            {
                Logging.Debug("Start");
                Utilla.Events.GameInitialized += OnGameInitialized;
                assetBundle = AssetUtils.LoadAssetBundle("Bark/Resources/barkbundle");
                if (assetBundle == null)
                {
                    Logging.Warning("Failed to load asset bundle - Bark will not function correctly");
                    return;
                }
                monkeMenuPrefab = assetBundle.LoadAsset<GameObject>("Bark Menu");
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        public static Text debugText;
        void CreateDebugGUI()
        {
            try
            {
                if (Player.Instance)
                {
                    var canvas = Player.Instance.headCollider.transform.GetComponentInChildren<Canvas>();
                    if (!canvas)
                    {
                        canvas = new GameObject("~~~Bark Debug Canvas").AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.WorldSpace;
                        canvas.transform.SetParent(Player.Instance.headCollider.transform);
                        canvas.transform.localPosition = Vector3.forward * .35f;
                        canvas.transform.localRotation = Quaternion.identity;
                        canvas.transform.localScale = Vector3.one;
                        canvas.gameObject.AddComponent<CanvasScaler>();
                        canvas.gameObject.AddComponent<GraphicRaycaster>();
                        canvas.GetComponent<RectTransform>().localScale = Vector3.one * .035f;
                        var text = new GameObject("~~~Text").AddComponent<Text>();
                        text.transform.SetParent(canvas.transform);
                        text.transform.localPosition = Vector3.zero;
                        text.transform.localRotation = Quaternion.identity;
                        text.transform.localScale = Vector3.one;
                        text.color = Color.green;
                        //text.text = "Hello World";
                        text.fontSize = 24;
                        text.font = Font.CreateDynamicFontFromOSFont("Arial", 24);
                        text.alignment = TextAnchor.MiddleCenter;
                        text.horizontalOverflow = HorizontalWrapMode.Overflow;
                        text.verticalOverflow = VerticalWrapMode.Overflow;
                        text.color = Color.white;
                        text.GetComponent<RectTransform>().localScale = Vector3.one * .02f;
                        debugText = text;
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnEnable()
        {
            try
            {
                Logging.Debug("OnEnable");
                this.pluginEnabled = true;
                HarmonyPatches.ApplyHarmonyPatches();
                if (initialized)
                    Setup();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnDisable()
        {
            try
            {
                Logging.Debug("OnDisable");
                this.pluginEnabled = false;
                HarmonyPatches.RemoveHarmonyPatches();
                Cleanup();
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }

        void OnGameInitialized(object sender, EventArgs e)
        {
            try
            {
                Logging.Debug("OnGameInitialized");
                initialized = true;
                string platform = (string)Traverse.Create(GorillaNetworking.PlayFabAuthenticator.instance).Field("platform").GetValue();
                Logging.Info("Platform: ", platform);
                IsSteam = platform.ToLower().Contains("steam");
                if (DebugMode)
                    CreateDebugGUI();
            }
            catch (Exception ex)
            {
                Logging.Exception(ex);
            }
        }

        [ModdedGamemodeJoin]
        void RoomJoined(string gamemode)
        {
            Logging.Debug("RoomJoined");
            inRoom = true;
            Setup();
        }

        [ModdedGamemodeLeave]
        void RoomLeft(string gamemode)
        {
            Logging.Debug("RoomLeft");
            inRoom = false;
            Cleanup();
        }

        public void JoinLobby(string name, string gamemode)
        {
            StartCoroutine(JoinLobbyInternal(name, gamemode));
        }

        IEnumerator JoinLobbyInternal(string name, string gamemode)
        {
            // Disconnect from current room - use Traverse in case API has changed
            try
            {
                var pnc = PhotonNetworkController.Instance;
                // Try to find a disconnect method - API may have changed
                var disconnectMethod = Traverse.Create(pnc).Method("AttemptDisconnect");
                if (disconnectMethod.MethodExists())
                    disconnectMethod.GetValue();
                else
                {
                    // Fallback: try to disconnect via PhotonNetwork directly
                    if (PhotonNetwork.InRoom)
                        PhotonNetwork.LeaveRoom();
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
                if (PhotonNetwork.InRoom)
                    PhotonNetwork.LeaveRoom();
            }

            do
            {
                yield return new WaitForSeconds(1f);
                Logging.Debug("Waiting to disconnect");
            }
            while (PhotonNetwork.InRoom);

            // Get current game mode using Traverse (currentGameMode may be WatchableStringSO now)
            string gamemodeCache = GetCurrentGameMode();
            Logging.Debug("Changing gamemode from", gamemodeCache, "to", gamemode);
            SetCurrentGameMode(gamemode);

            // Join the room - use Traverse in case API signature has changed
            try
            {
                var pnc = PhotonNetworkController.Instance;
                var joinMethod = Traverse.Create(pnc).Method("AttemptToJoinSpecificRoom", new Type[] { typeof(string) });
                if (joinMethod.MethodExists())
                {
                    // Old API: just name parameter
                    joinMethod.GetValue(name);
                }
                else
                {
                    // New API might have additional parameters - try reflection
                    var methods = pnc.GetType().GetMethods();
                    foreach (var method in methods)
                    {
                        if (method.Name == "AttemptToJoinSpecificRoom")
                        {
                            var parameters = method.GetParameters();
                            if (parameters.Length == 1)
                            {
                                method.Invoke(pnc, new object[] { name });
                                break;
                            }
                            else if (parameters.Length == 2)
                            {
                                // Get the enum type for the second parameter
                                var enumType = parameters[1].ParameterType;
                                var enumValue = Enum.ToObject(enumType, 0); // Use default/first enum value
                                method.Invoke(pnc, new object[] { name, enumValue });
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }

            while (!PhotonNetwork.InRoom)
            {
                yield return new WaitForSeconds(1f);
                Logging.Debug("Waiting to connect");
            }
            SetCurrentGameMode(gamemodeCache);
        }

        /// <summary>
        /// Gets the current game mode string, handling potential WatchableStringSO type.
        /// </summary>
        private static string GetCurrentGameMode()
        {
            try
            {
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
            catch (Exception e)
            {
                Logging.Exception(e);
            }
            return "CASUAL";
        }

        /// <summary>
        /// Sets the current game mode string, handling potential WatchableStringSO type.
        /// </summary>
        private static void SetCurrentGameMode(string gamemode)
        {
            try
            {
                var currentGameModeField = Traverse.Create(GorillaComputer.instance).Field("currentGameMode");
                var value = currentGameModeField.GetValue();
                if (value is string)
                {
                    currentGameModeField.SetValue(gamemode);
                }
                else if (value != null)
                {
                    // If it's a WatchableStringSO, try to set its Value property
                    var valueProperty = Traverse.Create(value).Property("Value");
                    valueProperty.SetValue(gamemode);
                }
            }
            catch (Exception e)
            {
                Logging.Exception(e);
            }
        }
    }
}
