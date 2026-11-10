using UnityEditor;

namespace MinimalEditor.FolderIcons.Editor
{
    // Keeps the preference in memory because the Project window reads it once per visible row.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class FolderIconsPreferences
    {
        internal const string EnabledKey = "MinimalEditor.FolderIcons.Enabled";
        internal const bool EnabledDefaultValue = true;

        static bool s_Loaded;
        static bool s_Enabled;

        internal static bool Enabled
        {
            get
            {
                if (!s_Loaded)
                {
                    s_Enabled = EditorPrefs.GetBool(EnabledKey, EnabledDefaultValue);
                    s_Loaded = true;
                }

                return s_Enabled;
            }
            set
            {
                bool changed = Enabled != value;
                EditorPrefs.SetBool(EnabledKey, value);
                s_Enabled = value;
                s_Loaded = true;
                if (!changed)
                    return;

                FolderIconIndex.Invalidate();
                EditorApplication.RepaintProjectWindow();
            }
        }

        // Reads EditorPrefs again on the next access.
        internal static void Reload()
        {
            s_Loaded = false;
        }
    }
}
