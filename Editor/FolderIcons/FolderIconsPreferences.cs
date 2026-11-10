using UnityEditor;

namespace MinimalEditor.FolderIcons.Editor
{
    // How the image is shown on its folder.
    internal enum FolderIconStyle
    {
        // The image replaces the folder icon.
        Full = 0,

        // The folder icon stays and the image is drawn small in its bottom-right corner.
        Badge = 1
    }

    // Keeps the preferences in memory because the Project window reads them once per visible row.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class FolderIconsPreferences
    {
        internal const string EnabledKey = "MinimalEditor.FolderIcons.Enabled";
        internal const bool EnabledDefaultValue = true;
        internal const string StyleKey = "MinimalEditor.FolderIcons.Style";
        internal const FolderIconStyle StyleDefaultValue = FolderIconStyle.Full;

        static bool s_Loaded;
        static bool s_Enabled;
        static FolderIconStyle s_Style;

        internal static bool Enabled
        {
            get
            {
                Load();
                return s_Enabled;
            }
            set
            {
                bool changed = Enabled != value;
                EditorPrefs.SetBool(EnabledKey, value);
                s_Enabled = value;
                if (!changed)
                    return;

                FolderIconIndex.Invalidate();
                EditorApplication.RepaintProjectWindow();
            }
        }

        internal static FolderIconStyle Style
        {
            get
            {
                Load();
                return s_Style;
            }
            set
            {
                if (value != FolderIconStyle.Full && value != FolderIconStyle.Badge)
                    value = StyleDefaultValue;

                bool changed = Style != value;
                EditorPrefs.SetInt(StyleKey, (int)value);
                s_Style = value;
                if (changed)
                    EditorApplication.RepaintProjectWindow();
            }
        }

        // Reads EditorPrefs again on the next access.
        internal static void Reload()
        {
            s_Loaded = false;
        }

        static void Load()
        {
            if (s_Loaded)
                return;

            s_Enabled = EditorPrefs.GetBool(EnabledKey, EnabledDefaultValue);
            int style = EditorPrefs.GetInt(StyleKey, (int)StyleDefaultValue);
            s_Style = style == (int)FolderIconStyle.Badge ? FolderIconStyle.Badge : FolderIconStyle.Full;
            s_Loaded = true;
        }
    }
}
