using System.IO;
using System.Linq;
using Essentials.PackageManager.Editor;
using NUnit.Framework;

namespace Essentials.Tests.Editor.PackageManager
{
    public sealed class PackageManagerAssetStoreDuplicateFilterTests
    {
        static string ProjectRoot => Path.Combine(Path.GetTempPath(), "EssentialsPackageManagerTests", "Project");

        [TestCase(true, "Library/PackageCache/com.vendor.tool@abc123", true)]
        [TestCase(true, "Packages/com.vendor.tool", false)]
        [TestCase(false, "Library/PackageCache/com.vendor.tool@abc123", false)]
        [TestCase(true, null, false)]
        public void ShouldHideAssetStoreDuplicate_RegisteredPath(bool assetStoreBacked, string relativePath, bool expected)
        {
            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                assetStoreBacked, "com.vendor.tool", ResolvePath(relativePath), ProjectRoot), Is.EqualTo(expected));
        }

        [TestCase(false, "Library/PackageCache/com.vendor.tool@abc123", true)]
        [TestCase(false, "Packages/com.vendor.tool", false)]
        [TestCase(true, "Library/PackageCache/com.vendor.tool@abc123", true)]
        [TestCase(true, "Packages/com.vendor.tool", false)]
        [TestCase(true, null, false)]
        public void ShouldHideAssetStoreDuplicate_TechnicalOrImportedPackage(
            bool legacyPackage, string relativePath, bool expected)
        {
            var package = CreatePackage(legacyPackage ? string.Empty : "com.vendor.tool", true,
                legacyPackage ? new[] { "Packages/com.vendor.tool/Feature/Tool.cs" } : new string[0]);

            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                package, name => name == "com.vendor.tool" ? ResolvePath(relativePath) : null, ProjectRoot),
                Is.EqualTo(expected));
        }

        [TestCase(false, "Packages/com.vendor.tool", true)]
        [TestCase(false, "Library/PackageCache/com.vendor.tool@abc123", false)]
        [TestCase(true, "Packages/com.vendor.tool", true)]
        [TestCase(true, "Library/PackageCache/com.vendor.tool@abc123", false)]
        public void TryGetLocalPackageForAssetStorePackage_TechnicalOrImportedPackage(
            bool legacyPackage, string relativePath, bool expected)
        {
            var package = CreatePackage(legacyPackage ? string.Empty : "com.vendor.tool", true,
                legacyPackage ? new[] { "Packages/com.vendor.tool/Feature/Tool.cs" } : new string[0]);
            string resolvedPath = ResolvePath(relativePath);

            Assert.That(PackageManagerAssetStoreDuplicateFilter.TryGetLocalPackageForAssetStorePackage(
                package, name => name == "com.vendor.tool" ? resolvedPath : null, ProjectRoot,
                out string packageName, out string localResolvedPath), Is.EqualTo(expected));
            Assert.That(packageName, Is.EqualTo(expected ? "com.vendor.tool" : string.Empty));
            Assert.That(localResolvedPath, Is.EqualTo(expected ? resolvedPath : string.Empty));
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_RegisteredPackageResolverThrows_ReturnsFalse()
        {
            var package = CreatePackage("com.vendor.tool", true);

            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                package, _ => throw new System.InvalidOperationException("Resolver failed."), ProjectRoot), Is.False);
        }

        [TestCase(true, null)]
        [TestCase(true, "")]
        [TestCase(true, " ")]
        [TestCase(true, "invalid\0root")]
        [TestCase(false, null)]
        [TestCase(false, "")]
        [TestCase(false, " ")]
        [TestCase(false, "invalid\0package")]
        public void UnknownPackageLocation_KeepsPackageVisible_AndDoesNotOfferLocalNavigation(
            bool invalidProjectRoot, string invalidPath)
        {
            var package = CreatePackage("com.vendor.tool", true);
            string root = invalidProjectRoot ? invalidPath : ProjectRoot;
            string resolvedPath = invalidProjectRoot ? ResolvePath("Library/PackageCache/com.vendor.tool@abc123") : invalidPath;

            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                package, _ => resolvedPath, root), Is.False);
            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                true, "com.vendor.tool", resolvedPath, root), Is.False);
            Assert.That(PackageManagerAssetStoreDuplicateFilter.TryGetLocalPackageForAssetStorePackage(
                package, _ => resolvedPath, root, out string packageName, out string localPath), Is.False);
            Assert.That(packageName, Is.Empty);
            Assert.That(localPath, Is.Empty);
        }

        [TestCase(true, "Assets/Plugins/Vendor/Tool.cs")]
        [TestCase(false, "Packages/com.vendor.tool/Feature/Tool.cs")]
        public void ShouldHideAssetStoreDuplicate_UnrelatedImportedAssets_ReturnsFalse(bool assetStoreBacked, string assetPath)
        {
            var package = CreatePackage(string.Empty, assetStoreBacked, assetPath);

            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                package, _ => ResolvePath("Library/PackageCache/com.vendor.tool@abc123"), ProjectRoot), Is.False);
        }

        [TestCase("com.vendor.tool", "Packages/com.vendor.tool/Feature/Tool.cs")]
        [TestCase("", "Packages/com.vendor.tool/Feature/One.cs", "Packages/com.vendor.tool/Feature/Two.cs")]
        public void ShouldHideAssetStoreDuplicate_DuplicateCandidates_ResolvesPackageOnce(string name, params string[] importedPaths)
        {
            int resolverCallCount = 0;
            var package = CreatePackage(name, true, importedPaths);

            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                package,
                packageName =>
                {
                    resolverCallCount++;
                    return packageName == "com.vendor.tool" ? ResolvePath("Library/PackageCache/com.vendor.tool@abc123") : null;
                },
                ProjectRoot), Is.True);
            Assert.That(resolverCallCount, Is.EqualTo(1));
        }

        [TestCase("Packages/com.vendor.bundle/Feature/Tool.cs", "com.vendor.bundle")]
        [TestCase("Assets/Plugins/Vendor/Tool.cs", "")]
        public void ExtractPackageNameFromImportedAssetPath(string path, string expected)
        {
            Assert.That(PackageManagerAssetStoreDuplicateFilter.ExtractPackageNameFromImportedAssetPath(path), Is.EqualTo(expected));
        }

        [Test]
        public void IsPathInsideProjectPackages_SiblingPackagesPrefix_ReturnsFalse()
        {
            Assert.That(PackageManagerAssetStoreDuplicateFilter.IsPathInsideProjectPackages(
                ResolvePath("PackagesExternal/com.vendor.tool"), ProjectRoot), Is.False);
        }

        [Test]
        public void PackageLocation_ProjectAtFilesystemRoot_PreservesEmbeddedAndCachedDistinction()
        {
            string projectRoot = Path.GetPathRoot(ProjectRoot);
            Assert.That(PackageManagerAssetStoreDuplicateFilter.IsPathInsideProjectPackages(
                Path.Combine(projectRoot, "Packages", "com.vendor.tool"), projectRoot), Is.True);
            Assert.That(PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                true, "com.vendor.tool", Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.tool"),
                projectRoot), Is.True);
        }

        static string ResolvePath(string relativePath)
        {
            return relativePath == null ? null : Path.Combine(ProjectRoot, relativePath);
        }

        static FakePackage CreatePackage(string name, bool assetStoreBacked, params string[] importedPaths)
        {
            var version = importedPaths.Length == 0 ? null : new FakeVersion(importedPaths.Select(path => new FakeAsset(path)).ToArray());
            return new FakePackage(name, assetStoreBacked ? new object() : null, new FakeVersionList(version));
        }

        sealed class FakePackage
        {
            public FakePackage(string name, object product, FakeVersionList versions)
            {
                this.name = name;
                this.product = product;
                this.versions = versions;
            }

            public string name { get; }
            public object product { get; }
            public FakeVersionList versions { get; }
        }

        sealed class FakeVersionList
        {
            public FakeVersionList(FakeVersion imported)
            {
                this.imported = imported;
                primary = imported;
            }

            public FakeVersion imported { get; }
            public FakeVersion primary { get; }
            public FakeVersion suggestedUpdate => null;
            public FakeVersion importAvailable => null;
            public FakeVersion latest => primary;
        }

        sealed class FakeVersion
        {
            public FakeVersion(FakeAsset[] importedAssets)
            {
                this.importedAssets = importedAssets;
            }

            public FakeAsset[] importedAssets { get; }
        }

        sealed class FakeAsset
        {
            public FakeAsset(string importedPath)
            {
                this.importedPath = importedPath;
            }

            public readonly string importedPath;
        }
    }
}
