using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.ProjectFolderVisibility.Editor
{
    [Serializable]
    internal sealed class FolderVisibilityRule
    {
        [SerializeField] string path;
        [SerializeField] bool hidden;

        internal string Path => path;
        internal bool Hidden => hidden;

        internal FolderVisibilityRule(string path, bool hidden)
        {
            this.path = path;
            this.hidden = hidden;
        }

        internal void SetHidden(bool value) => hidden = value;
    }

    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    internal static class FolderVisibilitySettings
    {
        internal const string AddressablesPath = "Assets/AddressableAssetsData";
        internal const string StreamingAssetsPath = "Assets/StreamingAssets";
        internal const string PluginsPath = "Assets/Plugins";
        internal const string GizmosPath = "Assets/Gizmos";
        internal const string SettingsPath = "Assets/Settings";
        internal static readonly string PreferenceKey =
            "com.essentials.projectfoldervisibility.settings." + Hash128.Compute(Application.dataPath);

        [Serializable]
        sealed class Settings
        {
            public bool enabled = true;
            public List<FolderVisibilityRule> folders = new List<FolderVisibilityRule>();
        }

        static Settings s_Settings;
        internal static event Action Changed;

        static FolderVisibilitySettings() => Reload();

        internal static bool Enabled
        {
            get => s_Settings.enabled;
            set
            {
                if (s_Settings.enabled == value)
                    return;
                s_Settings.enabled = value;
                Save();
            }
        }

        internal static IReadOnlyList<FolderVisibilityRule> Folders => s_Settings.folders;

        internal static bool IsBuiltInFolder(string path) =>
            string.Equals(path, AddressablesPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, StreamingAssetsPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, PluginsPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, GizmosPath, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(path, SettingsPath, StringComparison.OrdinalIgnoreCase);

        internal static bool IsFolderHidden(string path) => FindRule(path)?.Hidden ?? false;

        internal static void SetFolderHidden(string path, bool hidden)
        {
            var rule = FindRule(path);
            if (rule == null || rule.Hidden == hidden)
                return;
            rule.SetHidden(hidden);
            Save();
        }

        internal static bool AddFolder(string path)
        {
            path = NormalizeFolder(path);
            if (path == null || FindRule(path) != null)
                return false;
            s_Settings.folders.Add(new FolderVisibilityRule(path, true));
            Save();
            return true;
        }

        internal static bool RemoveFolder(string path)
        {
            var rule = FindRule(path);
            if (rule == null || IsBuiltInFolder(rule.Path))
                return false;
            s_Settings.folders.Remove(rule);
            Save();
            return true;
        }

        internal static bool IsHiddenPath(string path)
        {
            if (!Enabled || string.IsNullOrEmpty(path))
                return false;
            foreach (var rule in s_Settings.folders)
                if (rule.Hidden &&
                    (path.Equals(rule.Path, StringComparison.OrdinalIgnoreCase) ||
                     path.StartsWith(rule.Path + "/", StringComparison.OrdinalIgnoreCase)))
                    return true;
            return false;
        }

        internal static void Reload()
        {
            Settings saved = null;
            if (EditorPrefs.HasKey(PreferenceKey))
            {
                try
                {
                    saved = JsonUtility.FromJson<Settings>(EditorPrefs.GetString(PreferenceKey));
                }
                catch (ArgumentException)
                {
                    // Invalid local preferences fall back to the original folder choices.
                }
            }

            s_Settings = new Settings { enabled = saved?.enabled ?? true };
            var previousShowKey = "com.essentials.projectfoldervisibility.show." + Hash128.Compute(Application.dataPath);
            s_Settings.folders.Add(new FolderVisibilityRule(AddressablesPath, !EditorPrefs.GetBool(previousShowKey, false)));
            s_Settings.folders.Add(new FolderVisibilityRule(StreamingAssetsPath, false));
            s_Settings.folders.Add(new FolderVisibilityRule(PluginsPath, false));
            s_Settings.folders.Add(new FolderVisibilityRule(GizmosPath, false));
            s_Settings.folders.Add(new FolderVisibilityRule(SettingsPath, false));
            if (saved?.folders != null)
            {
                foreach (var rule in saved.folders)
                {
                    var path = NormalizeFolder(rule?.Path);
                    if (path == null)
                        continue;
                    var existing = FindRule(path);
                    if (existing != null)
                        existing.SetHidden(rule.Hidden);
                    else
                        s_Settings.folders.Add(new FolderVisibilityRule(path, rule.Hidden));
                }
            }
            Changed?.Invoke();
        }

        static FolderVisibilityRule FindRule(string path)
        {
            path = NormalizeFolder(path);
            return path == null ? null : s_Settings.folders.Find(rule =>
                rule.Path.Equals(path, StringComparison.OrdinalIgnoreCase));
        }

        static string NormalizeFolder(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return null;
            path = path.Trim().Replace('\\', '/').TrimEnd('/');
            if (!path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                return null;
            var segments = path.Split('/');
            foreach (var segment in segments)
                if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." || segment.IndexOf(':') >= 0)
                    return null;
            return "Assets" + path.Substring("Assets".Length);
        }

        static void Save()
        {
            EditorPrefs.SetString(PreferenceKey, JsonUtility.ToJson(s_Settings));
            Changed?.Invoke();
        }
    }
}
