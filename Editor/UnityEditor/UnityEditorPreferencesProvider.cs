using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Essentials.Editor
{
    internal static class UnityEditorPreferencesProvider
    {
        internal const string SettingsPath = "Preferences/Essentials/Unity Editor";

        static readonly GUIContent ShortWindowTitleContent = new GUIContent(
            "Short Window Title",
            "Uses the Player product name as the Unity Editor window title.");

        static readonly GUIContent HideStatusBarExtrasContent = new GUIContent(
            "Hide Status Bar Extras",
            "Hides Unity's animation and memory statistics from the Editor status bar.");

        static readonly GUIContent HierarchySceneAutoSaveContent = new GUIContent(
            "Hierarchy Scene Auto Save",
            "Automatically saves dirty, previously saved scenes after they are edited and the Editor is in a safe state.");

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Unity Editor",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "essentials",
                    "unity",
                    "editor",
                    "window",
                    "title",
                    "status bar",
                    "hierarchy",
                    "scene",
                    "auto save"
                }
            };
        }

        static void DrawGui(string _)
        {
            EditorGUILayout.LabelField("Editor Chrome", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool shortWindowTitle = EditorGUILayout.ToggleLeft(
                ShortWindowTitleContent,
                EditorFeatureToggles.ShortWindowTitle);
            if (EditorGUI.EndChangeCheck())
                EditorFeatureToggles.ShortWindowTitle = shortWindowTitle;

            EditorGUI.BeginChangeCheck();
            bool hideStatusBarExtras = EditorGUILayout.ToggleLeft(
                HideStatusBarExtrasContent,
                EditorFeatureToggles.HideStatusBarExtras);
            if (EditorGUI.EndChangeCheck())
                EditorFeatureToggles.HideStatusBarExtras = hideStatusBarExtras;

            EditorGUILayout.Space(8f);
            EditorGUILayout.LabelField("Scene Editing", EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();
            bool hierarchySceneAutoSave = EditorGUILayout.ToggleLeft(
                HierarchySceneAutoSaveContent,
                HierarchySceneAutoSave.Enabled);
            if (EditorGUI.EndChangeCheck())
                HierarchySceneAutoSave.Enabled = hierarchySceneAutoSave;

            EditorGUILayout.HelpBox(
                "Auto Save ignores unsaved and preview scenes, and waits while Unity is compiling, importing, entering Play Mode, or building a Player.",
                MessageType.Info);
        }
    }
}
