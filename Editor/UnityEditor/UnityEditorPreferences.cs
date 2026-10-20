using UnityEditor;

namespace MinimalEditor.Editor
{
    internal static class UnityEditorPreferences
    {
        internal const string ShortWindowTitleKey = "MinimalEditor.Editor.WindowTitleShortener.Enabled";
        internal const string HideStatusBarExtrasKey = "MinimalEditor.Editor.StatusBarExtrasHider.Enabled";

        internal const bool ShortWindowTitleDefaultValue = true;
        internal const bool HideStatusBarExtrasDefaultValue = true;

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

    }
}
