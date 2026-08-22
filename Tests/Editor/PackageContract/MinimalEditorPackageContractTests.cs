using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Essentials.Tests.Editor.MinimalEditor
{
    public sealed class MinimalEditorPackageContractTests
    {
        const string PackageName = "com.essentials.minimaleditor";
        const string PackageJsonPath = "Packages/com.essentials.minimaleditor/package.json";
        const string HarmonyPath =
            "Packages/com.essentials.minimaleditor/ThirdParty/Essentials.Harmony.dll";

        [Test]
        public void Package_HasExactIdentityAndEditorOnlyLayout()
        {
            PackageInfo package = PackageInfo.FindForAssetPath(PackageJsonPath);

            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
            Assert.That(package.version, Is.EqualTo("0.1.0"));
            Assert.That(package.dependencies, Is.Empty);
            Assert.That(Directory.Exists(Path.Combine(package.resolvedPath, "Runtime")), Is.False);
        }

        [TestCase("Editor/Console/Essentials.Console.Editor.asmdef", "14cbb37563b2149acb4ac3e45bcbcd36", "Essentials.Console.Editor", "!ESSENTIALS_DISABLE_CONSOLE")]
        [TestCase("Editor/MenuItem/Essentials.MenuItem.Editor.asmdef", "6dc2dc88be2f04b369efaca8770557e3", "Essentials.MenuItem.Editor", "!ESSENTIALS_DISABLE_MENU_ITEM")]
        [TestCase("Editor/PackageManager/Essentials.PackageManager.Editor.asmdef", "3b105bd3244e641d2abbc87499797d3c", "Essentials.PackageManager.Editor", "!ESSENTIALS_DISABLE_PACKAGE_MANAGER")]
        [TestCase("Editor/FMOD/Essentials.FMOD.Editor.asmdef", "229b6ab1758ff4b53b82e8bfb590fa30", "Essentials.FMOD.Editor", "!ESSENTIALS_DISABLE_FMOD")]
        [TestCase("Editor/UnityEditor/Essentials.Editor.asmdef", "16e29f94ad0e4961a9e2f4cf4567b684", "Essentials.Editor", "!ESSENTIALS_DISABLE_UNITY_EDITOR")]
        public void ProductionAssembly_PreservesIdentityAndDisableContracts(
            string relativePath,
            string expectedGuid,
            string assemblyName,
            string legacyDisableSymbol)
        {
            string assetPath = $"Packages/{PackageName}/{relativePath}";
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(expectedGuid));

            PackageInfo package = PackageInfo.FindForAssetPath(assetPath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));

            string json = File.ReadAllText(Path.Combine(package.resolvedPath, relativePath));
            StringAssert.Contains($"\"{assemblyName}\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_ALL\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_MINIMAL_EDITOR\"", json);
            StringAssert.Contains($"\"{legacyDisableSymbol}\"", json);
        }

        [Test]
        public void PackageManagerAssemblies_AreLimitedToUnity6InternalApis()
        {
            string packageRoot = PackageInfo.FindForAssetPath(PackageJsonPath).resolvedPath;
            string[] relativePaths =
            {
                "Editor/PackageManager/Essentials.PackageManager.Editor.asmdef",
                "Tests/Editor/PackageManager/Essentials.PackageManager.Editor.Tests.asmdef"
            };

            foreach (string relativePath in relativePaths)
            {
                string json = File.ReadAllText(Path.Combine(packageRoot, relativePath));
                StringAssert.Contains("\"UNITY_6000_0_OR_NEWER\"", json);
            }
        }

        [TestCase("Editor/Console/ConsoleLogFilterInstaller.cs")]
        [TestCase("Editor/MenuItem/MenuItemHider.cs")]
        [TestCase("Editor/PackageManager/PackageManagerAssetStoreDuplicatePatch.cs")]
        [TestCase("Editor/UnityEditor/WindowTitleShortener.cs")]
        public void ExtractedAssets_AreOwnedByMinimalEditor(string relativePath)
        {
            PackageInfo package = PackageInfo.FindForAssetPath($"Packages/{PackageName}/{relativePath}");
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
        }

        [Test]
        public void Harmony_HasOneStableEditorOnlyOwner()
        {
            string[] harmonyAssets = AssetDatabase.GetAllAssetPaths()
                .Where(path => Path.GetFileName(path) == "Essentials.Harmony.dll")
                .ToArray();

            Assert.That(harmonyAssets, Is.EqualTo(new[] { HarmonyPath }));
            Assert.That(
                AssetDatabase.AssetPathToGUID(HarmonyPath),
                Is.EqualTo("5b81de61450c43b0bc0b8b08c3e3764a"));

            var importer = AssetImporter.GetAtPath(HarmonyPath) as PluginImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False);
            Assert.That(importer.GetCompatibleWithEditor(), Is.True);

            PackageInfo package = PackageInfo.FindForAssetPath(HarmonyPath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
        }
    }
}
