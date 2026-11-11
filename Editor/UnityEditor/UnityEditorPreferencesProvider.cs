using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Editor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class UnityEditorPreferencesProvider
    {
        internal const string SettingsPath = "Preferences/Minimal Editor/Unity Editor";

        static readonly GUIContent ShortWindowTitleContent = new GUIContent(
            "Short Window Title",
            "Uses the Player product name as the Unity Editor window title.");

        static readonly GUIContent HideStatusBarExtrasContent = new GUIContent(
            "Hide Status Bar Extras",
            "Hides Unity's animation and memory statistics from the Editor status bar.");

        static readonly GUIContent HideInspectorAssetLabelsContent = new GUIContent(
            "Hide Asset Labels",
            "Hides the Asset Labels and AssetBundle fields at the bottom of asset Inspectors. " +
            "Hidden by default when Addressables is installed.");

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Unity Editor",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "minimal editor",
                    "unity",
                    "editor",
                    "window",
                    "title",
                    "status bar",
                    "asset labels",
                    "assetbundle",
                    "footer"
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

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inspector Footer", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!InspectorAssetLabelsHider.IsSupported))
            {
                EditorGUI.BeginChangeCheck();
                bool hideAssetLabels = EditorGUILayout.ToggleLeft(
                    HideInspectorAssetLabelsContent,
                    EditorFeatureToggles.HideInspectorAssetLabels);
                if (EditorGUI.EndChangeCheck())
                    EditorFeatureToggles.HideInspectorAssetLabels = hideAssetLabels;
            }
        }
    }
}
