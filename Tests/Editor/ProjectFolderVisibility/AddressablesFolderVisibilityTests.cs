using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Object = UnityEngine.Object;

namespace MinimalEditor.ProjectFolderVisibility.Editor.Tests
{
    public sealed class AddressablesFolderPathTests
    {
        FolderVisibilityPreferencesSnapshot preferences;

        [SetUp]
        public void SetUp()
        {
            preferences = new FolderVisibilityPreferencesSnapshot();
            FolderVisibilitySettings.Enabled = true;
            FolderVisibilitySettings.SetFolderHidden(AddressablesFolderVisibility.FolderPath, true);
        }

        [TearDown]
        public void TearDown() => preferences?.Dispose();

        [TestCase("Assets/AddressableAssetsData", true)]
        [TestCase("assets/addressableassetsdata/AssetGroups/Example.asset", true)]
        [TestCase("Assets/AddressableAssetsData/Child", true)]
        [TestCase("Assets/AddressableAssetsDatabase", false)]
        [TestCase("Assets/AddressableAssetsData.asset", false)]
        [TestCase("Assets/Other/AddressableAssetsData", false)]
        [TestCase("Packages/com.example/AddressableAssetsData", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void MatchesOnlyTheRootFolderAndItsDescendants(string path, bool expected)
        {
            Assert.That(AddressablesFolderVisibility.IsHiddenPath(path), Is.EqualTo(expected));
        }
    }

    public sealed class FolderVisibilitySettingsTests
    {
        const string StreamingAssetsPath = "Assets/StreamingAssets";
        const string CustomFolderPath = "Assets/FolderVisibilityTestGenerated";
        FolderVisibilityPreferencesSnapshot preferences;

        [SetUp]
        public void SetUp()
        {
            preferences = new FolderVisibilityPreferencesSnapshot();
            FolderVisibilitySettings.Enabled = true;
            FolderVisibilitySettings.SetFolderHidden(AddressablesFolderVisibility.FolderPath, false);
            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, false);
        }

        [TearDown]
        public void TearDown() => preferences?.Dispose();

        [TestCase("Assets/FolderVisibilityTestGenerated")]
        [TestCase("Assets/FolderVisibilityTestGenerated/")]
        [TestCase("Assets\\FolderVisibilityTestGenerated\\")]
        public void CustomFoldersNormalizePathsAndRejectDuplicates(string path)
        {
            Assert.That(FolderVisibilitySettings.AddFolder(path), Is.True);
            Assert.That(FolderVisibilitySettings.AddFolder(CustomFolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.AddFolder(CustomFolderPath.ToLowerInvariant()), Is.False);
            int matchingRules = 0;
            foreach (var rule in FolderVisibilitySettings.Folders)
                if (rule.Path == CustomFolderPath)
                    matchingRules++;
            Assert.That(matchingRules, Is.EqualTo(1));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("Assets")]
        [TestCase("Assets/")]
        [TestCase("Assets\\")]
        [TestCase("Packages/com.example/Generated")]
        [TestCase("/Assets/Generated")]
        [TestCase("Assets/../Generated")]
        [TestCase("Assets/Generated/../Other")]
        [TestCase("Assets\\Generated\\..\\Other")]
        public void CustomFoldersRejectRootsAndPathsOutsideAssets(string path)
        {
            int count = FolderVisibilitySettings.Folders.Count;

            Assert.That(FolderVisibilitySettings.AddFolder(path), Is.False);
            Assert.That(FolderVisibilitySettings.Folders.Count, Is.EqualTo(count));
        }

        [Test]
        public void CustomRulesOnlyHideTheSelectedFolderAndItsDescendants()
        {
            Assert.That(FolderVisibilitySettings.AddFolder(CustomFolderPath), Is.True);
            FolderVisibilitySettings.SetFolderHidden(CustomFolderPath, true);

            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath.ToLowerInvariant() + "/Child.asset"), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath + "Sibling"), Is.False);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath + ".asset"), Is.False);
            Assert.That(FolderVisibilitySettings.IsHiddenPath("Assets/Other/FolderVisibilityTestGenerated"), Is.False);
            Assert.That(FolderVisibilitySettings.IsHiddenPath("Packages/com.example/FolderVisibilityTestGenerated"), Is.False);

            FolderVisibilitySettings.SetFolderHidden(CustomFolderPath, false);

            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath + "/Child.asset"), Is.False);
        }

        [Test]
        public void RemovingACustomRuleRestoresVisibilityAndProtectsBuiltInRules()
        {
            Assert.That(FolderVisibilitySettings.AddFolder(CustomFolderPath), Is.True);
            FolderVisibilitySettings.SetFolderHidden(CustomFolderPath, true);

            Assert.That(FolderVisibilitySettings.RemoveFolder(CustomFolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath + "/Child.asset"), Is.False);
            Assert.That(FolderVisibilitySettings.RemoveFolder(CustomFolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.IsBuiltInFolder(AddressablesFolderVisibility.FolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsBuiltInFolder(StreamingAssetsPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsBuiltInFolder(CustomFolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.RemoveFolder(AddressablesFolderVisibility.FolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.RemoveFolder(StreamingAssetsPath), Is.False);
        }

        [Test]
        public void MasterTogglePreservesIndependentFolderChoicesAcrossReload()
        {
            FolderVisibilitySettings.SetFolderHidden(AddressablesFolderVisibility.FolderPath, true);
            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, false);
            Assert.That(FolderVisibilitySettings.AddFolder(CustomFolderPath), Is.True);
            FolderVisibilitySettings.SetFolderHidden(CustomFolderPath, true);
            FolderVisibilitySettings.Enabled = false;

            FolderVisibilitySettings.Reload();

            Assert.That(FolderVisibilitySettings.Enabled, Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(AddressablesFolderVisibility.FolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(StreamingAssetsPath), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(CustomFolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(AddressablesFolderVisibility.FolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath + "/Child.asset"), Is.False);

            FolderVisibilitySettings.Enabled = true;

            Assert.That(FolderVisibilitySettings.IsHiddenPath(AddressablesFolderVisibility.FolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(StreamingAssetsPath), Is.False);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(CustomFolderPath + "/Child.asset"), Is.True);
        }

        [Test]
        public void LegacyAddressablesToggleDoesNotChangeStreamingAssetsOrMasterChoice()
        {
            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, true);
            FolderVisibilitySettings.Enabled = false;

            AddressablesFolderVisibility.ShowFolder = false;

            Assert.That(FolderVisibilitySettings.IsFolderHidden(AddressablesFolderVisibility.FolderPath), Is.True);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(StreamingAssetsPath), Is.True);
            Assert.That(FolderVisibilitySettings.Enabled, Is.False);

            AddressablesFolderVisibility.ShowFolder = true;

            Assert.That(FolderVisibilitySettings.IsFolderHidden(AddressablesFolderVisibility.FolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(StreamingAssetsPath), Is.True);
            Assert.That(FolderVisibilitySettings.Enabled, Is.False);
        }

        [Test]
        public void MissingPreferenceUsesDefaultsUntilExplicitFolderChoicesAreSaved()
        {
            EditorPrefs.DeleteKey(FolderVisibilitySettings.PreferenceKey);

            FolderVisibilitySettings.Reload();

            Assert.That(FolderVisibilitySettings.Enabled, Is.True);
            foreach (var rule in FolderVisibilitySettings.Folders)
                Assert.That(rule.Hidden, Is.EqualTo(rule.Path == AddressablesFolderVisibility.FolderPath), rule.Path);

            FolderVisibilitySettings.SetFolderHidden(AddressablesFolderVisibility.FolderPath, false);
            FolderVisibilitySettings.Reload();

            Assert.That(FolderVisibilitySettings.IsFolderHidden(AddressablesFolderVisibility.FolderPath), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(StreamingAssetsPath), Is.False);
        }

        [TestCase("Assets/Plugins")]
        [TestCase("Assets/Gizmos")]
        [TestCase("Assets/Settings")]
        public void AdditionalBuiltInFoldersToggleAndPersistIndependently(string path)
        {
            Assert.That(FolderVisibilitySettings.IsBuiltInFolder(path), Is.True);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(path), Is.False);
            Assert.That(FolderVisibilitySettings.RemoveFolder(path), Is.False);

            FolderVisibilitySettings.SetFolderHidden(path + "/", true);
            FolderVisibilitySettings.Reload();

            Assert.That(FolderVisibilitySettings.IsHiddenPath(path), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(path.ToLowerInvariant() + "/Child.asset"), Is.True);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(path + "Sibling"), Is.False);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(path + ".asset"), Is.False);
            foreach (var rule in FolderVisibilitySettings.Folders)
                Assert.That(rule.Hidden, Is.EqualTo(rule.Path == path), rule.Path);

            FolderVisibilitySettings.Enabled = false;
            Assert.That(FolderVisibilitySettings.IsHiddenPath(path + "/Child.asset"), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden(path), Is.True);
            FolderVisibilitySettings.Enabled = true;
            Assert.That(FolderVisibilitySettings.IsHiddenPath(path + "/Child.asset"), Is.True);
            FolderVisibilitySettings.SetFolderHidden(path, false);
            Assert.That(FolderVisibilitySettings.IsHiddenPath(path + "/Child.asset"), Is.False);
        }

        [Test]
        public void ExistingPackagePreferencesKeepChoicesWhenBuiltInFoldersAreAdded()
        {
            EditorPrefs.SetString(FolderVisibilitySettings.PreferenceKey,
                "{\"enabled\":false,\"folders\":[" +
                "{\"path\":\"Assets/AddressableAssetsData\",\"hidden\":false}," +
                "{\"path\":\"Assets/StreamingAssets\",\"hidden\":true}," +
                "{\"path\":\"Assets/Plugins/\",\"hidden\":true}," +
                "{\"path\":\"Assets/CustomGenerated\",\"hidden\":true}]}");

            FolderVisibilitySettings.Reload();

            Assert.That(FolderVisibilitySettings.Enabled, Is.False);
            Assert.That(FolderVisibilitySettings.Folders.Count, Is.EqualTo(6));
            Assert.That(FolderVisibilitySettings.IsFolderHidden("Assets/AddressableAssetsData"), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden("Assets/StreamingAssets"), Is.True);
            Assert.That(FolderVisibilitySettings.IsFolderHidden("Assets/Plugins"), Is.True);
            Assert.That(FolderVisibilitySettings.IsFolderHidden("Assets/Gizmos"), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden("Assets/Settings"), Is.False);
            Assert.That(FolderVisibilitySettings.IsFolderHidden("Assets/CustomGenerated"), Is.True);
        }

        [Test]
        public void PreferencesAreRegisteredInTheUserScope()
        {
            SettingsProvider provider = FolderVisibilityControls.CreateProvider();

            Assert.That(provider.settingsPath, Is.EqualTo("Preferences/Minimal Editor/Project Folders"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.User));
            Assert.That(provider.guiHandler, Is.Not.Null);
        }
    }

    public sealed class AddressablesFolderVisibilityTests
    {
        const string StreamingAssetsPath = "Assets/StreamingAssets";
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags StaticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
        static readonly Type BrowserType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.ProjectBrowser", true);
        readonly List<EditorWindow> ownedWindows = new List<EditorWindow>();
        readonly List<string> additionalProbes = new List<string>();
        readonly List<string> additionalRoots = new List<string>();
        FolderVisibilityPreferencesSnapshot preferences;
        bool createdRoot;
        bool createdStreamingAssetsRoot;
        string probePath;
        string streamingAssetsProbePath;
        Object previousBrowser;
        Object[] previousSelection;

        [SetUp]
        public void SetUp()
        {
            Assert.That(AddressablesFolderVisibility.IsPatched, Is.True, AddressablesFolderVisibility.PatchError);
            previousSelection = Selection.objects;
            previousBrowser = (Object)BrowserType.GetField("s_LastInteractedProjectBrowser", StaticFlags).GetValue(null);
            preferences = new FolderVisibilityPreferencesSnapshot();
            FolderVisibilitySettings.Enabled = true;
            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, false);

            if (!AssetDatabase.IsValidFolder(AddressablesFolderVisibility.FolderPath))
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", "AddressableAssetsData"), Is.Not.Empty);
                createdRoot = true;
            }
            string probeName = "FolderVisibilityTest_" + Guid.NewGuid().ToString("N");
            probePath = AddressablesFolderVisibility.FolderPath + "/" + probeName;
            Assert.That(AssetDatabase.CreateFolder(AddressablesFolderVisibility.FolderPath, probeName), Is.Not.Empty);
            AddressablesFolderVisibility.ShowFolder = true;
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                foreach (EditorWindow window in ownedWindows)
                    if (window != null)
                        Object.DestroyImmediate(window);
                ownedWindows.Clear();
                foreach (var path in additionalProbes)
                    if (AssetDatabase.IsValidFolder(path))
                        Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
                additionalProbes.Clear();
                foreach (var path in additionalRoots)
                    if (AssetDatabase.IsValidFolder(path))
                        Assert.That(AssetDatabase.DeleteAsset(path), Is.True);
                additionalRoots.Clear();
                if (!string.IsNullOrEmpty(probePath) && AssetDatabase.IsValidFolder(probePath))
                    Assert.That(AssetDatabase.DeleteAsset(probePath), Is.True);
                probePath = null;
                if (createdRoot)
                {
                    Assert.That(AssetDatabase.DeleteAsset(AddressablesFolderVisibility.FolderPath), Is.True);
                    createdRoot = false;
                }
                if (!string.IsNullOrEmpty(streamingAssetsProbePath) && AssetDatabase.IsValidFolder(streamingAssetsProbePath))
                    Assert.That(AssetDatabase.DeleteAsset(streamingAssetsProbePath), Is.True);
                streamingAssetsProbePath = null;
                if (createdStreamingAssetsRoot)
                {
                    Assert.That(AssetDatabase.DeleteAsset(StreamingAssetsPath), Is.True);
                    createdStreamingAssetsRoot = false;
                }
            }
            finally
            {
                if (preferences != null)
                {
                    preferences.Dispose();
                    preferences = null;
                    Selection.objects = previousSelection;
                    BrowserType.GetField("s_LastInteractedProjectBrowser", StaticFlags).SetValue(null, previousBrowser);
                }
            }
        }

        [Test]
        public void ToggleRefreshesBothProjectWindowLayoutsAndPreservesAssets()
        {
            EditorWindow columns = CreateBrowser("TwoColumns");
            EditorWindow tree = CreateBrowser("OneColumn");
            string folderGuid = AssetDatabase.AssetPathToGUID(AddressablesFolderVisibility.FolderPath);
            string probeGuid = AssetDatabase.AssetPathToGUID(probePath);

            Assert.That(TreePaths(columns, "m_FolderTree"), Does.Contain(AddressablesFolderVisibility.FolderPath));
            Assert.That(TreePaths(tree, "m_AssetTree"), Does.Contain(AddressablesFolderVisibility.FolderPath));
            Assert.That(ListPaths(columns), Does.Contain(AddressablesFolderVisibility.FolderPath));

            AddressablesFolderVisibility.ShowFolder = false;

            Assert.That(TreePaths(columns, "m_FolderTree"), Has.None.EqualTo(AddressablesFolderVisibility.FolderPath));
            Assert.That(TreePaths(tree, "m_AssetTree"), Has.None.EqualTo(AddressablesFolderVisibility.FolderPath));
            Assert.That(ListPaths(columns), Has.None.EqualTo(AddressablesFolderVisibility.FolderPath));
            Assert.That(AssetDatabase.AssetPathToGUID(AddressablesFolderVisibility.FolderPath), Is.EqualTo(folderGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(probePath), Is.EqualTo(probeGuid));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(AddressablesFolderVisibility.FolderPath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(probePath), Is.Not.Null);
            Assert.That(AssetDatabase.FindAssets("", new[] { AddressablesFolderVisibility.FolderPath }), Does.Contain(probeGuid));

            AddressablesFolderVisibility.ShowFolder = true;

            Assert.That(TreePaths(columns, "m_FolderTree"), Does.Contain(AddressablesFolderVisibility.FolderPath));
            Assert.That(TreePaths(tree, "m_AssetTree"), Does.Contain(AddressablesFolderVisibility.FolderPath));
            Assert.That(ListPaths(columns), Does.Contain(AddressablesFolderVisibility.FolderPath));
        }

        [Test]
        public void StreamingAssetsAndAddressablesToggleIndependentlyInBothLayouts()
        {
            CreateStreamingAssetsProbe();
            EditorWindow columns = CreateBrowser("TwoColumns");
            EditorWindow tree = CreateBrowser("OneColumn");
            string folderGuid = AssetDatabase.AssetPathToGUID(StreamingAssetsPath);
            string probeGuid = AssetDatabase.AssetPathToGUID(streamingAssetsProbePath);

            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, true);

            AssertFolderVisibility(columns, tree, StreamingAssetsPath, false);
            AssertFolderVisibility(columns, tree, AddressablesFolderVisibility.FolderPath, true);
            Assert.That(AssetDatabase.AssetPathToGUID(StreamingAssetsPath), Is.EqualTo(folderGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(streamingAssetsProbePath), Is.EqualTo(probeGuid));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(streamingAssetsProbePath), Is.Not.Null);
            Assert.That(AssetDatabase.FindAssets("", new[] { StreamingAssetsPath }), Does.Contain(probeGuid));

            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, false);
            AddressablesFolderVisibility.ShowFolder = false;

            AssertFolderVisibility(columns, tree, StreamingAssetsPath, true);
            AssertFolderVisibility(columns, tree, AddressablesFolderVisibility.FolderPath, false);
        }

        [TestCase("Assets/Plugins")]
        [TestCase("Assets/Gizmos")]
        [TestCase("Assets/Settings")]
        public void AdditionalBuiltInFolderTogglesRefreshBothLayoutsAndPreserveAssets(string path)
        {
            string childPath = CreateAdditionalProbe(path);
            EditorWindow columns = CreateBrowser("TwoColumns");
            EditorWindow tree = CreateBrowser("OneColumn");
            string folderGuid = AssetDatabase.AssetPathToGUID(path);
            string childGuid = AssetDatabase.AssetPathToGUID(childPath);
            AssertFolderVisibility(columns, tree, path, true);

            FolderVisibilitySettings.SetFolderHidden(path, true);

            AssertFolderVisibility(columns, tree, path, false);
            AssertFolderVisibility(columns, tree, AddressablesFolderVisibility.FolderPath, true);
            Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(folderGuid));
            Assert.That(AssetDatabase.AssetPathToGUID(childPath), Is.EqualTo(childGuid));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(childPath), Is.Not.Null);
            Assert.That(AssetDatabase.FindAssets("", new[] { path }), Does.Contain(childGuid));

            FolderVisibilitySettings.Enabled = false;
            AssertFolderVisibility(columns, tree, path, true);
            FolderVisibilitySettings.Enabled = true;
            AssertFolderVisibility(columns, tree, path, false);
            FolderVisibilitySettings.SetFolderHidden(path, false);
            AssertFolderVisibility(columns, tree, path, true);
        }

        [TestCase("Assets/StreamingAssets", "OneColumn")]
        [TestCase("Assets/StreamingAssets", "TwoColumns")]
        [TestCase("Assets/FolderVisibilityTestGenerated", "OneColumn")]
        [TestCase("Assets/FolderVisibilityTestGenerated", "TwoColumns")]
        public void SearchResultsFollowFolderAndMasterToggles(string path, string layout)
        {
            if (!FolderVisibilitySettings.IsBuiltInFolder(path))
                Assert.That(FolderVisibilitySettings.AddFolder(path), Is.True);
            FolderVisibilitySettings.SetFolderHidden(path, false);
            string childPath = CreateAdditionalProbe(path);
            EditorWindow browser = CreateBrowser(layout);
            Invoke(browser, "SetSearch", childPath.Substring(childPath.LastIndexOf('/') + 1));
            Assert.That(ListPaths(browser), Does.Contain(childPath));

            FolderVisibilitySettings.SetFolderHidden(path, true);
            Assert.That(ListPaths(browser), Has.None.EqualTo(childPath));
            FolderVisibilitySettings.Enabled = false;
            Assert.That(ListPaths(browser), Does.Contain(childPath));
            FolderVisibilitySettings.Enabled = true;
            Assert.That(ListPaths(browser), Has.None.EqualTo(childPath));
            FolderVisibilitySettings.SetFolderHidden(path, false);
            Assert.That(ListPaths(browser), Does.Contain(childPath));
        }

        [Test]
        public void MasterToggleRestoresFoldersAndReappliesTheirChoicesInBothLayouts()
        {
            CreateStreamingAssetsProbe();
            EditorWindow columns = CreateBrowser("TwoColumns");
            EditorWindow tree = CreateBrowser("OneColumn");
            FolderVisibilitySettings.SetFolderHidden(AddressablesFolderVisibility.FolderPath, true);
            FolderVisibilitySettings.SetFolderHidden(StreamingAssetsPath, true);
            AssertFolderVisibility(columns, tree, AddressablesFolderVisibility.FolderPath, false);
            AssertFolderVisibility(columns, tree, StreamingAssetsPath, false);

            FolderVisibilitySettings.Enabled = false;

            AssertFolderVisibility(columns, tree, AddressablesFolderVisibility.FolderPath, true);
            AssertFolderVisibility(columns, tree, StreamingAssetsPath, true);

            FolderVisibilitySettings.Enabled = true;

            AssertFolderVisibility(columns, tree, AddressablesFolderVisibility.FolderPath, false);
            AssertFolderVisibility(columns, tree, StreamingAssetsPath, false);
        }

        [TestCase("Assets/AddressableAssetsData")]
        [TestCase("Assets/StreamingAssets")]
        public void HidingTheCurrentlyBrowsedFolderReturnsTheListToAssets(string path)
        {
            string childPath = path == AddressablesFolderVisibility.FolderPath ? probePath : CreateAdditionalProbe(path);
            EditorWindow browser = CreateBrowser("TwoColumns");
            EntityId folderId = AssetDatabase.LoadMainAssetAtPath(path).GetEntityId();
            Invoke(browser, "ShowFolderContents", folderId, false);
            Assert.That(ListPaths(browser), Does.Contain(childPath));

            FolderVisibilitySettings.SetFolderHidden(path, true);

            CollectionAssert.AreEqual(new[] { "Assets" }, (string[])Member(Member(browser, "m_SearchFilter"), "folders"));
            Assert.That(ListPaths(browser), Has.None.EqualTo(childPath));
            Assert.That(AddressablesFolderVisibility.IsPatched, Is.True, AddressablesFolderVisibility.PatchError);
        }

        [Test]
        public void HidingASelectedDescendantInOneColumnResetsTheSearchScopeWithoutChangingSelection()
        {
            EditorWindow browser = CreateBrowser("OneColumn");
            Object selectedFolder = AssetDatabase.LoadMainAssetAtPath(probePath);
            Selection.activeObject = selectedFolder;
            Invoke(Member(browser, "m_AssetTree"), "SetSelection", new[] { selectedFolder.GetEntityId() }, true);
            Invoke(browser, "ResetViews");
            CollectionAssert.AreEqual(new[] { probePath }, (string[])Member(Member(browser, "m_SearchFilter"), "folders"));
            Assert.That(TreePaths(browser, "m_AssetTree"), Does.Contain(probePath));

            AddressablesFolderVisibility.ShowFolder = false;

            CollectionAssert.AreEqual(new[] { "Assets" }, (string[])Member(Member(browser, "m_SearchFilter"), "folders"));
            Assert.That(TreePaths(browser, "m_AssetTree"), Has.None.EqualTo(probePath));
            Assert.That(ListPaths(browser), Has.None.EqualTo(probePath));
            Assert.That(Selection.activeObject, Is.EqualTo(selectedFolder));
            Assert.That(AddressablesFolderVisibility.IsPatched, Is.True, AddressablesFolderVisibility.PatchError);
        }

        [Test]
        public void ListsOwnedByOtherEditorWindowsRemainUnfiltered()
        {
            AddressablesFolderVisibility.ShowFolder = false;
            var owner = ScriptableObject.CreateInstance<FolderVisibilityTestWindow>();
            ownedWindows.Add(owner);
            var assembly = typeof(UnityEditor.Editor).Assembly;
            Type areaType = assembly.GetType("UnityEditor.ObjectListArea", true);
            object state = Activator.CreateInstance(assembly.GetType("UnityEditor.ObjectListAreaState", true));
            object area = Activator.CreateInstance(areaType, InstanceFlags, null, new[] { state, owner, (object)false }, null);
            try
            {
                object filter = Activator.CreateInstance(assembly.GetType("UnityEditor.SearchFilter", true));
                filter.GetType().GetProperty("folders", InstanceFlags).SetValue(filter, new[] { "Assets" });
                Type hierarchyType = assembly.GetType("UnityEditor.HierarchyType", true);
                Invoke(area, "Init", new Rect(0, 0, 640, 480), Enum.Parse(hierarchyType, "Assets"), filter, false);
                Assert.That(AreaPaths(area), Does.Contain(AddressablesFolderVisibility.FolderPath));
            }
            finally
            {
                Invoke(area, "OnDestroy");
            }
        }

        string CreateAdditionalProbe(string path)
        {
            if (!AssetDatabase.IsValidFolder(path))
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", path.Substring("Assets/".Length)), Is.Not.Empty);
                additionalRoots.Add(path);
            }
            string probeName = "FolderVisibilityTest_" + Guid.NewGuid().ToString("N");
            string childPath = path + "/" + probeName;
            Assert.That(AssetDatabase.CreateFolder(path, probeName), Is.Not.Empty);
            additionalProbes.Add(childPath);
            return childPath;
        }

        void CreateStreamingAssetsProbe()
        {
            if (!AssetDatabase.IsValidFolder(StreamingAssetsPath))
            {
                Assert.That(AssetDatabase.CreateFolder("Assets", "StreamingAssets"), Is.Not.Empty);
                createdStreamingAssetsRoot = true;
            }
            string probeName = "FolderVisibilityTest_" + Guid.NewGuid().ToString("N");
            streamingAssetsProbePath = StreamingAssetsPath + "/" + probeName;
            Assert.That(AssetDatabase.CreateFolder(StreamingAssetsPath, probeName), Is.Not.Empty);
        }

        static void AssertFolderVisibility(EditorWindow columns, EditorWindow tree, string path, bool visible)
        {
            Assert.That(TreePaths(columns, "m_FolderTree").Contains(path), Is.EqualTo(visible), path + " in folder tree");
            Assert.That(TreePaths(tree, "m_AssetTree").Contains(path), Is.EqualTo(visible), path + " in asset tree");
            Assert.That(ListPaths(columns).Contains(path), Is.EqualTo(visible), path + " in asset list");
        }

        EditorWindow CreateBrowser(string layout)
        {
            var browser = (EditorWindow)ScriptableObject.CreateInstance(BrowserType);
            ownedWindows.Add(browser);
            browser.position = new Rect(100, 100, 640, 480);
            Invoke(browser, "Init");
            Type viewMode = BrowserType.GetNestedType("ViewMode", BindingFlags.NonPublic);
            Invoke(browser, "InitViewMode", Enum.Parse(viewMode, layout));
            if (layout == "TwoColumns")
                Invoke(browser, "SelectAssetsFolder");
            string treeField = layout == "TwoColumns" ? "m_FolderTree" : "m_AssetTree";
            object data = Member(Member(browser, treeField), "data");
            EntityId assetsId = AssetDatabase.LoadMainAssetAtPath("Assets").GetEntityId();
            Invoke(data, "SetExpanded", assetsId, true);
            return browser;
        }

        static List<string> TreePaths(EditorWindow browser, string treeField)
        {
            object data = Member(Member(browser, treeField), "data");
            var paths = new List<string>();
            foreach (TreeViewItem<EntityId> row in (IEnumerable)Invoke(data, "GetRows"))
                paths.Add(AssetDatabase.GetAssetPath(row.id));
            return paths;
        }

        static List<string> ListPaths(EditorWindow browser) => AreaPaths(Member(browser, "m_ListArea"));

        static List<string> AreaPaths(object area)
        {
            object hierarchy = Member(Member(area, "m_LocalAssets"), "m_FilteredHierarchy");
            var paths = new List<string>();
            foreach (object result in (IEnumerable)Member(hierarchy, "results"))
                paths.Add(AssetDatabase.GUIDToAssetPath((string)Member(result, "guid")));
            return paths;
        }

        static object Member(object instance, string name)
        {
            for (Type type = instance.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(instance);
                PropertyInfo property = type.GetProperty(name, InstanceFlags | BindingFlags.DeclaredOnly);
                if (property != null)
                    return property.GetValue(instance);
            }
            throw new MissingMemberException(instance.GetType().FullName, name);
        }

        static object Invoke(object instance, string name, params object[] arguments)
        {
            foreach (MethodInfo method in instance.GetType().GetMethods(InstanceFlags))
            {
                if (method.Name != name)
                    continue;
                ParameterInfo[] parameters = method.GetParameters();
                if (parameters.Length != arguments.Length)
                    continue;
                bool matches = true;
                for (int i = 0; i < parameters.Length; i++)
                    matches &= parameters[i].ParameterType.IsInstanceOfType(arguments[i]);
                if (matches)
                    return method.Invoke(instance, arguments);
            }
            throw new MissingMethodException(instance.GetType().FullName, name);
        }
    }

    sealed class FolderVisibilityPreferencesSnapshot : IDisposable
    {
        readonly bool hadPreference;
        readonly string previousPreference;

        internal FolderVisibilityPreferencesSnapshot()
        {
            hadPreference = EditorPrefs.HasKey(FolderVisibilitySettings.PreferenceKey);
            previousPreference = EditorPrefs.GetString(FolderVisibilitySettings.PreferenceKey);
            EditorPrefs.DeleteKey(FolderVisibilitySettings.PreferenceKey);
            FolderVisibilitySettings.Reload();
        }

        public void Dispose()
        {
            if (hadPreference)
                EditorPrefs.SetString(FolderVisibilitySettings.PreferenceKey, previousPreference);
            else
                EditorPrefs.DeleteKey(FolderVisibilitySettings.PreferenceKey);
            FolderVisibilitySettings.Reload();
            AddressablesFolderVisibility.RefreshProjectWindows();
        }
    }

    public sealed class FolderVisibilityTestWindow : EditorWindow { }
}
