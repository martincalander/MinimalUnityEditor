using System.IO;
using Essentials.PackageManager.Editor;
using NUnit.Framework;

namespace Essentials.Tests.Editor.PackageManager
{
    public sealed class PackageManagerAssetStoreDuplicateFilterTests
    {
        [Test]
        public void ShouldHideAssetStoreDuplicate_ReadOnlyPackageCachePath_ReturnsTrue()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.tool@abc123");

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    true,
                    "com.vendor.tool",
                    resolvedPath,
                    projectRoot),
                Is.True);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_ProjectPackagesPath_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Packages", "com.vendor.tool");

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    true,
                    "com.vendor.tool",
                    resolvedPath,
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_NonAssetStorePackage_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.tool@abc123");

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    false,
                    "com.vendor.tool",
                    resolvedPath,
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_UnknownRegisteredPackagePath_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    true,
                    "com.vendor.tool",
                    null,
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_RegisteredPackageResolverThrows_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            var package = new FakePackage("com.vendor.tool", new object(), new FakeVersionList(null));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    _ =>
                    {
                        throw new System.InvalidOperationException("Resolver failed.");
                    },
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_TechnicalNameInReadOnlyPackage_ReturnsTrue()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.tool@abc123");
            var package = new FakePackage("com.vendor.tool", new object(), new FakeVersionList(null));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    name => name == "com.vendor.tool" ? resolvedPath : null,
                    projectRoot),
                Is.True);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_TechnicalNameInEmbeddedPackage_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Packages", "com.vendor.tool");
            var package = new FakePackage("com.vendor.tool", new object(), new FakeVersionList(null));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    name => name == "com.vendor.tool" ? resolvedPath : null,
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_TechnicalNameAndImportedAssetSamePackage_ResolvesPackageOnce()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Packages", "com.vendor.bundle");
            int resolverCallCount = 0;
            var package = new FakePackage(
                "com.vendor.bundle",
                new object(),
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Packages/com.vendor.bundle/Feature/Tool.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    name =>
                    {
                        resolverCallCount++;
                        return name == "com.vendor.bundle" ? resolvedPath : null;
                    },
                    projectRoot),
                Is.False);
            Assert.That(resolverCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_LegacyAssetStoreImportedAssetInReadOnlyPackage_ReturnsTrue()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.bundle@abc123");
            var package = new FakePackage(
                string.Empty,
                new object(),
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Packages/com.vendor.bundle/Feature/Tool.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    name => name == "com.vendor.bundle" ? resolvedPath : null,
                    projectRoot),
                Is.True);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_LegacyAssetStoreImportedAssetInEmbeddedPackage_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Packages", "com.vendor.bundle");
            var package = new FakePackage(
                string.Empty,
                new object(),
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Packages/com.vendor.bundle/Feature/Tool.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    name => name == "com.vendor.bundle" ? resolvedPath : null,
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_LegacyAssetStoreImportedAssetOutsidePackages_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            var package = new FakePackage(
                string.Empty,
                new object(),
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Assets/Plugins/Vendor/Tool.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    _ => Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.bundle@abc123"),
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_LegacyAssetStoreImportedAssetUnknownRegisteredPackage_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            var package = new FakePackage(
                string.Empty,
                new object(),
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Packages/com.vendor.bundle/Feature/Tool.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    _ => null,
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_ImportedAssetInReadOnlyPackageButNonAssetStoreRow_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            var package = new FakePackage(
                string.Empty,
                null,
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Packages/com.vendor.bundle/Feature/Tool.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    _ => Path.Combine(projectRoot, "Library", "PackageCache", "com.vendor.bundle@abc123"),
                    projectRoot),
                Is.False);
        }

        [Test]
        public void ShouldHideAssetStoreDuplicate_DuplicateImportedPackageAssets_ResolvesPackageOnce()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "Packages", "com.vendor.bundle");
            int resolverCallCount = 0;
            var package = new FakePackage(
                string.Empty,
                new object(),
                new FakeVersionList(new FakeVersion(new[]
                {
                    new FakeAsset("Packages/com.vendor.bundle/Feature/One.cs"),
                    new FakeAsset("Packages/com.vendor.bundle/Feature/Two.cs")
                })));

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(
                    package,
                    name =>
                    {
                        resolverCallCount++;
                        return name == "com.vendor.bundle" ? resolvedPath : null;
                    },
                    projectRoot),
                Is.False);
            Assert.That(resolverCallCount, Is.EqualTo(1));
        }

        [Test]
        public void ExtractPackageNameFromImportedAssetPath_PackagesPath_ReturnsPackageName()
        {
            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ExtractPackageNameFromImportedAssetPath(
                    "Packages/com.vendor.bundle/Feature/Tool.cs"),
                Is.EqualTo("com.vendor.bundle"));
        }

        [Test]
        public void ExtractPackageNameFromImportedAssetPath_AssetsPath_ReturnsEmpty()
        {
            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.ExtractPackageNameFromImportedAssetPath(
                    "Assets/Plugins/Vendor/Tool.cs"),
                Is.Empty);
        }

        [Test]
        public void IsPathInsideProjectPackages_SiblingPackagesPrefix_ReturnsFalse()
        {
            string projectRoot = CreateProjectRootPath();
            string resolvedPath = Path.Combine(projectRoot, "PackagesExternal", "com.vendor.tool");

            Assert.That(
                PackageManagerAssetStoreDuplicateFilter.IsPathInsideProjectPackages(resolvedPath, projectRoot),
                Is.False);
        }

        static string CreateProjectRootPath()
        {
            return Path.Combine(Path.GetTempPath(), "EssentialsPackageManagerTests", "Project");
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
