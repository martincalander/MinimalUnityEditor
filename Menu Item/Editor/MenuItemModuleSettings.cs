using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MartinCalander.Essentials.MenuItem.Editor
{
    internal static class MenuItemModuleSettings
    {
        const string SettingsPath = "ProjectSettings/MenuItemSettings.json";
        const string AdvancedSettingsExpandedKey = "MartinCalander.Essentials.MenuItem.AdvancedExpanded";

        static MenuItemSettingsData s_data;

        static MenuItemSettingsData Data
        {
            get
            {
                if (s_data == null)
                {
                    Load();
                }

                return s_data;
            }
        }

        internal static bool IsSupportedInCurrentEditor
        {
            get
            {
#if UNITY_6000_0_OR_NEWER
                return false;
#else
                return true;
#endif
            }
        }

        internal static bool StoredModuleEnabled => Data.moduleEnabled;

        internal static bool ModuleEnabled
        {
            get => IsSupportedInCurrentEditor && Data.moduleEnabled;
            set { Data.moduleEnabled = value; Save(); }
        }

        internal static bool CustomDefinitionsEnabled
        {
            get => Data.customDefinitionsEnabled;
            set { Data.customDefinitionsEnabled = value; Save(); }
        }

        internal static string CustomDefinitionsText
        {
            get => Data.customDefinitionsText ?? string.Empty;
            set { Data.customDefinitionsText = NormalizeDefinitionText(value); Save(); }
        }

        internal static bool AdvancedSettingsExpanded
        {
            get => EditorPrefs.GetBool(AdvancedSettingsExpandedKey, false);
            set => EditorPrefs.SetBool(AdvancedSettingsExpandedKey, value);
        }

        internal static bool GetBundleEnabled(string bundleId)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null)
            {
                return false;
            }

            return Data.GetBundleEnabled(bundle.Id, bundle.DefaultEnabled);
        }

        internal static void SetBundleEnabled(string bundleId, bool enabled)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null)
            {
                return;
            }

            Data.SetBundleEnabled(bundle.Id, enabled);
            Save();
        }

        internal static IReadOnlyList<string> ResolveBundlePaths()
        {
            var paths = new List<string>();
            for (var i = 0; i < MenuItemBundleCatalog.Bundles.Count; i++)
            {
                var bundle = MenuItemBundleCatalog.Bundles[i];
                if (bundle == null || !bundle.IsAvailable || !GetBundleEnabled(bundle.Id))
                {
                    continue;
                }

                for (var p = 0; p < bundle.MenuPaths.Count; p++)
                {
                    paths.Add(bundle.MenuPaths[p]);
                }
            }

            if (CustomDefinitionsEnabled)
            {
                var customPaths = ResolveCustomDefinitionPaths();
                for (var i = 0; i < customPaths.Count; i++)
                {
                    paths.Add(customPaths[i]);
                }
            }

            return MenuItemPatternMatcher.SanitizeEntries(paths);
        }

        internal static bool CustomDefinitionsContainUnsupportedEntries()
        {
            var entries = ParseDefinitionEntries(CustomDefinitionsText);
            for (var i = 0; i < entries.Count; i++)
            {
                if (MenuItemPatternMatcher.IsUnsupportedEntry(entries[i]))
                {
                    return true;
                }
            }

            return false;
        }

        internal static int CustomDefinitionEntryCount()
        {
            return ParseDefinitionEntries(CustomDefinitionsText).Count;
        }

        static IReadOnlyList<string> ResolveCustomDefinitionPaths()
        {
            return MenuItemPatternMatcher.SanitizeEntries(ParseDefinitionEntries(CustomDefinitionsText));
        }

        static List<string> ParseDefinitionEntries(string text)
        {
            var entries = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return entries;
            }

            var lines = text.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]?.Trim();
                if (string.IsNullOrEmpty(line) ||
                    line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                if (line.Length >= 2)
                {
                    var startsWithQuote = line[0] == '"' || line[0] == '\'';
                    var endsWithQuote = line[line.Length - 1] == '"' || line[line.Length - 1] == '\'';
                    if (startsWithQuote && endsWithQuote)
                    {
                        line = line.Substring(1, line.Length - 2).Trim();
                    }
                }

                if (!string.IsNullOrEmpty(line))
                {
                    entries.Add(line);
                }
            }

            return entries;
        }

        static string NormalizeDefinitionText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static void Load()
        {
            s_data = new MenuItemSettingsData();
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    return;
                }

                var json = File.ReadAllText(SettingsPath);
                if (string.IsNullOrWhiteSpace(json))
                {
                    return;
                }

                JsonUtility.FromJsonOverwrite(json, s_data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuItemSettings] Failed to load settings: " + e.Message);
            }
        }

        static void Save()
        {
            if (s_data == null)
            {
                return;
            }

            try
            {
                var json = JsonUtility.ToJson(s_data, true);
                var directory = Path.GetDirectoryName(SettingsPath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.WriteAllText(SettingsPath, json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuItemSettings] Failed to save settings: " + e.Message);
            }
        }
    }
}
