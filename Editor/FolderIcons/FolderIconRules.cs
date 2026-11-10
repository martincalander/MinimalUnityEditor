using System;
using System.Collections.Generic;

namespace MinimalEditor.FolderIcons.Editor
{
    // Pure path rules for folder icons. They take asset paths with forward slashes
    // and never touch the AssetDatabase, so they can be tested without assets.
    internal static class FolderIconRules
    {
        internal const string Suffix = "Icon";

        internal const int ExactIconRank = 0;
        internal const int FolderNameIconRank = 1;
        internal const int OtherIconRank = 2;
        internal const int NotAnIconRank = int.MaxValue;

        // True when the file name without extension ends with "Icon", ignoring case.
        internal static bool IsIconFileName(string fileNameWithoutExtension)
        {
            return fileNameWithoutExtension != null
                && fileNameWithoutExtension.EndsWith(Suffix, StringComparison.OrdinalIgnoreCase);
        }

        // True when the last path segment, without its extension, ends with "Icon".
        internal static bool IsIconAssetPath(string assetPath)
        {
            if (!TryGetNameRange(assetPath, out int start, out int length) || length < Suffix.Length)
                return false;
            return string.Compare(
                assetPath, start + length - Suffix.Length, Suffix, 0, Suffix.Length,
                StringComparison.OrdinalIgnoreCase) == 0;
        }

        // The folder that directly contains the asset, or null for a top-level path.
        internal static string GetParentFolder(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return null;
            int slash = assetPath.LastIndexOf('/');
            return slash > 0 ? assetPath.Substring(0, slash) : null;
        }

        internal static string GetFolderName(string folderPath)
        {
            if (string.IsNullOrEmpty(folderPath))
                return string.Empty;
            int slash = folderPath.LastIndexOf('/');
            return slash >= 0 ? folderPath.Substring(slash + 1) : folderPath;
        }

        // Lower is preferred: exactly "Icon", then "<FolderName>Icon", then any other match.
        internal static int GetRank(string assetPath, string folderName)
        {
            if (!TryGetNameRange(assetPath, out int start, out int length) || !IsIconAssetPath(assetPath))
                return NotAnIconRank;
            if (length == Suffix.Length)
                return ExactIconRank;

            if (!string.IsNullOrEmpty(folderName)
                && length == folderName.Length + Suffix.Length
                && string.Compare(assetPath, start, folderName, 0, folderName.Length,
                    StringComparison.OrdinalIgnoreCase) == 0)
                return FolderNameIconRank;

            return OtherIconRank;
        }

        // True when the candidate should replace the current choice for the same folder.
        // Equal ranks fall back to ordinal path order so the result is deterministic.
        internal static bool IsPreferred(string candidatePath, string currentPath, string folderName)
        {
            if (currentPath == null)
                return GetRank(candidatePath, folderName) != NotAnIconRank;

            int candidateRank = GetRank(candidatePath, folderName);
            int currentRank = GetRank(currentPath, folderName);
            if (candidateRank != currentRank)
                return candidateRank < currentRank;
            return string.CompareOrdinal(candidatePath, currentPath) < 0;
        }

        // Picks the icon for one folder from candidate paths, or null when none qualify.
        // Candidates that are not direct children of the folder are ignored.
        internal static string ChooseIcon(string folderPath, IEnumerable<string> candidatePaths)
        {
            if (string.IsNullOrEmpty(folderPath) || candidatePaths == null)
                return null;

            string folderName = GetFolderName(folderPath);
            string chosen = null;
            foreach (string candidate in candidatePaths)
            {
                if (!string.Equals(GetParentFolder(candidate), folderPath, StringComparison.Ordinal))
                    continue;
                if (IsPreferred(candidate, chosen, folderName))
                    chosen = candidate;
            }

            return chosen;
        }

        // Finds the start and length of the last path segment without its extension.
        static bool TryGetNameRange(string assetPath, out int start, out int length)
        {
            start = 0;
            length = 0;
            if (string.IsNullOrEmpty(assetPath))
                return false;

            start = assetPath.LastIndexOf('/') + 1;
            int end = assetPath.Length;
            int dot = assetPath.LastIndexOf('.');
            if (dot > start)
                end = dot;
            length = end - start;
            return length > 0;
        }
    }
}
