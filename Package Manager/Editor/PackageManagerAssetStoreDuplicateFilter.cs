using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor.PackageManager;
using UnityEngine;

namespace Essentials.PackageManager.Editor
{
    internal static class PackageManagerAssetStoreDuplicateFilter
    {
        internal const string PackageNamePropertyName = "name";
        internal const string PackageProductPropertyName = "product";
        internal const string PackageVersionsPropertyName = "versions";
        internal const string VersionListImportedPropertyName = "imported";
        internal const string VersionListPrimaryPropertyName = "primary";
        internal const string VersionListImportAvailablePropertyName = "importAvailable";
        internal const string VersionListSuggestedUpdatePropertyName = "suggestedUpdate";
        internal const string VersionListLatestPropertyName = "latest";
        internal const string VersionImportedAssetsPropertyName = "importedAssets";
        internal const string AssetImportedPathFieldName = "importedPath";
        internal const string PackageConcreteTypeName = "UnityEditor.PackageManager.UI.Internal.Package";
        internal const string ProductInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IProduct";
        internal const string VersionListInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IVersionList";
        internal const string PackageVersionInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IPackageVersion";
        internal const string AssetTypeName = "UnityEditor.PackageManager.UI.Internal.Asset";

        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly object s_MemberCacheLock = new object();
        static readonly Dictionary<Type, Dictionary<string, MemberInfo>> s_MemberCache = new Dictionary<Type, Dictionary<string, MemberInfo>>();

        internal static bool ShouldHideAssetStoreDuplicate(object package)
        {
            return ShouldHideAssetStoreDuplicate(package, GetRegisteredPackageResolvedPath, GetProjectRoot());
        }

        internal static bool ShouldHideAssetStoreDuplicate(
            object package,
            Func<string, string> registeredPackageResolvedPathResolver,
            string projectRoot)
        {
            if (package == null || !IsAssetStoreBacked(package))
                return false;

            var visitedPackageNames = new HashSet<string>(StringComparer.Ordinal);
            string packageName = GetStringProperty(package, PackageNamePropertyName);
            if (TryShouldHideRegisteredPackage(packageName, visitedPackageNames, registeredPackageResolvedPathResolver, projectRoot))
                return true;

            foreach (string importedAssetPath in GetImportedAssetPaths(package))
            {
                string importedPackageName = ExtractPackageNameFromImportedAssetPath(importedAssetPath);
                if (TryShouldHideRegisteredPackage(importedPackageName, visitedPackageNames, registeredPackageResolvedPathResolver, projectRoot))
                    return true;
            }

            return false;
        }

        internal static bool ShouldHideAssetStoreDuplicate(
            bool isAssetStoreBacked,
            string packageName,
            string resolvedPath,
            string projectRoot)
        {
            if (!isAssetStoreBacked ||
                string.IsNullOrEmpty(packageName) ||
                string.IsNullOrEmpty(resolvedPath) ||
                string.IsNullOrEmpty(projectRoot))
            {
                return false;
            }

            return !IsPathInsideProjectPackages(resolvedPath, projectRoot);
        }

        internal static string ExtractPackageNameFromImportedAssetPath(string importedAssetPath)
        {
            if (string.IsNullOrEmpty(importedAssetPath))
                return string.Empty;

            string normalizedPath = importedAssetPath.Replace('\\', '/');
            const string packagesPrefix = "Packages/";
            if (!normalizedPath.StartsWith(packagesPrefix, StringComparison.Ordinal))
                return string.Empty;

            int packageNameStart = packagesPrefix.Length;
            int packageNameEnd = normalizedPath.IndexOf('/', packageNameStart);
            if (packageNameEnd <= packageNameStart)
                return string.Empty;

            return normalizedPath.Substring(packageNameStart, packageNameEnd - packageNameStart);
        }

        internal static bool IsPathInsideProjectPackages(string resolvedPath, string projectRoot)
        {
            if (string.IsNullOrEmpty(resolvedPath) || string.IsNullOrEmpty(projectRoot))
                return false;

            string packagePath = NormalizePath(Path.Combine(projectRoot, "Packages"));
            string candidatePath = NormalizePath(resolvedPath);
            if (string.IsNullOrEmpty(packagePath) || string.IsNullOrEmpty(candidatePath))
                return false;

            return string.Equals(candidatePath, packagePath, PathComparison) ||
                   candidatePath.StartsWith(packagePath + "/", PathComparison);
        }

