using System.Collections;
using System.IO;
using MinimalEditor.FolderIcons.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MinimalEditor.Tests.Editor.FolderIcons
{
    public sealed class FolderIconIndexTests
    {
        string root;
        (bool HasKey, bool Value) enabledSnapshot;

        [SetUp]
        public void SetUp()
        {
            enabledSnapshot = (
                EditorPrefs.HasKey(FolderIconsPreferences.EnabledKey),
                EditorPrefs.GetBool(FolderIconsPreferences.EnabledKey, FolderIconsPreferences.EnabledDefaultValue));
            FolderIconsPreferences.Enabled = true;

            root = "Assets/FolderIconsTests_" + System.Guid.NewGuid().ToString("N").Substring(0, 8);
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(root));
            FolderIconIndex.Invalidate();
        }

        [TearDown]
        public void TearDown()
        {
            if (AssetDatabase.IsValidFolder(root))
                AssetDatabase.DeleteAsset(root);

            FolderIconsPreferences.Enabled = enabledSnapshot.HasKey
                ? enabledSnapshot.Value
                : FolderIconsPreferences.EnabledDefaultValue;
            if (!enabledSnapshot.HasKey)
                EditorPrefs.DeleteKey(FolderIconsPreferences.EnabledKey);
            FolderIconsPreferences.Reload();
            FolderIconIndex.Invalidate();
        }

        [Test]
        public void Drawer_IsRegisteredAfterCodeLoad()
        {
            Assert.That(FolderIconDrawer.Registered, Is.True);
        }

        [Test]
        public void Preferences_DefaultToEnabled()
        {
            EditorPrefs.DeleteKey(FolderIconsPreferences.EnabledKey);
            FolderIconsPreferences.Reload();

            Assert.That(FolderIconsPreferences.Enabled, Is.True);
        }

        [Test]
        public void SettingsProvider_IsUserScopedAtFolderIconsPath()
        {
            SettingsProvider provider = FolderIconsPreferencesProvider.CreateProvider();

            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Minimal Editor/Folder Icons"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.User));
        }

        [Test]
        public void Index_MapsFolderToDirectChildIcon()
        {
            string folder = CreateFolder("Characters");
            string icon = CreateTexture(folder + "/CharactersIcon.png");

            Assert.That(TryGetIcon(folder, out Texture2D texture), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(icon));
        }

        [Test]
        public void Index_IgnoresNestedIconForGrandparent()
        {
            string parent = CreateFolder("Nested");
            string child = CreateFolder("Nested/Deep");
            CreateTexture(child + "/Icon.png");

            Assert.That(TryGetIcon(parent, out _), Is.False);
            Assert.That(TryGetIcon(child, out _), Is.True);
        }

        [Test]
        public void Index_IgnoresNonTextureNamedLikeIcon()
        {
            string folder = CreateFolder("Text");
            File.WriteAllText(folder + "/FooIcon.txt", "not a texture");
            AssetDatabase.ImportAsset(folder + "/FooIcon.txt", ImportAssetOptions.ForceSynchronousImport);

            Assert.That(TryGetIcon(folder, out _), Is.False);
        }

        [Test]
        public void Index_AppliesTieBreakToImportedAssets()
        {
            string folder = CreateFolder("Tie");
            CreateTexture(folder + "/AIcon.png");
            CreateTexture(folder + "/TieIcon.png");

            Assert.That(TryGetIcon(folder, out Texture2D texture), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(folder + "/TieIcon.png"));

            CreateTexture(folder + "/Icon.png");
            FolderIconIndex.Invalidate();
            Assert.That(TryGetIcon(folder, out texture), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(folder + "/Icon.png"));
        }

        [UnityTest]
        public IEnumerator Index_RefreshesAfterDelete()
        {
            string folder = CreateFolder("Deleted");
            string icon = CreateTexture(folder + "/Icon.png");
            Assert.That(TryGetIcon(folder, out _), Is.True);

            Assert.That(AssetDatabase.DeleteAsset(icon), Is.True);
            yield return WaitForProjectChange();

            Assert.That(TryGetIcon(folder, out _), Is.False);
        }

        [UnityTest]
        public IEnumerator Index_RefreshesAfterRename()
        {
            string folder = CreateFolder("Renamed");
            string icon = CreateTexture(folder + "/Icon.png");
            Assert.That(TryGetIcon(folder, out _), Is.True);

            Assert.That(AssetDatabase.RenameAsset(icon, "Picture"), Is.Empty);
            yield return WaitForProjectChange();
            Assert.That(TryGetIcon(folder, out _), Is.False);

            Assert.That(AssetDatabase.RenameAsset(folder + "/Picture.png", "RenamedIcon"), Is.Empty);
            yield return WaitForProjectChange();
            Assert.That(TryGetIcon(folder, out Texture2D texture), Is.True);
            Assert.That(AssetDatabase.GetAssetPath(texture), Is.EqualTo(folder + "/RenamedIcon.png"));
        }

        [UnityTest]
        public IEnumerator Index_RefreshesAfterMove()
        {
            string source = CreateFolder("Source");
            string target = CreateFolder("Target");
            string icon = CreateTexture(source + "/Icon.png");
            Assert.That(TryGetIcon(source, out _), Is.True);
            Assert.That(TryGetIcon(target, out _), Is.False);

            Assert.That(AssetDatabase.MoveAsset(icon, target + "/Icon.png"), Is.Empty);
            yield return WaitForProjectChange();

            Assert.That(TryGetIcon(source, out _), Is.False);
            Assert.That(TryGetIcon(target, out _), Is.True);
        }

        [Test]
        public void Index_ReturnsNothingWhenDisabled()
        {
            string folder = CreateFolder("Disabled");
            CreateTexture(folder + "/Icon.png");
            Assert.That(TryGetIcon(folder, out _), Is.True);

            FolderIconsPreferences.Enabled = false;

            Assert.That(TryGetIcon(folder, out Texture2D texture), Is.False);
            Assert.That(texture, Is.Null);
            Assert.That(EditorPrefs.GetBool(FolderIconsPreferences.EnabledKey, true), Is.False);
        }

        [Test]
        public void Index_TogglingInvalidatesTheIndex()
        {
            string folder = CreateFolder("Toggle");
            CreateTexture(folder + "/Icon.png");
            Assert.That(TryGetIcon(folder, out _), Is.True);
            Assert.That(FolderIconIndex.IsDirty, Is.False);

            FolderIconsPreferences.Enabled = false;
            Assert.That(FolderIconIndex.IsDirty, Is.True);
            FolderIconsPreferences.Enabled = true;
            Assert.That(TryGetIcon(folder, out _), Is.True);
        }

        [Test]
        public void Index_DoesNotRebuildOnRepeatedLookups()
        {
            string folder = CreateFolder("Repeated");
            CreateTexture(folder + "/Icon.png");
            string guid = AssetDatabase.AssetPathToGUID(folder);
            Assert.That(FolderIconIndex.TryGetIcon(guid, out _), Is.True);
            int builds = FolderIconIndex.BuildCount;

            for (int i = 0; i < 100; i++)
                FolderIconIndex.TryGetIcon(guid, out _);

            Assert.That(FolderIconIndex.BuildCount, Is.EqualTo(builds));
        }

        [Test]
        public void Index_LookupDoesNotAllocate()
        {
            string folder = CreateFolder("Allocation");
            CreateTexture(folder + "/Icon.png");
            string hit = AssetDatabase.AssetPathToGUID(folder);
            string miss = AssetDatabase.AssetPathToGUID(root);
            Assert.That(FolderIconIndex.TryGetIcon(hit, out _), Is.True);

            // Warm up first so one-time JIT and runtime work is not counted.
            for (int i = 0; i < 100; i++)
                LookUp(hit, miss);

            long before = System.GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < 1000; i++)
                LookUp(hit, miss);
            long allocated = System.GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.That(allocated, Is.Zero);
        }

        static void LookUp(string hit, string miss)
        {
            FolderIconIndex.TryGetIcon(hit, out _);
            FolderIconIndex.TryGetIcon(miss, out _);
        }

        [Test]
        public void Index_IncludesPackageFolders()
        {
            Assert.That(TryGetIcon("Packages/com.martincalander.minimaleditor", out Texture2D texture), Is.True);
            Assert.That(
                AssetDatabase.GetAssetPath(texture),
                Is.EqualTo("Packages/com.martincalander.minimaleditor/MinimalEditorIcon.png"));
        }

        static bool TryGetIcon(string folder, out Texture2D texture)
        {
            return FolderIconIndex.TryGetIcon(AssetDatabase.AssetPathToGUID(folder), out texture);
        }

        string CreateFolder(string relativePath)
        {
            string parent = root;
            foreach (string part in relativePath.Split('/'))
            {
                string next = parent + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(parent, part);
                parent = next;
            }

            return parent;
        }

        static string CreateTexture(string path)
        {
            var texture = new Texture2D(4, 4);
            try
            {
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            return path;
        }

        // Asset changes reach EditorApplication.projectChanged on a later Editor update.
        static IEnumerator WaitForProjectChange()
        {
            for (int frame = 0; frame < 10 && !FolderIconIndex.IsDirty; frame++)
                yield return null;
            Assert.That(FolderIconIndex.IsDirty, Is.True, "projectChanged did not invalidate the index.");
        }
    }
}
