using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.Console.Editor
{
    internal static class ConsoleLogFilterSettings
    {
        private const string EditorPrefsKey = "Essentials.Console.HiddenLogFilters";

        private static ConsoleLogFilterSettingsData s_data;

        internal static event Action SettingsChanged;

        internal static ConsoleLogFilterSettingsData Data
        {
            get
            {
                EnsureLoaded();
                return s_data;
            }
        }

        internal static ConsoleLogFilterSettingsData Snapshot
        {
            get
            {
                EnsureLoaded();
                return s_data.Clone();
            }
        }

        internal static void Save()
        {
            EnsureLoaded();
            Sanitize(s_data);

            EditorPrefs.SetString(EditorPrefsKey, JsonUtility.ToJson(s_data));
            SettingsChanged?.Invoke();
        }

        private static void EnsureLoaded()
        {
            if (s_data != null)
                return;

            var json = EditorPrefs.GetString(EditorPrefsKey, string.Empty);
            if (string.IsNullOrEmpty(json))
            {
                s_data = new ConsoleLogFilterSettingsData();
                return;
            }

            var hasCleanStackTraceSetting = json.IndexOf("\"cleanStackTraces\"", StringComparison.Ordinal) >= 0;

            try
            {
                s_data = JsonUtility.FromJson<ConsoleLogFilterSettingsData>(json);
            }
            catch
            {
                s_data = new ConsoleLogFilterSettingsData();
            }

            s_data ??= new ConsoleLogFilterSettingsData();
            if (!hasCleanStackTraceSetting)
                s_data.cleanStackTraces = true;

            Sanitize(s_data);
        }

        private static void Sanitize(ConsoleLogFilterSettingsData data)
        {
            data.rules ??= new List<ConsoleLogFilterRuleData>();

            for (var i = data.rules.Count - 1; i >= 0; i--)
            {
                if (data.rules[i] != null)
                    continue;

                data.rules.RemoveAt(i);
            }
        }
    }
}
