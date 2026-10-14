using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

[assembly: InternalsVisibleTo("Essentials.ProjectFolderVisibility.Editor.Tests")]

namespace Essentials.ProjectFolderVisibility.Editor
{
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
    internal static partial class AddressablesFolderVisibility
    {
        internal const string FolderPath = FolderVisibilitySettings.AddressablesPath;
        internal const string MenuPath = "Tools/Minimal Editor/Project Folders/Hide Selected Folders";
        const string HarmonyId = "com.essentials.projectfoldervisibility";
        const BindingFlags InstanceFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static object s_Harmony;
        static BrowserMembers s_Members;

        internal static bool IsPatched { get; private set; }
        internal static string PatchError { get; private set; }

        internal static bool ShowFolder
        {
            get => !FolderVisibilitySettings.IsFolderHidden(FolderPath);
            set => FolderVisibilitySettings.SetFolderHidden(FolderPath, !value);
        }

        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        internal static void Initialize()
        {
            FolderVisibilitySettings.Changed -= OnSettingsChanged;
            FolderVisibilitySettings.Changed += OnSettingsChanged;
            EditorApplication.delayCall -= Install;
            EditorApplication.delayCall += Install;
            EditorApplication.quitting -= Uninstall;
            EditorApplication.quitting += Uninstall;
        }

        [MenuItem(MenuPath, false, 2100)]
        static void Toggle() => FolderVisibilitySettings.Enabled = !FolderVisibilitySettings.Enabled;

