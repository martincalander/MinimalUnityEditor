using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.MenuItem.Editor
{
    internal static class MenuItemPreferencesProvider
    {
        internal const string SettingsPath = "Project/Essentials/Menu Item";

        static GUIStyle s_customDefinitionsTextAreaStyle;

        [SettingsProvider]
        static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.Project)
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
            var menuItemHidingSupported = MenuItemModuleSettings.IsSupportedInCurrentEditor;

            if (!menuItemHidingSupported)
            {
                EditorGUILayout.HelpBox(
                    MenuItemModuleSettings.UnsupportedEditorMessage,
                    MessageType.Warning);
                EditorGUILayout.Space(8f);
            }

            EditorGUILayout.LabelField("Module", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!menuItemHidingSupported))
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

            using (new EditorGUI.DisabledScope(!menuItemHidingSupported))
            {
                for (var i = 0; i < MenuItemBundleCatalog.Bundles.Count; i++)
                {
                    var bundle = MenuItemBundleCatalog.Bundles[i];
                    if (bundle == null)
                    {
                        continue;
                    }

                    using (new EditorGUI.DisabledScope(!bundle.IsAvailable || !menuItemHidingSupported))
                    {
                        EditorGUI.BeginChangeCheck();
                        var enabled = MenuItemModuleSettings.GetBundleEnabled(bundle.Id);
                        var label = bundle.IsAvailable
                            ? bundle.DisplayName
                            : bundle.DisplayName + " (not installed)";
                        var labelContent = new GUIContent(label, BuildBundleTooltip(bundle));
                        var nextEnabled = EditorGUILayout.ToggleLeft(labelContent, enabled);
                        if (EditorGUI.EndChangeCheck())
                        {
                            MenuItemModuleSettings.SetBundleEnabled(bundle.Id, nextEnabled);
                            MenuItemHider.Apply();
                        }
                    }
                }
            }

            EditorGUILayout.Space(12f);
            using (new EditorGUI.DisabledScope(!menuItemHidingSupported))
            {
                DrawAdvancedSettings();
            }
        }

        static string BuildBundleTooltip(MenuItemBundleDefinition bundle)
        {
            if (bundle == null)
            {
                return string.Empty;
            }

            if (bundle.MenuPaths == null || bundle.MenuPaths.Count == 0)
            {
                return "No menu item entries in this bundle.";
            }

            return string.Join("\n", bundle.MenuPaths);
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

            if (!advancedExpanded)
            {
                return;
            }

            using (new EditorGUI.IndentLevelScope())
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
                textAreaRect.width = Mathf.Min(540f, textAreaRect.width);
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

                DrawCustomDefinitionWarnings(MenuItemBlockedEntryKind.Unsupported);
                DrawCustomDefinitionWarnings(MenuItemBlockedEntryKind.ErrorProne);
            }
        }

        static void DrawCustomDefinitionWarnings(MenuItemBlockedEntryKind kind)
        {
            var warnings = MenuItemModuleSettings.GetCustomDefinitionWarnings(kind);
            for (var i = 0; i < warnings.Count; i++)
            {
                if (string.IsNullOrWhiteSpace(warnings[i])) continue;
                EditorGUILayout.HelpBox(warnings[i], MessageType.Warning);
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
