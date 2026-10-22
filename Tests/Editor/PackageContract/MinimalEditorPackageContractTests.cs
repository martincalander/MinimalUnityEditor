using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace MinimalEditor.Tests.Editor.PackageContract
{
    public sealed class MinimalEditorPackageContractTests
    {
        const string PackageName = "com.martincalander.minimaleditor";
        const string PackageJsonPath = "Packages/com.martincalander.minimaleditor/package.json";
        const string HarmonyPath =
            "Packages/com.martincalander.minimaleditor/ThirdParty/MinimalEditor.Harmony.dll";
        const string CoreClrHarmonyPath =
            "Packages/com.martincalander.minimaleditor/ThirdParty/CoreCLR/MinimalEditor.Harmony.dll";

        [Test]
        public void Package_HasExactIdentityAndEditorOnlyLayout()
        {
            PackageInfo package = PackageInfo.FindForAssetPath(PackageJsonPath);

            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));
            Assert.That(package.dependencies, Is.Empty);
            Assert.That(Directory.Exists(Path.Combine(package.resolvedPath, "Runtime")), Is.False);
            Assert.That(AssetDatabase.LoadAssetAtPath<UnityEngine.Texture2D>(
                $"Packages/{PackageName}/MinimalEditorIcon.png"), Is.Not.Null);
        }

        [TestCase("Editor/Console/MinimalEditor.Console.Editor.asmdef", "14cbb37563b2149acb4ac3e45bcbcd36", "MinimalEditor.Console.Editor", "!MINIMAL_EDITOR_DISABLE_CONSOLE")]
        [TestCase("Editor/MenuItem/MinimalEditor.MenuItem.Editor.asmdef", "6dc2dc88be2f04b369efaca8770557e3", "MinimalEditor.MenuItem.Editor", "!MINIMAL_EDITOR_DISABLE_MENU_ITEM")]
        [TestCase("Editor/PackageManager/MinimalEditor.PackageManager.Editor.asmdef", "3b105bd3244e641d2abbc87499797d3c", "MinimalEditor.PackageManager.Editor", "!MINIMAL_EDITOR_DISABLE_PACKAGE_MANAGER")]
        [TestCase("Editor/ProjectFolderVisibility/MinimalEditor.ProjectFolderVisibility.Editor.asmdef", "eb78ef6201f214b1e9d2fa0eb4ae142b", "MinimalEditor.ProjectFolderVisibility.Editor", "!MINIMAL_EDITOR_DISABLE_PROJECT_FOLDER_VISIBILITY")]
        [TestCase("Editor/FMOD/MinimalEditor.FMOD.Editor.asmdef", "229b6ab1758ff4b53b82e8bfb590fa30", "MinimalEditor.FMOD.Editor", "!MINIMAL_EDITOR_DISABLE_FMOD")]
        [TestCase("Editor/UnityEditor/MinimalEditor.Editor.asmdef", "16e29f94ad0e4961a9e2f4cf4567b684", "MinimalEditor.Editor", "!MINIMAL_EDITOR_DISABLE_UNITY_EDITOR")]
        public void ProductionAssembly_UsesPackageIdentityAndPreservesGuidAndDisableContracts(
            string relativePath,
            string expectedGuid,
            string assemblyName,
            string moduleDisableSymbol)
        {
            string assetPath = $"Packages/{PackageName}/{relativePath}";
            Assert.That(AssetDatabase.AssetPathToGUID(assetPath), Is.EqualTo(expectedGuid));

            PackageInfo package = PackageInfo.FindForAssetPath(assetPath);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));

            AssemblyDefinition definition = ReadAssemblyDefinition(
                Path.Combine(package.resolvedPath, relativePath));
            Assert.That(definition.name, Is.EqualTo(assemblyName));
            Assert.That(definition.rootNamespace, Is.EqualTo(assemblyName));
            Assert.That(definition.defineConstraints, Does.Contain(moduleDisableSymbol));
        }

        [Test]
        public void AllAssemblies_HaveUniqueNamesAndExpectedReferences()
        {
            string packageRoot = PackageInfo.FindForAssetPath(PackageJsonPath).resolvedPath;
            AssemblyDefinition[] definitions = Directory.GetFiles(
                    packageRoot, "*.asmdef", SearchOption.AllDirectories)
                .Select(ReadAssemblyDefinition)
                .ToArray();
            string[] assemblyNames = definitions.Select(definition => definition.name).ToArray();

            Assert.That(assemblyNames, Is.Not.Empty);
            Assert.That(assemblyNames, Is.Unique);
            foreach (AssemblyDefinition definition in definitions)
            {
                Assert.That(definition.name, Does.StartWith("MinimalEditor."));
                Assert.That(definition.rootNamespace, Does.StartWith("MinimalEditor."));
                Assert.That(definition.defineConstraints, Does.Contain("!MINIMAL_EDITOR_DISABLE_ALL"));
                Assert.That(definition.defineConstraints, Is.Unique);
                Assert.That(definition.includePlatforms, Is.EqualTo(new[] { "Editor" }), definition.name);
                Assert.That(definition.autoReferenced, Is.False, definition.name);
                if (definition.name == "MinimalEditor.FMOD.Editor")
                    Assert.That(definition.references, Is.EqualTo(new[] { "FMODUnityEditor" }));
                else
                    Assert.That(definition.references, Is.SubsetOf(assemblyNames));
                if (definition.name.StartsWith("MinimalEditor.PackageManager.", StringComparison.Ordinal))
                    Assert.That(definition.defineConstraints, Does.Contain("UNITY_6000_0_OR_NEWER"), definition.name);
                if (definition.name.StartsWith("MinimalEditor.ProjectFolderVisibility.", StringComparison.Ordinal))
                    Assert.That(definition.defineConstraints, Is.SupersetOf(new[]
                    {
                        "UNITY_7000_0_OR_NEWER", "ENABLE_CORECLR", "!MINIMAL_EDITOR_DISABLE_PROJECT_FOLDER_VISIBILITY"
                    }), definition.name);
            }
        }

        [Test]
        public void ProjectFolderVisibility_SharesMinimalEditorHarmonyWithoutAnotherBinary()
        {
            string packageRoot = PackageInfo.FindForAssetPath(PackageJsonPath).resolvedPath;
            foreach (string module in new[] { "PackageManager", "ProjectFolderVisibility" })
            {
                AssemblyDefinition definition = ReadAssemblyDefinition(Path.Combine(packageRoot,
                    $"Editor/{module}/MinimalEditor.{module}.Editor.asmdef"));
                Assert.That(definition.precompiledReferences, Is.EqualTo(new[] { "MinimalEditor.Harmony.dll" }), module);
            }

            string[] harmonyBinaries = Directory.GetFiles(packageRoot, "*.dll", SearchOption.AllDirectories)
                .Where(path => Path.GetFileName(path).IndexOf("Harmony", StringComparison.OrdinalIgnoreCase) >= 0)
                .ToArray();
            Assert.That(harmonyBinaries, Is.EquivalentTo(new[]
            {
                Path.Combine(packageRoot, "ThirdParty", "MinimalEditor.Harmony.dll"),
                Path.Combine(packageRoot, "ThirdParty", "CoreCLR", "MinimalEditor.Harmony.dll")
            }));
        }

        [TestCase(HarmonyPath, "e6ff89daf671406bb11379a34e86086c", "!UNITY_7000_0_OR_NEWER")]
        [TestCase(CoreClrHarmonyPath, "5d8560063d1af48dabc3ece455919538", "UNITY_7000_0_OR_NEWER")]
        public void Harmony_RuntimeVariantHasStableEditorOnlyImporter(string path, string guid, string constraint)
        {
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(guid));
            var importer = AssetImporter.GetAtPath(path) as PluginImporter;
            Assert.That(importer, Is.Not.Null);
            Assert.That(importer.GetCompatibleWithAnyPlatform(), Is.False);
            Assert.That(importer.GetCompatibleWithEditor(), Is.True);
            Assert.That(importer.DefineConstraints, Is.EqualTo(new[] { constraint }));
            foreach (var platform in new[] { BuildTarget.StandaloneOSX, BuildTarget.StandaloneWindows64, BuildTarget.StandaloneLinux64 })
                Assert.That(importer.GetCompatibleWithPlatform(platform), Is.False, platform.ToString());
            PackageInfo package = PackageInfo.FindForAssetPath(path);
            Assert.That(package, Is.Not.Null);
            Assert.That(package.name, Is.EqualTo(PackageName));

            string importerMeta = File.ReadAllText(Path.Combine(
                package.resolvedPath, path.Substring($"Packages/{PackageName}/".Length) + ".meta"));
            StringAssert.Contains("isExplicitlyReferenced: 1", importerMeta);
        }

        static AssemblyDefinition ReadAssemblyDefinition(string path)
        {
            return UnityEngine.JsonUtility.FromJson<AssemblyDefinition>(File.ReadAllText(path));
        }

        [Serializable]
        sealed class AssemblyDefinition
        {
            public string name = string.Empty;
            public string rootNamespace = string.Empty;
            public string[] references = Array.Empty<string>();
            public string[] defineConstraints = Array.Empty<string>();
            public string[] precompiledReferences = Array.Empty<string>();
            public string[] includePlatforms = Array.Empty<string>();
            public bool autoReferenced = true;
        }
    }
}
