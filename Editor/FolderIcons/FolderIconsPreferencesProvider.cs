using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.FolderIcons.Editor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class FolderIconsPreferencesProvider
    {
        internal const string SettingsPath = "Preferences/Minimal Editor/Folder Icons";

        static readonly GUIContent EnabledContent = new GUIContent(
            "Show Folder Icons",
            "Shows an image whose name ends in Icon as the icon of the folder that contains it.");

        static readonly GUIContent StyleContent = new GUIContent(
            "Icon Style",
            "Full Size replaces the folder icon with the image. Badge keeps the folder icon "
            + "and shows the image small in its bottom-right corner.");

        // Indexes match FolderIconStyle values.
        static readonly GUIContent[] StyleOptions =
        {
            new GUIContent("Full Size"),
            new GUIContent("Badge")
        };

        [SettingsProvider]
        internal static SettingsProvider CreateProvider()
        {
            return new SettingsProvider(SettingsPath, SettingsScope.User)
            {
                label = "Folder Icons",
                guiHandler = DrawGui,
                keywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                {
                    "minimal editor",
                    "folder",
                    "icon",
                    "badge",
                    "project window"
                }
            };
        }

        static void DrawGui(string _)
        {
            EditorGUI.BeginChangeCheck();
            bool enabled = EditorGUILayout.ToggleLeft(EnabledContent, FolderIconsPreferences.Enabled);
            if (EditorGUI.EndChangeCheck())
                FolderIconsPreferences.Enabled = enabled;

            using (new EditorGUI.DisabledScope(!enabled))
            {
                EditorGUI.BeginChangeCheck();
                int style = EditorGUILayout.Popup(
                    StyleContent, (int)FolderIconsPreferences.Style, StyleOptions);
                if (EditorGUI.EndChangeCheck())
                    FolderIconsPreferences.Style = (FolderIconStyle)style;
            }

            EditorGUILayout.HelpBox(
                "A folder uses an image directly inside it whose file name ends in \"Icon\", "
                + "such as Icon.png or CharactersIcon.png.",
                MessageType.None);
        }
    }
}
