using BepInEx.Configuration;
using UnityEngine;
using System;

namespace Bark.Extensions
{
    public static class ConfigExtensions
    {
        public struct ConfigValueInfo
        {
            public object[] AcceptableValues;
            public int InitialValue;
        }

        public static ConfigValueInfo ValuesInfo(this ConfigEntryBase entry)
        {
            if (entry.SettingType == typeof(bool))
            {
                return new ConfigValueInfo
                {
                    AcceptableValues = new object[] { false, true },
                    InitialValue = (bool)entry.BoxedValue ? 1 : 0
                };
            }
            else if (entry.SettingType == typeof(int))
            {
                return new ConfigValueInfo
                {
                    AcceptableValues = new object[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 },
                    InitialValue = (int)Mathf.Clamp((int)entry.BoxedValue, 0, 10)
                };
            }
            else if (entry.SettingType == typeof(string))
            {
                if (entry.Description.AcceptableValues is AcceptableValueList<string> acceptableValueList)
                {
                    var acceptableValues = acceptableValueList.AcceptableValues;
                    for (int i = 0; i < acceptableValues.Length; i++)
                    {
                        if (acceptableValues[i] == (string)entry.BoxedValue)
                            return new ConfigValueInfo
                            {
                                AcceptableValues = acceptableValues,
                                InitialValue = i
                            };
                    }
                }
                // String config without AcceptableValueList - return just the current value
                return new ConfigValueInfo
                {
                    AcceptableValues = new object[] { entry.BoxedValue },
                    InitialValue = 0
                };
            }
            throw new Exception($"Unknown config type {entry.SettingType}");
        }
    }
}
