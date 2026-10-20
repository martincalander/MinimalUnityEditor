using System;
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

            ResolveAllPackages(lockPath, () => UnityEditor.PackageManager.Client.Resolve());
        }

        internal static void ResolveAllPackages(string lockPath, Action resolve)
        {
            // File.Delete already tolerates a missing lockfile, including projects that disable it.
            File.Delete(lockPath);
            resolve();
        }
    }
}
