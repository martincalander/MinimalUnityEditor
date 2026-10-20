using System.IO;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Editor
{
    internal static class ResolvePackagesMenuItem
    {
        [UnityEditor.MenuItem("Window/Package Management/Re-resolve All Packages")]
        static void ResolveAllPackages()
        {
            string lockPath = Path.Combine(Application.dataPath, "..", "Packages", "packages-lock.json");
            lockPath = Path.GetFullPath(lockPath);

            if (File.Exists(lockPath))
            {
                File.Delete(lockPath);
                UnityEditor.PackageManager.Client.Resolve();
            }
        }
    }
}
