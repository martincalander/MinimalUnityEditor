using UnityEditor;

namespace MinimalEditor.Editor
{
    internal static class UnityEditorPreferences
    {
        internal const string ShortWindowTitleKey = "MinimalEditor.Editor.WindowTitleShortener.Enabled";
        internal const string HideStatusBarExtrasKey = "MinimalEditor.Editor.StatusBarExtrasHider.Enabled";
        internal const string HideInspectorAssetLabelsKey = "MinimalEditor.Editor.InspectorAssetLabelsHider.Enabled";

        internal const bool ShortWindowTitleDefaultValue = true;
        internal const bool HideStatusBarExtrasDefaultValue = true;
        // Addressables replaces AssetBundle names, so the footer is hidden by default alongside it.
#if MINIMAL_EDITOR_ADDRESSABLES
        internal const bool HideInspectorAssetLabelsDefaultValue = true;
#else
        internal const bool HideInspectorAssetLabelsDefaultValue = false;
#endif

        internal static bool ShortWindowTitle
        {
            get => EditorPrefs.GetBool(ShortWindowTitleKey, ShortWindowTitleDefaultValue);
            set => EditorPrefs.SetBool(ShortWindowTitleKey, value);
        }

        internal static bool HideStatusBarExtras
        {
            get => EditorPrefs.GetBool(HideStatusBarExtrasKey, HideStatusBarExtrasDefaultValue);
            set => EditorPrefs.SetBool(HideStatusBarExtrasKey, value);
        }

        internal static bool HideInspectorAssetLabels
        {
            get => EditorPrefs.GetBool(HideInspectorAssetLabelsKey, HideInspectorAssetLabelsDefaultValue);
            set => EditorPrefs.SetBool(HideInspectorAssetLabelsKey, value);
        }
    }
}
