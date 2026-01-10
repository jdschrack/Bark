using Bark.Extensions;
using Bark.Modules;
using Bark.Tools;
using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using static Bark.Extensions.ConfigExtensions;

namespace Bark.GUI
{
    public class SettingsPage : MonoBehaviour
    {
        BarkOptionWheel modSelector, configSelector;
        BarkSlider valueSlider;
        ConfigEntryBase entry;

        void Awake()
        {
            try
            {
                modSelector = transform.Find("Mod Selector").gameObject.AddComponent<BarkOptionWheel>();
                modSelector.InitializeValues(GetModulesWithSettings());

                configSelector = transform.Find("Config Selector").gameObject.AddComponent<BarkOptionWheel>();
                configSelector.InitializeValues(GetConfigKeys(modSelector.Selected));

                valueSlider = transform.Find("Value Slider").gameObject.AddComponent<BarkSlider>();
                entry = GetEntry(modSelector.Selected, configSelector.Selected);
                var info = entry.ValuesInfo();
                valueSlider.InitializeValues(info.AcceptableValues, info.InitialValue);

                modSelector.OnValueChanged += (mod) =>
                {
                    configSelector.InitializeValues(GetConfigKeys(mod));
                };

                configSelector.OnValueChanged += (config) =>
                {
                    entry = GetEntry(modSelector.Selected, configSelector.Selected);
                    UpdateText();
                    var info = entry.ValuesInfo();
                    valueSlider.InitializeValues(info.AcceptableValues, info.InitialValue);
                };

                valueSlider.OnValueChanged += (value) =>
                {
                    entry.BoxedValue = value;
                };

            }
            catch (Exception e) { Logging.Exception(e); }
        }

        ConfigEntryBase GetEntry(string modName, string key)
        {
            foreach (var definition in Plugin.configFile.Keys)
            {
                if (definition.Section == modName && definition.Key == key)
                {
                    return Plugin.configFile[definition];
                }
            }
            throw new Exception($"Could not find config entry for {modName} with key {key}");
        }

        List<string> GetConfigKeys(string modName)
        {
            try
            {
                List<string> configKeys = new List<string>();
                foreach (var definition in Plugin.configFile.Keys)
                {
                    if (definition.Section == modName)
                    {
                        configKeys.Add(Plugin.configFile[definition].Definition.Key);
                    }
                }
                return configKeys;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
                return null;
            }
        }

        List<string> GetModulesWithSettings()
        {
            try
            {
                List<string> modulesWithSettings = new List<string>() { "General" };
                foreach (var type in BarkModule.GetBarkModuleTypes())
                {
                    if (type == typeof(BarkModule)) continue;
                    MethodInfo bindConfigs = type.GetMethod("BindConfigEntries");
                    if (bindConfigs is null) continue;

                    FieldInfo nameField = type.GetField("DisplayName");
                    string displayName = (string)nameField.GetValue(null);
                    modulesWithSettings.Add(displayName);
                }
                return modulesWithSettings;
            }
            catch (Exception e)
            {
                Logging.Exception(e);
                return null;
            }
        }

        public void UpdateText()
        {
            if (entry is null) return;
            MenuController.Instance.helpText.text =
                $"{modSelector.Selected} > {configSelector.Selected}\n" +
                "-----------------------------------\n" +
                entry.Description.Description +
                $"\n\nDefault: {entry.DefaultValue}";
        }
    }
}