        [MenuItem(MenuPath, true)]
        static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, FolderVisibilitySettings.Enabled);
            return true;
        }

        static void Install()
        {
            if (IsPatched)
                return;
            try
            {
                s_Members = new BrowserMembers();
                // The shared CoreCLR Harmony build targets .NET 10; Editor scripts target .NET Standard.
                var harmonyType = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: true);
                s_Harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                Patch(s_Members.FolderFetch, typeof(AddressablesFolderVisibility), postfix: nameof(FilterTree));
                Patch(s_Members.AssetFetch, typeof(AddressablesFolderVisibility), postfix: nameof(FilterTree));
                Patch(s_Members.UpdateAssets, typeof(AddressablesFolderVisibility), prefix: nameof(FilterList));
                FolderVisibilityControls.Install(s_Members.BrowserType);
                IsPatched = true;
                PatchError = null;
                RefreshProjectWindows();
            }
            catch (Exception exception)
            {
                DisableAfterFailure(exception);
            }
        }

        internal static void Patch(MethodInfo target, Type patchType, string prefix = null, string postfix = null)
        {
            var harmonyType = s_Harmony.GetType();
            var harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", throwOnError: true);
            var constructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) }) ??
                              throw new MissingMethodException(harmonyMethodType.FullName, ".ctor(MethodInfo)");
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            object CreatePatch(string name)
            {
                if (name == null)
                    return null;
                var method = patchType.GetMethod(name, flags) ??
                             throw new MissingMethodException(patchType.FullName, name);
                return constructor.Invoke(new object[] { method });
            }
            var patch = harmonyType.GetMethod("Patch", new[]
            {
                typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType
            }) ?? throw new MissingMethodException(harmonyType.FullName, "Patch");
            patch.Invoke(s_Harmony, new[] { (object)target, CreatePatch(prefix), CreatePatch(postfix), null, null });
        }

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void Uninstall()
        {
            EditorApplication.delayCall -= Install;
            EditorApplication.delayCall -= RefreshProjectWindows;
            EditorApplication.quitting -= Uninstall;
            FolderVisibilitySettings.Changed -= OnSettingsChanged;
            IsPatched = false;
            RemovePatches();
            s_Members = null;
        }

        static void RemovePatches()
        {
            try
            {
                if (s_Harmony == null)
                    return;
                var method = s_Harmony.GetType().GetMethod("UnpatchAll", new[] { typeof(string) }) ??
                             throw new MissingMethodException(s_Harmony.GetType().FullName, "UnpatchAll");
                method.Invoke(s_Harmony, new object[] { HarmonyId });
            }
            catch (Exception exception)
            {
                Debug.LogWarning("Project Folder Visibility could not remove its Editor hooks. " +
                                 exception.GetBaseException().Message);
            }
            finally
            {
                s_Harmony = null;
            }
        }

        internal static void DisableAfterFailure(Exception exception)
        {
            if (PatchError != null)
                return;
            PatchError = exception.GetBaseException().Message;
            IsPatched = false;
            RemovePatches();
            Debug.LogWarning("Project Folder Visibility could not filter this Unity version. " +
                             "Folders will remain visible. " + PatchError);
            EditorApplication.delayCall += RefreshProjectWindows;
        }

        static void OnSettingsChanged()
        {
            Menu.SetChecked(MenuPath, FolderVisibilitySettings.Enabled);
            EditorApplication.delayCall -= RefreshProjectWindows;
            if (Event.current != null)
                EditorApplication.delayCall += RefreshProjectWindows;
            else
                RefreshProjectWindows();
        }

        internal static bool IsHiddenPath(string path) => FolderVisibilitySettings.IsHiddenPath(path);

        static bool ShouldFilter => IsPatched && FolderVisibilitySettings.Enabled;

        // These data sources also serve other Editor windows. Only alter a Project browser's data.
        static void FilterTree(object __instance)
        {
            if (!ShouldFilter)
                return;
            try
            {
                if (!s_Members.IsProjectTree(__instance))
                    return;
                var field = __instance.GetType() == s_Members.FolderFetch.DeclaringType
                    ? s_Members.FolderRows : s_Members.AssetRows;
                var rows = (IList<TreeViewItem<EntityId>>)field.GetValue(__instance);
                for (var i = rows.Count - 1; i >= 0; i--)
                {
                    var item = rows[i];
                    if (!IsHiddenPath(AssetDatabase.GetAssetPath(item.id)))
                        continue;
                    item.parent?.children?.Remove(item);
                    rows.RemoveAt(i);
                }
            }
            catch (Exception exception)
            {
                DisableAfterFailure(exception);
            }
        }

        static void FilterList(object __instance)
        {
            if (!ShouldFilter)
                return;
            try
            {
                var list = s_Members.GroupOwner.GetValue(__instance);
                if (!s_Members.BrowserType.IsInstanceOfType(s_Members.ListOwner.GetValue(list)))
                    return;
                var hierarchy = s_Members.GroupHierarchy.GetValue(__instance);
                if (hierarchy == null)
                    return;
                FilterResults(hierarchy, s_Members.Results);
                FilterResults(hierarchy, s_Members.VisibleResults);
            }
            catch (Exception exception)
            {
                DisableAfterFailure(exception);
            }
        }

        static void FilterResults(object hierarchy, FieldInfo field)
        {
            var results = (Array)field.GetValue(hierarchy);
            if (results == null || results.Length == 0)
                return;
            var kept = new List<object>(results.Length);
            foreach (var result in results)
            {
                var guid = (string)s_Members.ResultGuid.GetValue(result);
                if (!IsHiddenPath(AssetDatabase.GUIDToAssetPath(guid)))
                    kept.Add(result);
            }
            if (kept.Count == results.Length)
                return;
            var filtered = Array.CreateInstance(results.GetType().GetElementType(), kept.Count);
            for (var i = 0; i < kept.Count; i++)
                filtered.SetValue(kept[i], i);
            field.SetValue(hierarchy, filtered);
        }

        internal static void RefreshProjectWindows()
        {
            if (s_Members == null)
                return;
            try
            {
                foreach (var window in Resources.FindObjectsOfTypeAll(s_Members.BrowserType))
                {
                    var browser = (EditorWindow)window;
                    if (!(bool)s_Members.Initialized.Invoke(browser, null))
                        continue;
                    // A folder already being browsed must not leave the content panel stranded.
                    var moveToAssets = false;
                    var hasFolderTree = s_Members.HasFolderTree(browser);
                    if (ShouldFilter)
                    {
                        var filter = s_Members.SearchFilter.GetValue(browser);
                        var folders = (string[])s_Members.SearchFolders.GetValue(filter);
                        moveToAssets = folders != null && Array.Exists(folders, IsHiddenPath);
                        if (moveToAssets && hasFolderTree)
                            s_Members.SelectAssets.Invoke(browser, null);
                    }
                    s_Members.ResetViews.Invoke(browser, null);
                    if (moveToAssets && !hasFolderTree)
                    {
                        var filter = s_Members.SearchFilter.GetValue(browser);
                        s_Members.SearchFolders.SetValue(filter, new[] { "Assets" });
                        s_Members.InitListArea.Invoke(browser, null);
                    }
                    browser.Repaint();
                }
            }
            catch (Exception exception)
            {
                if (IsPatched)
                    DisableAfterFailure(exception);
            }
        }

        sealed class BrowserMembers
        {
            internal readonly Type BrowserType;
            internal readonly MethodInfo FolderFetch, AssetFetch, UpdateAssets, ResetViews, Initialized, SelectAssets, InitListArea;
            internal readonly FieldInfo FolderRows, AssetRows, GroupOwner, ListOwner, GroupHierarchy, Results, VisibleResults;
            internal readonly FieldInfo SearchFilter;
            internal readonly PropertyInfo SearchFolders, ResultGuid;
            readonly FieldInfo m_Browsers, m_FolderTree, m_AssetTree;
            readonly PropertyInfo m_TreeData;

            internal BrowserMembers()
            {
                var assembly = typeof(UnityEditor.Editor).Assembly;
                BrowserType = assembly.GetType("UnityEditor.ProjectBrowser", true);
                var folder = assembly.GetType("UnityEditor.ProjectBrowserColumnOneTreeViewDataSource", true);
                var asset = assembly.GetType("UnityEditor.AssetsTreeViewDataSource", true);
                var list = assembly.GetType("UnityEditor.ObjectListArea", true);
                var group = list.GetNestedType("LocalGroup", BindingFlags.NonPublic | BindingFlags.Public);
                var hierarchy = assembly.GetType("UnityEditor.FilteredHierarchy", true);
                FolderFetch = RequireMethod(folder, "FetchData");
                AssetFetch = RequireMethod(asset, "FetchData");
                UpdateAssets = RequireMethod(group, "UpdateAssets");
                ResetViews = RequireMethod(BrowserType, "ResetViews");
                Initialized = RequireMethod(BrowserType, "Initialized");
                SelectAssets = RequireMethod(BrowserType, "SelectAssetsFolder");
                InitListArea = RequireMethod(BrowserType, "InitListArea");
                FolderRows = RequireField(folder, "m_Rows");
                AssetRows = RequireField(asset, "m_Rows");
                GroupOwner = RequireField(group, "m_Owner");
                ListOwner = RequireField(list, "m_Owner");
                GroupHierarchy = RequireField(group, "m_FilteredHierarchy");
                Results = RequireField(hierarchy, "m_Results");
                VisibleResults = RequireField(hierarchy, "m_VisibleItems");
                ResultGuid = RequireProperty(Results.FieldType.GetElementType(), "guid");
                m_Browsers = RequireField(BrowserType, "s_ProjectBrowsers");
                m_FolderTree = RequireField(BrowserType, "m_FolderTree");
                m_AssetTree = RequireField(BrowserType, "m_AssetTree");
                m_TreeData = RequireProperty(m_FolderTree.FieldType, "data");
                SearchFilter = RequireField(BrowserType, "m_SearchFilter");
                SearchFolders = RequireProperty(SearchFilter.FieldType, "folders");
            }

            internal bool IsProjectTree(object source)
            {
                foreach (var browser in (IEnumerable)m_Browsers.GetValue(null))
                {
                    var folderTree = m_FolderTree.GetValue(browser);
                    var assetTree = m_AssetTree.GetValue(browser);
                    if (folderTree != null && ReferenceEquals(m_TreeData.GetValue(folderTree), source) ||
                        assetTree != null && ReferenceEquals(m_TreeData.GetValue(assetTree), source))
                        return true;
                }
                return false;
            }

            internal bool HasFolderTree(object browser) => m_FolderTree.GetValue(browser) != null;

            static FieldInfo RequireField(Type type, string name)
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    var field = current.GetField(name, InstanceFlags | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (field != null)
                        return field;
                }
                throw new MissingFieldException(type.FullName, name);
            }

            static PropertyInfo RequireProperty(Type type, string name)
            {
                for (var current = type; current != null; current = current.BaseType)
                {
                    var property = current.GetProperty(name, InstanceFlags | BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (property != null)
                        return property;
                }
                throw new MissingMemberException(type.FullName, name);
            }

            static MethodInfo RequireMethod(Type type, string name) =>
                type.GetMethod(name, InstanceFlags, null, Type.EmptyTypes, null) ??
                throw new MissingMethodException(type.FullName, name);
        }
    }
}
