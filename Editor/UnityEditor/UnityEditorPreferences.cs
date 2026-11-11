using UnityEditor;

namespace MinimalEditor.Editor
{
    internal static class UnityEditorPreferences
    {
        internal const string ShortWindowTitleKey = "MinimalEditor.Editor.WindowTitleShortener.Enabled";
        internal const string HideStatusBarExtrasKey = "MinimalEditor.Editor.StatusBarExtrasHider.Enabled";
        internal const string HideInspectorHelpButtonKey = "MinimalEditor.Editor.InspectorHelpButtonHider.Enabled";
        internal const string HideInspectorPresetButtonKey = "MinimalEditor.Editor.InspectorPresetButtonHider.Enabled";
        internal const string HideInspectorAssetLabelsKey = "MinimalEditor.Editor.InspectorAssetLabelsHider.Enabled";
        internal const string HideAssetPostprocessorsKey = "MinimalEditor.Editor.AssetPostprocessorsHider.Enabled";
        internal const string HideStatusBarDebuggerKey = "MinimalEditor.Editor.StatusBarDebuggerHider.Enabled";
        internal const string QuitAutomatedEditorKey = "MinimalEditor.Editor.AutomatedEditorQuit.Enabled";

        internal const bool QuitAutomatedEditorDefaultValue = true;
        internal const bool ShortWindowTitleDefaultValue = true;
        internal const bool HideStatusBarExtrasDefaultValue = true;
        internal const bool HideInspectorHelpButtonDefaultValue = false;
        internal const bool HideInspectorPresetButtonDefaultValue = false;
        // Addressables replaces AssetBundle names, so the footer is hidden by default alongside it.
#if MINIMAL_EDITOR_ADDRESSABLES
        internal const bool HideInspectorAssetLabelsDefaultValue = true;
#else
        internal const bool HideInspectorAssetLabelsDefaultValue = false;
#endif

        internal const bool HideAssetPostprocessorsDefaultValue = false;
        internal const bool HideStatusBarDebuggerDefaultValue = false;

        internal static bool ShortWindowTitle
        {
            get => EditorPrefs.GetBool(ShortWindowTitleKey, ShortWindowTitleDefaultValue);
            set => EditorPrefs.SetBool(ShortWindowTitleKey, value);
        }

        internal static bool QuitAutomatedEditor
        {
            get => EditorPrefs.GetBool(QuitAutomatedEditorKey, QuitAutomatedEditorDefaultValue);
            set => EditorPrefs.SetBool(QuitAutomatedEditorKey, value);
        }

        internal static bool HideStatusBarExtras
        {
            get => EditorPrefs.GetBool(HideStatusBarExtrasKey, HideStatusBarExtrasDefaultValue);
            set => EditorPrefs.SetBool(HideStatusBarExtrasKey, value);
        }

        internal static bool HideInspectorHelpButton
        {
            get => EditorPrefs.GetBool(HideInspectorHelpButtonKey, HideInspectorHelpButtonDefaultValue);
            set => EditorPrefs.SetBool(HideInspectorHelpButtonKey, value);
        }

        internal static bool HideInspectorPresetButton
        {
            get => EditorPrefs.GetBool(HideInspectorPresetButtonKey, HideInspectorPresetButtonDefaultValue);
            set => EditorPrefs.SetBool(HideInspectorPresetButtonKey, value);
        }

        internal static bool HideInspectorAssetLabels
        {
            get => EditorPrefs.GetBool(HideInspectorAssetLabelsKey, HideInspectorAssetLabelsDefaultValue);
            set => EditorPrefs.SetBool(HideInspectorAssetLabelsKey, value);
        }

        internal static bool HideAssetPostprocessors
        {
            get => EditorPrefs.GetBool(HideAssetPostprocessorsKey, HideAssetPostprocessorsDefaultValue);
            set => EditorPrefs.SetBool(HideAssetPostprocessorsKey, value);
        }

        internal static bool HideStatusBarDebugger
        {
            get => EditorPrefs.GetBool(HideStatusBarDebuggerKey, HideStatusBarDebuggerDefaultValue);
            set => EditorPrefs.SetBool(HideStatusBarDebuggerKey, value);
        }
    }
}
