using System.Diagnostics;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Essentials.Editor
{
    public static class OpenTerminalTool
    {
        [UnityEditor.MenuItem("File/Open Terminal Here")]
        public static void OpenTerminal()
        {
            string projectPath = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../"));
            OpenTerminalAtPath(projectPath);
        }

        [UnityEditor.MenuItem("Assets/Open Terminal Here %&t", false, 0)]
        public static void OpenTerminalAtSelection()
        {
            OpenTerminalAtPath(GetSelectedFolderFullPath());
        }

        static string GetSelectedFolderFullPath()
        {
            string assetPath = GetAssetPathFromProjectBrowser() ?? GetAssetPathFromSelection();

            if (!string.IsNullOrEmpty(assetPath))
            {
                string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", assetPath));
                return AssetDatabase.IsValidFolder(assetPath) ? full : System.IO.Path.GetDirectoryName(full);
            }

            return System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "../"));
        }

        static string GetAssetPathFromProjectBrowser()
        {
            var method = typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            return method?.Invoke(null, null) as string;
        }

        static string GetAssetPathFromSelection()
        {
            if (Selection.assetGUIDs?.Length > 0)
            {
                string path = AssetDatabase.GUIDToAssetPath(Selection.assetGUIDs[0]);
                if (!string.IsNullOrEmpty(path)) return path;
            }
            return AssetDatabase.GetAssetPath(Selection.activeObject);
        }

        static void OpenTerminalAtPath(string path)
        {
#if UNITY_EDITOR_OSX
            Process.Start("open", $"-a Terminal \"{path}\"");
#elif UNITY_EDITOR_WIN
            Process.Start(new ProcessStartInfo
            {
                FileName = "cmd.exe",
                WorkingDirectory = path
            });
#elif UNITY_EDITOR_LINUX
            Process.Start(new ProcessStartInfo
            {
                FileName = "x-terminal-emulator",
                WorkingDirectory = path
            });
#endif
        }
    }
}
