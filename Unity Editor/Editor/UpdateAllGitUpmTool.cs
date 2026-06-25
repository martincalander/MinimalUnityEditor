using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace Essentials.Editor
{
    public static class UpdateAllGitUpmTool
    {
        const string MenuPath = "Window/Package Management/Update All Git UPM";

        static readonly Regex DependencyEntryRegex = new Regex("\"(?<name>[^\"]+)\"\\s*:\\s*\"(?<value>[^\"]+)\"", RegexOptions.Compiled);
        static readonly Queue<GitDependency> PendingPackages = new Queue<GitDependency>();

        static AddRequest s_currentRequest;
        static GitDependency s_currentPackage;
        static int s_totalPackages;
        static int s_completedPackages;
        static bool s_isUpdating;

        [UnityEditor.MenuItem(MenuPath, priority = 3200)]
        static void UpdateAllGitUpm()
        {
            if (s_isUpdating)
            {
                Debug.LogWarning("[Update All Git UPM] An update is already in progress.");
                return;
            }

            if (!TryLoadGitDependencies(out var packages, out var error))
            {
                Debug.LogError($"[Update All Git UPM] {error}");
                return;
            }

            if (packages.Count == 0)
            {
                Debug.Log("[Update All Git UPM] No git dependencies were found in Packages/manifest.json.");
                return;
            }

            PendingPackages.Clear();
            foreach (var package in packages)
            {
                PendingPackages.Enqueue(package);
            }

            s_totalPackages = packages.Count;
            s_completedPackages = 0;
            s_isUpdating = true;

            Debug.Log($"[Update All Git UPM] Updating {s_totalPackages} git package(s).");

            EditorApplication.update -= ProcessCurrentRequest;
            EditorApplication.update += ProcessCurrentRequest;

            StartNextRequest();
        }

        [UnityEditor.MenuItem(MenuPath, true)]
        static bool ValidateUpdateAllGitUpm()
        {
            return !s_isUpdating;
        }

        static void StartNextRequest()
        {
            if (PendingPackages.Count == 0)
            {
                FinishUpdate(success: true, message: $"Updated {s_completedPackages} git package(s).");
                return;
            }

            s_currentPackage = PendingPackages.Dequeue();

            ShowProgressBar();
            Debug.Log($"[Update All Git UPM] Updating {s_currentPackage.Name} from {s_currentPackage.Identifier}");

            s_currentRequest = Client.Add(s_currentPackage.Identifier);
        }

        static void ProcessCurrentRequest()
        {
            if (!s_isUpdating || s_currentRequest == null)
            {
                return;
            }

            ShowProgressBar();

            if (!s_currentRequest.IsCompleted)
            {
                return;
            }

            if (s_currentRequest.Error != null)
            {
                FinishUpdate(success: false, message: $"Failed to update {s_currentPackage.Name}: {s_currentRequest.Error.message}");
                return;
            }

            s_completedPackages++;
            s_currentRequest = null;
            StartNextRequest();
        }

        static void FinishUpdate(bool success, string message)
        {
            EditorApplication.update -= ProcessCurrentRequest;
            EditorUtility.ClearProgressBar();

            PendingPackages.Clear();
            s_currentRequest = null;
            s_currentPackage = default;
            s_totalPackages = 0;
            s_completedPackages = 0;
            s_isUpdating = false;

            if (success)
            {
                Debug.Log($"[Update All Git UPM] {message}");
            }
            else
            {
                Debug.LogError($"[Update All Git UPM] {message}");
            }
        }

        static void ShowProgressBar()
        {
            float progress = s_totalPackages <= 0
                ? 0f
                : (float)s_completedPackages / s_totalPackages;

            string label = s_currentPackage.Name ?? "Resolving packages...";
            EditorUtility.DisplayProgressBar("Update All Git UPM", label, progress);
        }

        static bool TryLoadGitDependencies(out List<GitDependency> packages, out string error)
        {
            packages = new List<GitDependency>();
            error = null;

            string manifestPath = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Packages", "manifest.json"));
            if (!File.Exists(manifestPath))
            {
                error = $"Could not find manifest at {manifestPath}.";
                return false;
            }

            string json = File.ReadAllText(manifestPath);
            if (!TryExtractObject(json, "dependencies", out var dependenciesJson))
            {
                error = "Could not read the dependencies object from Packages/manifest.json.";
                return false;
            }

            foreach (Match match in DependencyEntryRegex.Matches(dependenciesJson))
            {
                string packageName = match.Groups["name"].Value;
                string identifier = match.Groups["value"].Value;

                if (!LooksLikeGitDependency(identifier))
                {
                    continue;
                }

                packages.Add(new GitDependency(packageName, identifier));
            }

            return true;
        }

        static bool TryExtractObject(string json, string propertyName, out string objectBody)
        {
            objectBody = null;

            string token = $"\"{propertyName}\"";
            int propertyIndex = json.IndexOf(token, StringComparison.Ordinal);
            if (propertyIndex < 0)
            {
                return false;
            }

            int objectStart = json.IndexOf('{', propertyIndex);
            if (objectStart < 0)
            {
                return false;
            }

            int depth = 0;
            bool inString = false;
            bool escaping = false;

            for (int i = objectStart; i < json.Length; i++)
            {
                char current = json[i];

                if (inString)
                {
                    if (escaping)
                    {
                        escaping = false;
                    }
                    else if (current == '\\')
                    {
                        escaping = true;
                    }
                    else if (current == '"')
                    {
                        inString = false;
                    }

                    continue;
                }

                if (current == '"')
                {
                    inString = true;
                    continue;
                }

                if (current == '{')
                {
                    depth++;
                    continue;
                }

                if (current != '}')
                {
                    continue;
                }

                depth--;
                if (depth != 0)
                {
                    continue;
                }

                objectBody = json.Substring(objectStart + 1, i - objectStart - 1);
                return true;
            }

            return false;
        }

        static bool LooksLikeGitDependency(string identifier)
        {
            if (identifier.StartsWith("git+", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (identifier.StartsWith("git@", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (identifier.EndsWith(".git", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return identifier.IndexOf("://", StringComparison.Ordinal) >= 0;
        }

        readonly struct GitDependency
        {
            public GitDependency(string name, string identifier)
            {
                Name = name;
                Identifier = identifier;
            }

            public string Name { get; }

            public string Identifier { get; }
        }
    }
}
