using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MartinCalander.Essentials.MenuItem.Editor
{
    internal static class MenuItemPreferencesProvider
    {
        const string CustomDefinitionsControlName = "MartinCalander.Essentials.MenuItem.CustomDefinitions";

        static GUIStyle s_customDefinitionsTextAreaStyle;
        static string s_customDefinitionsDraft;
        static bool s_customDefinitionsDraftDirty;
        static bool s_wasEditingCustomDefinitions;

        [SettingsProvider]
        static SettingsProvider CreateProvider()
        {
            return new SettingsProvider("Preferences/Menu Item", SettingsScope.User)
            {
                label = "Menu Item",
                activateHandler = (_, _) => ResetCustomDefinitionsDraft(),
                deactivateHandler = () => CommitPendingCustomDefinitions(),
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
                    CommitPendingCustomDefinitions();
                    MenuItemHider.ApplyWithMenuProxy();
                }
            }

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Bundles", EditorStyles.boldLabel);

            for (var i = 0; i < MenuItemBundleCatalog.Bundles.Count; i++)
            {
                var bundle = MenuItemBundleCatalog.Bundles[i];
                if (bundle == null)
                {
                    continue;
                }

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
                    CommitPendingCustomDefinitions(applyChanges: false);
                    MenuItemModuleSettings.CustomDefinitionsEnabled = customDefinitionsEnabled;
                    MenuItemHider.Apply();
                }

                EnsureCustomDefinitionsDraft();
                EditorGUILayout.LabelField(
                    "Detected entries: " + MenuItemModuleSettings.CustomDefinitionEntryCount(s_customDefinitionsDraft),
                    EditorStyles.miniLabel);

                var customDefinitionsText = s_customDefinitionsDraft;
                EditorGUI.BeginChangeCheck();
                GUI.SetNextControlName(CustomDefinitionsControlName);
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
                    s_customDefinitionsDraft = customDefinitionsText;
                    s_customDefinitionsDraftDirty = true;
                }

                var isEditingCustomDefinitions = string.Equals(
                    GUI.GetNameOfFocusedControl(),
                    CustomDefinitionsControlName,
                    StringComparison.Ordinal);
                if (s_wasEditingCustomDefinitions && !isEditingCustomDefinitions)
                {
                    CommitPendingCustomDefinitions();
                }

                s_wasEditingCustomDefinitions = isEditingCustomDefinitions;

                if (MenuItemModuleSettings.CustomDefinitionsContainUnsupportedEntries(s_customDefinitionsDraft))
                {
                    EditorGUILayout.HelpBox(
                        "Services entries are not reliable. Unity regenerates Services menu items internally, so they cannot be hidden consistently.",
                        MessageType.Warning);
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

        static void EnsureCustomDefinitionsDraft()
        {
            if (s_customDefinitionsDraft != null)
            {
                return;
            }

            ResetCustomDefinitionsDraft();
        }

        static void ResetCustomDefinitionsDraft()
        {
            s_customDefinitionsDraft = MenuItemModuleSettings.CustomDefinitionsText;
            s_customDefinitionsDraftDirty = false;
            s_wasEditingCustomDefinitions = false;
        }

        static void CommitPendingCustomDefinitions(bool applyChanges = true)
        {
            EnsureCustomDefinitionsDraft();
            if (!s_customDefinitionsDraftDirty)
            {
                return;
            }

            MenuItemModuleSettings.CustomDefinitionsText = s_customDefinitionsDraft;
            s_customDefinitionsDraft = MenuItemModuleSettings.CustomDefinitionsText;
            s_customDefinitionsDraftDirty = false;

            if (applyChanges && MenuItemModuleSettings.CustomDefinitionsEnabled)
            {
                MenuItemHider.Apply();
            }
        }
    }
}
