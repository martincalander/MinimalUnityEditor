using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace MinimalEditor.MenuItem.Editor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class MenuItemModuleSettings
    {
        // User-global like the other Preferences pages. A file is used instead of EditorPrefs because
        // several open Editors overwrote each other's EditorPrefs values.
        static readonly string SettingsPath = Path.Combine(
            InternalEditorUtility.unityPreferencesFolder, "MinimalEditor", "MenuItemSettings.json");

        // Earlier versions stored the settings with the project. They are copied once when no
        // user file exists yet.
        const string LegacyProjectSettingsPath = "ProjectSettings/MenuItemSettings.json";
        const string AdvancedSettingsExpandedKey = "MinimalEditor.MenuItem.AdvancedExpanded";
        const string MinimumSupportedUnity6Version = "6000.0.43f1";

        static readonly bool CurrentEditorSupported = !IsKnownUnsafeEditorVersion(Application.unityVersion);
        static MenuItemSettingsData s_data;
        static DateTime s_loadedWriteTimeUtc;
        static double s_nextDiskCheck;
        const double DiskCheckInterval = 1.0;
        static string s_cachedDefinitionText;
        static List<string> s_cachedDefinitionEntries;
        static IReadOnlyList<string> s_cachedUnsupportedWarnings;
        static IReadOnlyList<string> s_cachedErrorProneWarnings;
        static IReadOnlyList<string> s_cachedMoveWarnings;

        static MenuItemSettingsData Data
        {
            get
            {
                if (s_data == null)
                {
                    Load();
                }
                else
                {
                    // Settings pages redraw often, so reads only look at the file about once a second.
                    var now = EditorApplication.timeSinceStartup;
                    if (now >= s_nextDiskCheck)
                    {
                        s_nextDiskCheck = now + DiskCheckInterval;
                        if (HasChangedOnDisk()) Load();
                    }
                }

                return s_data;
            }
        }

        // Changes always start from the latest saved file.
        static MenuItemSettingsData FreshData
        {
            get
            {
                if (s_data == null || HasChangedOnDisk())
                {
                    Load();
                }

                return s_data;
            }
        }

        internal static bool IsSupportedInCurrentEditor
        {
            get => CurrentEditorSupported;
        }

        internal static string UnsupportedEditorMessage
        {
            get
            {
                if (IsSupportedInCurrentEditor)
                {
                    return string.Empty;
                }

                return
                    "Menu item hiding is disabled in Unity 6.0 editors before " + MinimumSupportedUnity6Version +
                    " because this module relies on internal editor menu APIs that were unstable in those builds. " +
                    "The saved settings are ignored until the project is opened in a newer editor.";
            }
        }

        internal static bool StoredModuleEnabled => Data.moduleEnabled;

        internal static bool ModuleEnabled
        {
            get => IsSupportedInCurrentEditor && Data.moduleEnabled;
            set
            {
                var data = FreshData;
                if (data.moduleEnabled == value) return;
                data.moduleEnabled = value;
                Save();
            }
        }

        internal static bool CustomDefinitionsEnabled
        {
            get => Data.customDefinitionsEnabled;
            set
            {
                var data = FreshData;
                if (data.customDefinitionsEnabled == value) return;
                data.customDefinitionsEnabled = value;
                Save();
            }
        }

        internal static string CustomDefinitionsText
        {
            get => Data.customDefinitionsText ?? string.Empty;
            set
            {
                var normalized = NormalizeDefinitionText(value);
                var data = FreshData;
                if ((data.customDefinitionsText ?? string.Empty) == normalized) return;
                data.customDefinitionsText = normalized;
                Save();
            }
        }

        internal static bool AdvancedSettingsExpanded
        {
            get => EditorPrefs.GetBool(AdvancedSettingsExpandedKey, false);
            set => EditorPrefs.SetBool(AdvancedSettingsExpandedKey, value);
        }

        internal static bool GetBundleEnabled(string bundleId)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null || !bundle.IsAvailable)
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

            var data = FreshData;
            if (data.GetBundleEnabled(bundle.Id, bundle.DefaultEnabled) == enabled) return;
            data.SetBundleEnabled(bundle.Id, enabled);
            Save();
        }

        internal static IReadOnlyList<string> ResolveBundlePaths()
        {
            var paths = new List<string>();
            for (var i = 0; i < MenuItemBundleCatalog.Bundles.Count; i++)
            {
                var bundle = MenuItemBundleCatalog.Bundles[i];
                if (bundle == null || !bundle.IsAvailable || !Data.GetBundleEnabled(bundle.Id, bundle.DefaultEnabled))
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
                var customPaths = GetCustomDefinitionEntries();
                for (var i = 0; i < customPaths.Count; i++)
                {
                    paths.Add(customPaths[i]);
                }
            }

            return MenuItemPatternMatcher.SanitizeEntries(paths);
        }

        internal static bool CustomDefinitionsContainUnsupportedEntries()
        {
            return GetCustomDefinitionWarnings(MenuItemBlockedEntryKind.Unsupported).Count > 0;
        }

        internal static bool CustomDefinitionsContainBlacklistedEntries()
        {
            return GetCustomDefinitionWarnings(MenuItemBlockedEntryKind.ErrorProne).Count > 0;
        }

        internal static int CustomDefinitionEntryCount()
        {
            return GetCustomDefinitionEntries().Count;
        }

        internal static IReadOnlyList<string> GetCustomDefinitionWarnings(MenuItemBlockedEntryKind kind)
        {
            var entries = GetCustomDefinitionEntries();
            if (kind == MenuItemBlockedEntryKind.Unsupported)
                return s_cachedUnsupportedWarnings ?? (s_cachedUnsupportedWarnings =
                    MenuItemPatternMatcher.GetWarningMessages(entries, kind));
            if (kind == MenuItemBlockedEntryKind.ErrorProne)
                return s_cachedErrorProneWarnings ?? (s_cachedErrorProneWarnings =
                    MenuItemPatternMatcher.GetWarningMessages(entries, kind));
            return Array.Empty<string>();
        }

        internal static IReadOnlyList<string> GetCustomDefinitionMoveWarnings()
        {
            var entries = GetCustomDefinitionEntries();
            return s_cachedMoveWarnings ?? (s_cachedMoveWarnings = MenuItemMoveRules.GetWarningMessages(entries));
        }

        static List<string> GetCustomDefinitionEntries()
        {
            var text = CustomDefinitionsText;
            if (s_cachedDefinitionEntries == null || s_cachedDefinitionText != text)
            {
                s_cachedDefinitionText = text;
                s_cachedDefinitionEntries = ParseDefinitionEntries(text);
                s_cachedUnsupportedWarnings = null;
                s_cachedErrorProneWarnings = null;
                s_cachedMoveWarnings = null;
            }

            return s_cachedDefinitionEntries;
        }

        static List<string> ParseDefinitionEntries(string text)
        {
            var entries = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
            {
                return entries;
            }

            var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]?.Trim();
                if (string.IsNullOrEmpty(line) ||
                    line.StartsWith("#", StringComparison.Ordinal) ||
                    line.StartsWith("//", StringComparison.Ordinal))
                {
                    continue;
                }

                // Comments cover the whole line, including any semicolon examples.
                foreach (var definition in line.Split(';'))
                {
                    var entry = definition.Trim();
                    if (string.IsNullOrEmpty(entry) || entry.StartsWith("#", StringComparison.Ordinal) ||
                        entry.StartsWith("//", StringComparison.Ordinal))
                        continue;

                    var startsWithQuote = entry[0] == '"' || entry[0] == '\'';
                    var endsWithQuote = entry[entry.Length - 1] == '"' || entry[entry.Length - 1] == '\'';
                    if (startsWithQuote || endsWithQuote)
                    {
                        if (entry.Length < 2 || !startsWithQuote || entry[0] != entry[entry.Length - 1])
                            continue;
                        entry = entry.Substring(1, entry.Length - 2).Trim();
                    }

                    if (!string.IsNullOrEmpty(entry))
                        entries.Add(entry);
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

        internal static string StoragePath => SettingsPath;

        // Another Editor may have saved since this one loaded. Reading the file again before each
        // change keeps that Editor's choices instead of overwriting them with stale values.
        static bool HasChangedOnDisk()
        {
            try
            {
                return File.Exists(SettingsPath) && File.GetLastWriteTimeUtc(SettingsPath) != s_loadedWriteTimeUtc;
            }
            catch
            {
                return false;
            }
        }

        static void Load()
        {
            s_data = new MenuItemSettingsData();
            try
            {
                if (File.Exists(SettingsPath))
                {
                    s_loadedWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
                    ReadInto(SettingsPath, s_data);
                    return;
                }

                if (File.Exists(LegacyProjectSettingsPath))
                {
                    ReadInto(LegacyProjectSettingsPath, s_data);
                    Save();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuItemSettings] Failed to load settings: " + e.Message);
            }
        }

        static void ReadInto(string path, MenuItemSettingsData data)
        {
            var json = File.ReadAllText(path);
            if (!string.IsNullOrWhiteSpace(json))
            {
                JsonUtility.FromJsonOverwrite(json, data);
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

                // Write beside the target and swap it in so another Editor never reads half a file.
                var temporaryPath = SettingsPath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                if (File.Exists(SettingsPath))
                    File.Replace(temporaryPath, SettingsPath, null);
                else
                    File.Move(temporaryPath, SettingsPath);
                s_loadedWriteTimeUtc = File.GetLastWriteTimeUtc(SettingsPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuItemSettings] Failed to save settings: " + e.Message);
            }
        }

        static bool IsKnownUnsafeEditorVersion(string unityVersion)
        {
            if (!TryParseUnityVersion(unityVersion, out var major, out var minor, out var patch))
            {
                return false;
            }

            if (major != 6000)
            {
                return false;
            }

            if (minor != 0)
            {
                return false;
            }

            return patch < 43;
        }

        static bool TryParseUnityVersion(string unityVersion, out int major, out int minor, out int patch)
        {
            major = 0;
            minor = 0;
            patch = 0;

            if (string.IsNullOrWhiteSpace(unityVersion))
            {
                return false;
            }

            var numericLength = 0;
            while (numericLength < unityVersion.Length)
            {
                var character = unityVersion[numericLength];
                if (!char.IsDigit(character) && character != '.')
                {
                    break;
                }

                numericLength++;
            }

            if (numericLength == 0)
            {
                return false;
            }

            var numericPart = unityVersion.Substring(0, numericLength);
            var parts = numericPart.Split('.');
            if (parts.Length < 3)
            {
                return false;
            }

            return int.TryParse(parts[0], out major) &&
                   int.TryParse(parts[1], out minor) &&
                   int.TryParse(parts[2], out patch);
        }
    }
}
