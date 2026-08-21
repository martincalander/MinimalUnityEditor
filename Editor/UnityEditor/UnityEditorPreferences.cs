using UnityEditor;

namespace Essentials.Editor
{
    internal static class UnityEditorPreferences
    {
        internal const string ShortWindowTitleKey = "Essentials.Editor.WindowTitleShortener.Enabled";
        internal const string HideStatusBarExtrasKey = "Essentials.Editor.StatusBarExtrasHider.Enabled";
        internal const string HierarchySceneAutoSaveKey = "Essentials.Editor.HierarchySceneAutoSave.Enabled";

        internal const bool ShortWindowTitleDefaultValue = true;
        internal const bool HideStatusBarExtrasDefaultValue = true;
        internal const bool HierarchySceneAutoSaveDefaultValue = false;

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

        internal static bool HierarchySceneAutoSave
        {
            get => EditorPrefs.GetBool(HierarchySceneAutoSaveKey, HierarchySceneAutoSaveDefaultValue);
            set => EditorPrefs.SetBool(HierarchySceneAutoSaveKey, value);
        }
    }
}