        static bool TryShouldHideRegisteredPackage(
            string packageName,
            HashSet<string> visitedPackageNames,
            Func<string, string> registeredPackageResolvedPathResolver,
            string projectRoot)
        {
            if (string.IsNullOrEmpty(packageName) || !visitedPackageNames.Add(packageName))
                return false;

            return ShouldHideRegisteredPackage(packageName, registeredPackageResolvedPathResolver, projectRoot);
        }

        static bool ShouldHideRegisteredPackage(
            string packageName,
            Func<string, string> registeredPackageResolvedPathResolver,
            string projectRoot)
        {
            if (string.IsNullOrEmpty(packageName) || registeredPackageResolvedPathResolver == null)
                return false;

            string resolvedPath;
            try
            {
                resolvedPath = registeredPackageResolvedPathResolver(packageName);
            }
            catch
            {
                return false;
            }

            return ShouldHideAssetStoreDuplicate(true, packageName, resolvedPath, projectRoot);
        }

        static string GetRegisteredPackageResolvedPath(string packageName)
        {
            try
            {
                return PackageInfo.FindForPackageName(packageName)?.resolvedPath;
            }
            catch
            {
                return string.Empty;
            }
        }

        static IEnumerable<string> GetImportedAssetPaths(object package)
        {
            object versions = GetMemberValue(package, PackageVersionsPropertyName);
            if (versions == null)
                yield break;

            var visitedVersions = new HashSet<object>();
            foreach (object version in EnumerateVersionCandidates(versions))
            {
                if (version == null || !visitedVersions.Add(version))
                    continue;

                object importedAssets = GetMemberValue(version, VersionImportedAssetsPropertyName);
                if (!(importedAssets is IEnumerable assets))
                    continue;

                foreach (object asset in assets)
                {
                    string importedPath = GetStringMember(asset, AssetImportedPathFieldName);
                    if (!string.IsNullOrEmpty(importedPath))
                        yield return importedPath;
                }
            }
        }

        static IEnumerable<object> EnumerateVersionCandidates(object versions)
        {
            yield return GetMemberValue(versions, VersionListImportedPropertyName);
            yield return GetMemberValue(versions, VersionListPrimaryPropertyName);
            yield return GetMemberValue(versions, VersionListSuggestedUpdatePropertyName);
            yield return GetMemberValue(versions, VersionListImportAvailablePropertyName);
            yield return GetMemberValue(versions, VersionListLatestPropertyName);

            if (!(versions is IEnumerable versionList))
                yield break;

            foreach (object version in versionList)
                yield return version;
        }

        static bool IsAssetStoreBacked(object package)
        {
            return GetMemberValue(package, PackageProductPropertyName) != null;
        }

        static string GetStringProperty(object instance, string propertyName)
        {
            return GetStringMember(instance, propertyName);
        }

        static string GetStringMember(object instance, string memberName)
        {
            return GetMemberValue(instance, memberName) as string;
        }

        static object GetMemberValue(object instance, string memberName)
        {
            if (instance == null || string.IsNullOrEmpty(memberName))
                return null;

            try
            {
                var type = instance.GetType();
                var member = GetCachedMember(type, memberName);
                if (member is PropertyInfo property)
                    return property.GetValue(instance, null);

                if (member is FieldInfo field)
                    return field.GetValue(instance);
            }
            catch
            {
                return null;
            }

            return null;
        }

        static MemberInfo GetCachedMember(Type type, string memberName)
        {
            lock (s_MemberCacheLock)
            {
                if (!s_MemberCache.TryGetValue(type, out var members))
                {
                    members = new Dictionary<string, MemberInfo>();
                    s_MemberCache.Add(type, members);
                }

                if (members.TryGetValue(memberName, out var cachedMember))
                    return cachedMember;

                MemberInfo member = type.GetProperty(memberName, InstanceFlags);
                if (member == null)
                    member = type.GetField(memberName, InstanceFlags);

                members.Add(memberName, member);
                return member;
            }
        }

        static string GetProjectRoot()
        {
            try
            {
                var assetsDirectory = Directory.GetParent(Application.dataPath);
                return assetsDirectory?.FullName ?? string.Empty;
            }
            catch
            {
                return string.Empty;
            }
        }

        static string NormalizePath(string path)
        {
            try
            {
                return Path.GetFullPath(path)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Replace('\\', '/');
            }
            catch
            {
                return string.Empty;
            }
        }

        static StringComparison PathComparison
        {
            get
            {
#if UNITY_EDITOR_WIN || UNITY_EDITOR_OSX
                return StringComparison.OrdinalIgnoreCase;
#else
                return StringComparison.Ordinal;
#endif
            }
        }
    }
}
