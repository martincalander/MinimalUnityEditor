using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.FolderIcons.Editor
{
    // Maps folder GUIDs to the texture shown as their icon. The map is rebuilt lazily
    // on the first lookup after an invalidation, using one project-wide asset query.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class FolderIconIndex
    {
        internal const string SearchFilter = FolderIconRules.Suffix + " t:Texture2D";

        static readonly Dictionary<string, Texture2D> s_Icons =
            new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        static bool s_Dirty = true;

        internal static bool IsDirty => s_Dirty;
        internal static int BuildCount { get; private set; }
        internal static int Count => s_Icons.Count;

        internal static void Invalidate()
        {
            s_Dirty = true;
        }

        internal static void Clear()
        {
            s_Icons.Clear();
            s_Dirty = true;
        }

        // Hot path: one dictionary lookup without allocations once the index is built.
        internal static bool TryGetIcon(string folderGuid, out Texture2D icon)
        {
            icon = null;
            if (!FolderIconsPreferences.Enabled || string.IsNullOrEmpty(folderGuid))
                return false;
            if (s_Dirty)
                Rebuild();
            if (!s_Icons.TryGetValue(folderGuid, out icon))
                return false;

            // The texture was deleted or unloaded. Rebuild on the next lookup instead of
            // keeping it alive.
            if (icon == null)
            {
                icon = null;
                s_Dirty = true;
                return false;
            }

            return true;
        }

        internal static void Rebuild()
        {
            s_Icons.Clear();
            s_Dirty = false;
            BuildCount++;

            var chosenByFolder = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets(SearchFilter))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!FolderIconRules.IsIconAssetPath(path))
                    continue;

                string folder = FolderIconRules.GetParentFolder(path);
                if (folder == null)
                    continue;

                // The type filter also matches assets that only contain a texture.
                Type mainType = AssetDatabase.GetMainAssetTypeAtPath(path);
                if (mainType == null || !typeof(Texture2D).IsAssignableFrom(mainType))
                    continue;

                chosenByFolder.TryGetValue(folder, out string current);
                if (FolderIconRules.IsPreferred(path, current, FolderIconRules.GetFolderName(folder)))
                    chosenByFolder[folder] = path;
            }

            foreach (KeyValuePair<string, string> entry in chosenByFolder)
            {
                string folderGuid = AssetDatabase.AssetPathToGUID(entry.Key);
                if (string.IsNullOrEmpty(folderGuid))
                    continue;

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(entry.Value);
                if (texture != null)
                    s_Icons[folderGuid] = texture;
            }
        }
    }
}
