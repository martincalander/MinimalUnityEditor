using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MartinCalander.Essentials.MenuItem.Editor
{
    internal static class MenuItemBundleCatalog
    {
        internal static readonly IReadOnlyList<MenuItemBundleDefinition> Bundles = CreateBundles();

        static IReadOnlyList<MenuItemBundleDefinition> CreateBundles()
        {
            var bundles = new List<MenuItemBundleDefinition>();

#if HAS_TMP
            bundles.Add(new MenuItemBundleDefinition(
                "text_mesh_pro",
                "TextMeshPro",
                true,
                true,
                new[]
                {
                    "GameObject/UI/Text - TextMeshPro",
                    "GameObject/3D Object/Text - TextMeshPro",
                    "Component/TextMeshPro/*",
                    "Component/Mesh/TextMeshPro - Text",
                    "Window/TextMeshPro/*",
                    "Window/TextMeshPro",
                    "Windows/TextMeshPro",
                    "Assets/Create/TextMeshPro"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "text_mesh_pro",
                "TextMeshPro",
                false,
                false,
                Array.Empty<string>()));
#endif

            bundles.Add(new MenuItemBundleDefinition(
                "ui_canvas_legacy",
                "UI (Canvas) Legacy",
                true,
                true,
                new[]
                {
                    "Component/UI (Canvas)/Legacy*",
                    "Component/UI/Legacy",
                    "Component/Mesh/Legacy",
                    "GameObject/3D Object/Legacy",
                    "GameObject/UI (Canvas)/Legacy*",
                    "GameObject/UI/Legacy",
                    "Window/Text",
                    "Windows/Text",
                    "Assets/Create/Text Core",
                    "Assets/Create/GUI Skin",
                    "Assets/Create/Custom Font"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "ui_toolkit",
                "UI Toolkit",
                true,
                true,
                new[]
                {
                    "Window/UI Toolkit",
                    "Assets/Create/UI Toolkit",
                    "Component/UI Toolkit/*",
                    "GameObject/UI Toolkit/*"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "jobs",
                "Jobs",
                true,
                true,
                new[]
                {
                    "Jobs/*"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "upm_packaging",
                "UPM Packaging",
                true,
                true,
                new[]
                {
                    "Assets/Create UPM Package...",
                    "Assets/Export As UPM Package...",
                    "Assets/Import New Asset...",
                    "Assets/Import Package",
                    "Assets/View in Package Manager",
                    "Assets/Export As Asset Package..."
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "d3d12_vulkan",
                "D3D12/Vulkan",
                false,
                true,
                new[]
                {
                    // These entries vary by Unity version/pipeline setup. Keep easy to extend.
                    "Edit/Rendering/D3D12*",
                    "Edit/Rendering/Vulkan*",
                    "Assets/Create/D3D12 Settings",
                    "Assets/Create/Vulkan Settings"
                }));

#if HAS_UNITY_TERRAIN
            bundles.Add(new MenuItemBundleDefinition(
                "terrain",
                "Terrain",
                true,
                true,
                new[]
                {
                    "Assets/Create/Terrain"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "terrain",
                "Terrain",
                false,
                false,
                Array.Empty<string>()));
#endif

#if HAS_INPUT_SYSTEM
            bundles.Add(new MenuItemBundleDefinition(
                "xr_options",
                "XR Options",
                true,
                true,
                new[]
                {
                    "Component/XR"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "xr_options",
                "XR Options",
                false,
                false,
                Array.Empty<string>()));
#endif

#if HAS_GLTFAST
            bundles.Add(new MenuItemBundleDefinition(
                "gltf_importer",
                "glTF Importer",
                true,
                true,
                new[]
                {
                    "GameObject/Export glTF",
                    "Assets/Export glTF"
                }));
#else
            bundles.Add(new MenuItemBundleDefinition(
                "gltf_importer",
                "glTF Importer",
                false,
                false,
                Array.Empty<string>()));
#endif

            bundles.Add(new MenuItemBundleDefinition(
                "assets_extra",
                "Assets Extra",
                true,
                true,
                new[]
                {
                    "Assets/Open C# Project",
                    "Assets/View in Import Activity Window",
                    "Assets/Update UXML Schema",
                    "Assets/Extract Material SubAsset"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "assets_dependencies",
                "Assets Dependencies",
                true,
                true,
                new[]
                {
                    "Assets/Find References In Scene",
                    "Assets/Find References In Project",
                    "Assets/Select Dependencies"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "gameobject_extra",
                "GameObject Extra",
                true,
                true,
                new[]
                {
                    "GameObject/Set as first sibling",
                    "GameObject/Set as last sibling",
                    "GameObject/Move To View",
                    "GameObject/Align With View",
                    "GameObject/Align View to Selected",
                    "GameObject/Toggle Active State",
                    "GameObject/Create Empty Child",
                    "GameObject/Create Empty Parent",
                    "GameObject/Center On Children",
                    "GameObject/Make Parent",
                    "GameObject/Clear Parent"
                }));

            bundles.Add(new MenuItemBundleDefinition(
                "version_control",
                "Version Control",
                true,
                true,
                new[]
                {
                    "Window/Version Control"
                }));

            return bundles;
        }

        internal static MenuItemBundleDefinition Find(string bundleId)
        {
            if (string.IsNullOrWhiteSpace(bundleId)) return null;

            for (var i = 0; i < Bundles.Count; i++)
            {
                var bundle = Bundles[i];
                if (string.Equals(bundle.Id, bundleId, StringComparison.OrdinalIgnoreCase))
                {
                    return bundle;
                }
            }

            return null;
        }
    }

    [Serializable]
    internal sealed class BundleState
    {
        [SerializeField] internal string id;
        [SerializeField] internal bool enabled;
    }

    [Serializable]
    internal sealed class MenuItemSettingsData
    {
        [SerializeField] internal bool moduleEnabled = true;
        [SerializeField] internal bool customDefinitionsEnabled;
        [SerializeField] internal string customDefinitionsText = string.Empty;
        [SerializeField] internal List<BundleState> bundles = new List<BundleState>();

        internal bool GetBundleEnabled(string bundleId, bool defaultValue)
        {
            if (string.IsNullOrEmpty(bundleId)) return defaultValue;
            for (var i = 0; i < bundles.Count; i++)
            {
                if (string.Equals(bundles[i].id, bundleId, StringComparison.OrdinalIgnoreCase))
                    return bundles[i].enabled;
            }
            return defaultValue;
        }

        internal void SetBundleEnabled(string bundleId, bool enabled)
        {
            if (string.IsNullOrEmpty(bundleId)) return;
            for (var i = 0; i < bundles.Count; i++)
            {
                if (string.Equals(bundles[i].id, bundleId, StringComparison.OrdinalIgnoreCase))
                {
                    bundles[i].enabled = enabled;
                    return;
                }
            }
            bundles.Add(new BundleState { id = bundleId, enabled = enabled });
        }
    }

    internal static class MenuItemModuleSettings
    {
        const string SettingsPath = "ProjectSettings/MenuItemSettings.json";
        const string AdvancedSettingsExpandedKey = "MartinCalander.Essentials.MenuItem.AdvancedExpanded";

        static MenuItemSettingsData s_data;

        static MenuItemSettingsData Data
        {
            get
            {
                if (s_data == null) Load();
                return s_data;
            }
        }

        internal static bool ModuleEnabled
        {
            get => Data.moduleEnabled;
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

        // UI-only per-user preference — stays in EditorPrefs intentionally.
        internal static bool AdvancedSettingsExpanded
        {
            get => EditorPrefs.GetBool(AdvancedSettingsExpandedKey, false);
            set => EditorPrefs.SetBool(AdvancedSettingsExpandedKey, value);
        }

        internal static bool GetBundleEnabled(string bundleId)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null) return false;

            return Data.GetBundleEnabled(bundle.Id, bundle.DefaultEnabled);
        }

        internal static void SetBundleEnabled(string bundleId, bool enabled)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null) return;

            Data.SetBundleEnabled(bundle.Id, enabled);
            Save();
        }

        internal static IReadOnlyList<string> ResolveBundlePaths()
        {
            var paths = new List<string>();
            for (var i = 0; i < MenuItemBundleCatalog.Bundles.Count; i++)
            {
                var bundle = MenuItemBundleCatalog.Bundles[i];
                if (bundle == null || !bundle.IsAvailable) continue;
                if (!GetBundleEnabled(bundle.Id)) continue;

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
            if (string.IsNullOrWhiteSpace(text)) return entries;

            var lines = text.Split(new[] { '\r', '\n', ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i]?.Trim();
                if (string.IsNullOrEmpty(line)) continue;
                if (line.StartsWith("#", StringComparison.Ordinal)) continue;
                if (line.StartsWith("//", StringComparison.Ordinal)) continue;

                if (line.Length >= 2)
                {
                    var startsWithQuote = line[0] == '"' || line[0] == '\'';
                    var endsWithQuote = line[line.Length - 1] == '"' || line[line.Length - 1] == '\'';
                    if (startsWithQuote && endsWithQuote)
                    {
                        line = line.Substring(1, line.Length - 2).Trim();
                    }
                }

                if (string.IsNullOrEmpty(line)) continue;
                entries.Add(line);
            }
            return entries;
        }

        static string NormalizeDefinitionText(string text)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            return text.Replace("\r\n", "\n").Replace('\r', '\n');
        }

        static void Load()
        {
            s_data = new MenuItemSettingsData();
            try
            {
                if (!File.Exists(SettingsPath)) return;
                var json = File.ReadAllText(SettingsPath);
                if (string.IsNullOrWhiteSpace(json)) return;
                JsonUtility.FromJsonOverwrite(json, s_data);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[MenuItemSettings] Failed to load settings: " + e.Message);
            }
        }

        static void Save()
        {
            if (s_data == null) return;
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

    internal static class MenuItemPreferencesProvider
    {
        static GUIStyle s_customDefinitionsTextAreaStyle;

        [SettingsProvider]
        static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/Menu Item", SettingsScope.User)
            {
                label = "Menu Item",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "menu",
                    "item",
                    "bundle",
                    "advanced",
                    "custom",
                    "upm",
                    "package",
                    "jobs",
                    "legacy",
                    "toolkit",
                    "tmp",
                    "vulkan",
                    "d3d12",
                    "terrain",
                    "xr",
                    "gltf",
                    "dependencies",
                    "version control"
                }
            };
        }

        static void DrawGui(string _)
        {
            EditorGUILayout.LabelField("Module", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginChangeCheck();
                var moduleEnabled = EditorGUILayout.ToggleLeft(
                    "Enable menu item hider",
                    MenuItemModuleSettings.ModuleEnabled,
                    GUILayout.MinWidth(220f));
                if (EditorGUI.EndChangeCheck())
                {
                    MenuItemModuleSettings.ModuleEnabled = moduleEnabled;
                    MenuItemHider.SetActive(moduleEnabled);
                }

                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Apply Now", GUILayout.Width(100f)))
                {
                    MenuItemHider.ApplyWithMenuProxy();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Bundles", EditorStyles.boldLabel);

            for (var i = 0; i < MenuItemBundleCatalog.Bundles.Count; i++)
            {
                var bundle = MenuItemBundleCatalog.Bundles[i];
                if (bundle == null) continue;

                using (new EditorGUI.DisabledScope(!bundle.IsAvailable))
                {
                    EditorGUI.BeginChangeCheck();
                    var enabled = MenuItemModuleSettings.GetBundleEnabled(bundle.Id);
                    var label = bundle.IsAvailable
                        ? bundle.DisplayName
                        : bundle.DisplayName + " (not installed)";
                    var nextEnabled = EditorGUILayout.ToggleLeft(label, enabled);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MenuItemModuleSettings.SetBundleEnabled(bundle.Id, nextEnabled);
                        MenuItemHider.Apply();
                    }
                }
            }

            EditorGUILayout.Space(12f);
            DrawAdvancedSettings();
        }

        static void DrawAdvancedSettings()
        {
            var advancedExpanded = MenuItemModuleSettings.AdvancedSettingsExpanded;
            EditorGUI.BeginChangeCheck();
            advancedExpanded = EditorGUILayout.Foldout(
                advancedExpanded,
                "Advanced Settings",
                true);
            if (EditorGUI.EndChangeCheck())
            {
                MenuItemModuleSettings.AdvancedSettingsExpanded = advancedExpanded;
            }

            if (!advancedExpanded) return;

            using (new EditorGUI.IndentLevelScope())
            {
                using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.LabelField("Custom Path Definitions", EditorStyles.boldLabel);
                    EditorGUILayout.HelpBox(
                        "One path per line or ';' separated. Supports wildcard suffix '*'. Lines starting with # or // are ignored.",
                        MessageType.None);

                    EditorGUI.BeginChangeCheck();
                    var customDefinitionsEnabled = EditorGUILayout.ToggleLeft(
                        "Enable custom text definitions",
                        MenuItemModuleSettings.CustomDefinitionsEnabled);
                    if (EditorGUI.EndChangeCheck())
                    {
                        MenuItemModuleSettings.CustomDefinitionsEnabled = customDefinitionsEnabled;
                        MenuItemHider.Apply();
                    }

                    EditorGUILayout.LabelField(
                        "Detected entries: " + MenuItemModuleSettings.CustomDefinitionEntryCount(),
                        EditorStyles.miniLabel);

                    var customDefinitionsText = MenuItemModuleSettings.CustomDefinitionsText;
                    EditorGUI.BeginChangeCheck();
                    var textAreaRect = GUILayoutUtility.GetRect(
                        GUIContent.none,
                        GetCustomDefinitionsTextAreaStyle(),
                        GUILayout.ExpandWidth(true),
                        GUILayout.MinHeight(180f),
                        GUILayout.MaxHeight(260f));
                    var boundedWidth = Mathf.Min(540f, textAreaRect.width);
                    textAreaRect.width = boundedWidth;
                    customDefinitionsText = EditorGUI.TextArea(
                        textAreaRect,
                        customDefinitionsText,
                        GetCustomDefinitionsTextAreaStyle());
                    if (EditorGUI.EndChangeCheck())
                    {
                        MenuItemModuleSettings.CustomDefinitionsText = customDefinitionsText;
                        if (MenuItemModuleSettings.CustomDefinitionsEnabled)
                        {
                            MenuItemHider.Apply();
                        }
                    }

                    if (MenuItemModuleSettings.CustomDefinitionsContainUnsupportedEntries())
                    {
                        EditorGUILayout.HelpBox(
                            "Services entries are not reliable. Unity regenerates Services menu items internally, so they cannot be hidden consistently.",
                            MessageType.Warning);
                    }
                }
            }
        }

        static GUIStyle GetCustomDefinitionsTextAreaStyle()
        {
            if (s_customDefinitionsTextAreaStyle != null)
            {
                return s_customDefinitionsTextAreaStyle;
            }

            s_customDefinitionsTextAreaStyle = new GUIStyle(EditorStyles.textArea)
            {
                wordWrap = true,
                clipping = TextClipping.Clip
            };
            return s_customDefinitionsTextAreaStyle;
        }
    }
}
