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

        static readonly GUIContent HideInspectorHelpButtonContent = new GUIContent(
            "Hide Help Button",
            "Hides the ? Open Reference button from Inspector headers.");

        static readonly GUIContent HideInspectorPresetButtonContent = new GUIContent(
            "Hide Preset Button",
            "Hides the preset button from Inspector headers.");

        static readonly GUIContent HideInspectorAssetLabelsContent = new GUIContent(
            "Hide Asset Labels",
            "Hides the Asset Labels and AssetBundle fields at the bottom of asset Inspectors. " +
            "Hidden by default when Addressables is installed.");

        static readonly GUIContent QuitAutomatedEditorContent = new GUIContent(
            "Quit Automated Editor",
            "Lets Command+Q and the window close button quit an Editor launched with -automated. " +
            "Saved scenes are saved again and untitled scenes are dropped. Unity 7 and newer.");

        static readonly GUIContent HideAssetPostprocessorsContent = new GUIContent(
            "Hide Asset PostProcessors",
            "Hides the Asset PostProcessors list at the bottom of importer Inspectors.");

        static readonly GUIContent HideStatusBarDebuggerContent = new GUIContent(
            "Hide Debugger Button",
            "Hides the debug mode button from the Editor status bar.");

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
                    "inspector",
                    "help",
                    "reference",
                    "preset",
                    "asset labels",
                    "assetbundle",
                    "footer",
                    "quit",
                    "automated",
                    "postprocessors",
                    "debugger"
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

            using (new EditorGUI.DisabledScope(!EditorElementHider.IsSupported(EditorElementHider.Element.StatusBarDebugger)))
            {
                EditorGUI.BeginChangeCheck();
                bool hideDebugger = EditorGUILayout.ToggleLeft(
                    HideStatusBarDebuggerContent,
                    EditorFeatureToggles.HideStatusBarDebugger);
                if (EditorGUI.EndChangeCheck())
                    EditorFeatureToggles.HideStatusBarDebugger = hideDebugger;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Inspector Header", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!InspectorHeaderButtonsHider.IsButtonSupported(InspectorHeaderButtonsHider.Button.Help)))
            {
                EditorGUI.BeginChangeCheck();
                bool hideHelp = EditorGUILayout.ToggleLeft(
                    HideInspectorHelpButtonContent,
                    EditorFeatureToggles.HideInspectorHelpButton);
                if (EditorGUI.EndChangeCheck())
                    EditorFeatureToggles.HideInspectorHelpButton = hideHelp;
            }

            using (new EditorGUI.DisabledScope(!InspectorHeaderButtonsHider.IsButtonSupported(InspectorHeaderButtonsHider.Button.Preset)))
            {
                EditorGUI.BeginChangeCheck();
                bool hidePreset = EditorGUILayout.ToggleLeft(
                    HideInspectorPresetButtonContent,
                    EditorFeatureToggles.HideInspectorPresetButton);
                if (EditorGUI.EndChangeCheck())
                    EditorFeatureToggles.HideInspectorPresetButton = hidePreset;
            }

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

            using (new EditorGUI.DisabledScope(!EditorElementHider.IsSupported(EditorElementHider.Element.AssetPostprocessors)))
            {
                EditorGUI.BeginChangeCheck();
                bool hidePostprocessors = EditorGUILayout.ToggleLeft(
                    HideAssetPostprocessorsContent,
                    EditorFeatureToggles.HideAssetPostprocessors);
                if (EditorGUI.EndChangeCheck())
                    EditorFeatureToggles.HideAssetPostprocessors = hidePostprocessors;
            }

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Automated Editor", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(!AutomatedEditorQuit.IsSupported))
            {
                EditorGUI.BeginChangeCheck();
                bool quitAutomatedEditor = EditorGUILayout.ToggleLeft(
                    QuitAutomatedEditorContent,
                    EditorFeatureToggles.QuitAutomatedEditor);
                if (EditorGUI.EndChangeCheck())
                    EditorFeatureToggles.QuitAutomatedEditor = quitAutomatedEditor;
            }
        }
    }
}
