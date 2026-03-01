using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.MenuItem.Editor
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

    internal static class MenuItemModuleSettings
    {
        const string ModuleEnabledKey = "Essentials.MenuItem.ModuleEnabled";
        const string BundleEnabledPrefix = "Essentials.MenuItem.Bundle.";
        const string CustomDefinitionsEnabledKey = "Essentials.MenuItem.CustomDefinitionsEnabled";
        const string CustomDefinitionsTextKey = "Essentials.MenuItem.CustomDefinitionsText";
        const string AdvancedSettingsExpandedKey = "Essentials.MenuItem.AdvancedSettingsExpanded";

        internal static bool ModuleEnabled
        {
            get => EditorPrefs.GetBool(ModuleEnabledKey, true);
            set => EditorPrefs.SetBool(ModuleEnabledKey, value);
        }

        internal static bool CustomDefinitionsEnabled
        {
            get => EditorPrefs.GetBool(CustomDefinitionsEnabledKey, false);
            set => EditorPrefs.SetBool(CustomDefinitionsEnabledKey, value);
        }

        internal static string CustomDefinitionsText
        {
            get => EditorPrefs.GetString(CustomDefinitionsTextKey, string.Empty);
            set => EditorPrefs.SetString(CustomDefinitionsTextKey, NormalizeDefinitionText(value));
        }

        internal static bool AdvancedSettingsExpanded
        {
            get => EditorPrefs.GetBool(AdvancedSettingsExpandedKey, false);
            set => EditorPrefs.SetBool(AdvancedSettingsExpandedKey, value);
        }

        internal static bool GetBundleEnabled(string bundleId)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null) return false;

            return EditorPrefs.GetBool(BundleEnabledPrefix + bundle.Id, bundle.DefaultEnabled);
        }

        internal static void SetBundleEnabled(string bundleId, bool enabled)
        {
            var bundle = MenuItemBundleCatalog.Find(bundleId);
            if (bundle == null) return;

            EditorPrefs.SetBool(BundleEnabledPrefix + bundle.Id, enabled);
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
                    "d3d12"
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
                    MenuItemHider.Apply();
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

                    var textAreaWidth = Mathf.Max(120f, EditorGUIUtility.currentViewWidth - 80f);
                    var customDefinitionsText = MenuItemModuleSettings.CustomDefinitionsText;
                    EditorGUI.BeginChangeCheck();
                    customDefinitionsText = EditorGUILayout.TextArea(
                        customDefinitionsText,
                        GetCustomDefinitionsTextAreaStyle(),
                        GUILayout.Width(textAreaWidth),
                        GUILayout.MinHeight(180f));
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
