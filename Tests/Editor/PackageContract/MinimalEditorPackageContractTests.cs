using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace Essentials.Tests.Editor.MinimalEditor
{
    public sealed class MinimalEditorPackageContractTests
    {
        const string PackageName = "com.martincalander.minimaleditor";
        const string PackageJsonPath = "Packages/com.martincalander.minimaleditor/package.json";
        const string HarmonyDirectoryPath =
            "Packages/com.martincalander.minimaleditor/ThirdParty";
        const string HarmonyPath =
            "Packages/com.martincalander.minimaleditor/ThirdParty/Essentials.MinimalEditor.Harmony.dll";
        const string CoreClrHarmonyPath =
            "Packages/com.martincalander.minimaleditor/ThirdParty/CoreCLR/Essentials.MinimalEditor.Harmony.dll";

        [Test]
        public void Package_HasExactIdentityAndEditorOnlyLayout()
        {
            PackageInfo package = PackageInfo.FindForAssetPath(PackageJsonPath);

            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
            Assert.That(package.version, Is.EqualTo("0.1.0"));
            Assert.That(package.dependencies, Is.Empty);
            Assert.That(Directory.Exists(Path.Combine(package.resolvedPath, "Runtime")), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
                $"Packages/{PackageName}/EssentialsIcon.png"), Is.Not.Null);
        }

        [TestCase("Editor/Console/Essentials.Console.Editor.asmdef", "14cbb37563b2149acb4ac3e45bcbcd36", "Essentials.Console.Editor", "!ESSENTIALS_DISABLE_CONSOLE")]
        [TestCase("Editor/MenuItem/Essentials.MenuItem.Editor.asmdef", "6dc2dc88be2f04b369efaca8770557e3", "Essentials.MenuItem.Editor", "!ESSENTIALS_DISABLE_MENU_ITEM")]
        [TestCase("Editor/PackageManager/Essentials.PackageManager.Editor.asmdef", "3b105bd3244e641d2abbc87499797d3c", "Essentials.PackageManager.Editor", "!ESSENTIALS_DISABLE_PACKAGE_MANAGER")]
        [TestCase("Editor/ProjectFolderVisibility/Essentials.ProjectFolderVisibility.Editor.asmdef", "eb78ef6201f214b1e9d2fa0eb4ae142b", "Essentials.ProjectFolderVisibility.Editor", "!ESSENTIALS_DISABLE_PROJECT_FOLDER_VISIBILITY")]
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
        [TestCase("Editor/ProjectFolderVisibility/AddressablesFolderVisibility.cs")]
        [TestCase("Editor/ProjectFolderVisibility/FolderVisibilitySettings.cs")]
        [TestCase("Editor/ProjectFolderVisibility/FolderVisibilityControls.cs")]
        [TestCase("Tests/Editor/ProjectFolderVisibility/AddressablesFolderVisibilityTests.cs")]
        public void ExtractedAssets_AreOwnedByMinimalEditor(string relativePath)
        {
            PackageInfo package = PackageInfo.FindForAssetPath($"Packages/{PackageName}/{relativePath}");
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
        }

        [TestCase("Editor/ProjectFolderVisibility/Essentials.ProjectFolderVisibility.Editor.asmdef", "eb78ef6201f214b1e9d2fa0eb4ae142b")]
        [TestCase("Tests/Editor/ProjectFolderVisibility/Essentials.ProjectFolderVisibility.Editor.Tests.asmdef", "8f18d14c84426447782690950e452507")]
        public void ProjectFolderVisibilityAssemblies_PreserveGuidsAndRequireUnity7CoreClr(
            string relativePath,
            string expectedGuid)
        {
            string assetPath = $"Packages/{PackageName}/{relativePath}";
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(expectedGuid));

            PackageInfo package = PackageInfo.FindForAssetPath(assetPath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));

            string json = File.ReadAllText(Path.Combine(package.resolvedPath, relativePath));
            StringAssert.Contains("\"UNITY_7000_0_OR_NEWER\"", json);
            StringAssert.Contains("\"ENABLE_CORECLR\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_ALL\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_MINIMAL_EDITOR\"", json);
            StringAssert.Contains("\"!ESSENTIALS_DISABLE_PROJECT_FOLDER_VISIBILITY\"", json);
            StringAssert.Contains("\"Editor\"", json);
            StringAssert.Contains("\"autoReferenced\": false", json);
        }

        [Test]
        public void ProjectFolderVisibility_SharesMinimalEditorHarmonyWithoutAnotherBinary()
        {
            string packageRoot = PackageInfo.FindForAssetPath(PackageJsonPath).resolvedPath;
            string json = File.ReadAllText(Path.Combine(packageRoot,
                "Editor/ProjectFolderVisibility/Essentials.ProjectFolderVisibility.Editor.asmdef"));
            StringAssert.Contains("\"Essentials.MinimalEditor.Harmony.dll\"", json);
            StringAssert.DoesNotContain("Essentials.ProjectFolderVisibility.Harmony.dll", json);

            string[] harmonyBinaries = Directory.GetFiles(packageRoot, "*.dll", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.That(harmonyBinaries, Is.EquivalentTo(new[]
            {
                Path.Combine(packageRoot, "ThirdParty", "Essentials.MinimalEditor.Harmony.dll"),
                Path.Combine(packageRoot, "ThirdParty", "CoreCLR", "Essentials.MinimalEditor.Harmony.dll")
            }));
        }

        [Test]
        public void Harmony_HasStablePackageOwnedEditorOnlyImporter()
        {
            Assert.That(
                AssetDatabase.AssetPathToGUID(HarmonyDirectoryPath),
                Is.EqualTo("c23ac49376204a03ad8d23c2b24682da"));
            Assert.That(
                AssetDatabase.AssetPathToGUID(HarmonyPath),
                Is.EqualTo("e6ff89daf671406bb11379a34e86086c"));

            var importer = AssetImporter.GetAtPath(HarmonyPath) as PluginImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False);
            Assert.That(importer.GetCompatibleWithEditor(), Is.True);

            PackageInfo package = PackageInfo.FindForAssetPath(HarmonyPath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));

            string importerMeta = File.ReadAllText(Path.Combine(
                package.resolvedPath,
                "ThirdParty/Essentials.MinimalEditor.Harmony.dll.meta"));
            StringAssert.Contains("isExplicitlyReferenced: 1", importerMeta);

            string asmdef = File.ReadAllText(Path.Combine(
                package.resolvedPath,
                "Editor/PackageManager/Essentials.PackageManager.Editor.asmdef"));
            StringAssert.Contains("\"Essentials.MinimalEditor.Harmony.dll\"", asmdef);
        }

        [Test]
        public void Harmony_RuntimeVariantsAreMutuallyExclusiveAndEditorOnly()
        {
            var mono = AssetImporter.GetAtPath(HarmonyPath) as PluginImporter;
            var coreClr = AssetImporter.GetAtPath(CoreClrHarmonyPath) as PluginImporter;
            Assert.That(mono, Is.Not.Null);
            Assert.That(coreClr, Is.Not.Null);
            Assert.That(mono.DefineConstraints, Is.EqualTo(new[] { "!UNITY_7000_0_OR_NEWER" }));
            Assert.That(coreClr.DefineConstraints, Is.EqualTo(new[] { "UNITY_7000_0_OR_NEWER" }));
            Assert.That(coreClr.GetCompatibleWithAnyPlatform(), Is.False);
            Assert.That(coreClr.GetCompatibleWithEditor(), Is.True);
            Assert.That(coreClr.GetCompatibleWithPlatform(BuildTarget.StandaloneOSX), Is.False);
            Assert.That(coreClr.GetCompatibleWithPlatform(BuildTarget.StandaloneWindows64), Is.False);
            Assert.That(coreClr.GetCompatibleWithPlatform(BuildTarget.StandaloneLinux64), Is.False);
        }

#if UNITY_6000_0_OR_NEWER
        [Test]
        public void Harmony_LoadsTheAssemblyForTheCurrentEditorRuntime()
        {
            Type harmony = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: true);
            var assembly = harmony.Assembly;
#if UNITY_7000_0_OR_NEWER
            Assert.That(assembly.GetName().Version, Is.EqualTo(new Version(2, 4, 2, 0)));
            Assert.That(assembly.GetReferencedAssemblies().Any(
                reference => reference.Name == "System.Runtime" && reference.Version.Major == 10), Is.True);
#else
            Assert.That(assembly.GetName().Version, Is.EqualTo(new Version(2, 4, 1, 0)));
            Assert.That(assembly.GetReferencedAssemblies().Any(
                reference => reference.Name == "mscorlib"), Is.True);
#endif
        }
#endif
    }
}
