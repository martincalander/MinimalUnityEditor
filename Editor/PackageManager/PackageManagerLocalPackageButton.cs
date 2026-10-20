using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace MinimalEditor.PackageManager.Editor
{
    internal static class PackageManagerLocalPackageButton
    {
        internal const string PackageManagerWindowTypeName = "UnityEditor.PackageManager.UI.PackageManagerWindow";
        internal const string DetailHeaderTypeName = "UnityEditor.PackageManager.UI.Internal.PackageDetailsHeader";
        internal const string DetailHeaderRefreshMethodName = "Refresh";
        internal const string DetailHeaderPackageFieldName = "m_Package";
        internal const string ToolbarTypeName = "UnityEditor.PackageManager.UI.Internal.PackageToolbar";
        internal const string ToolbarName = "packageToolbar";
        internal const string InProjectActionText = "In Project";
        internal const string ButtonName = "minimal-editor-go-to-local-package-button";
        internal const string ButtonText = "Go To Local Package";
        internal const string ButtonTooltip = "Select the matching local package installed under this project's Packages folder.";

        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        internal static void Refresh(object detailHeader, object package)
        {
            if (!(detailHeader is VisualElement header))
                return;

            var toolbar = FindPackageToolbar(header);
            if (toolbar == null)
                return;

            RemoveExistingButton(toolbar);
            if (!PackageManagerAssetStoreDuplicateFilter.TryGetLocalPackageForAssetStorePackage(
                    package,
                    out string packageName,
                    out string resolvedPath))
            {
                return;
            }

            var button = CreateButton(packageName, resolvedPath);
            TryInsertButtonAfterToolbarAction(toolbar, button, InProjectActionText);
        }

        internal static void RefreshOpenPackageManagerWindows()
        {
            try
            {
                var packageManagerWindowType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerWindowTypeName);
                if (packageManagerWindowType == null)
                    return;

                var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
                foreach (var window in windows)
                {
                    if (window == null || window.GetType() != packageManagerWindowType)
                        continue;

                    foreach (var header in FindDescendants(window.rootVisualElement, IsDetailHeader))
                        Refresh(header, GetPackageFromDetailHeader(header));
                }
            }
            catch
            {
                // The normal Package Manager refresh hook will try again on the next selection change.
            }
        }

        internal static bool TryInsertButtonAfterToolbarAction(
            VisualElement toolbar,
            Button button,
            string actionText)
        {
            if (toolbar == null || button == null || string.IsNullOrEmpty(actionText))
                return false;

            var anchor = FindDirectToolbarChildContainingText(toolbar, actionText);
            if (anchor == null)
                return false;

            int anchorIndex = toolbar.IndexOf(anchor);
            if (anchorIndex < 0)
                return false;

            toolbar.Insert(anchorIndex + 1, button);
            return true;
        }

        internal static bool GoToLocalPackage(string packageName, string resolvedPath)
        {
            if (string.IsNullOrEmpty(packageName))
                return false;

            if (TrySelectPackageManagerPackage(packageName))
                return true;

            return TryPingPackageJson(resolvedPath);
        }

        static Button CreateButton(string packageName, string resolvedPath)
        {
            var button = new Button(() => GoToLocalPackage(packageName, resolvedPath))
            {
                name = ButtonName,
                text = ButtonText,
                tooltip = ButtonTooltip
            };
            button.style.marginLeft = 2;
            button.style.flexShrink = 0;
            return button;
        }

        static bool TrySelectPackageManagerPackage(string packageName)
        {
            try
            {
                var pageManager = PackageManagerAssetStoreDuplicatePatch.ResolvePackageManagerService(
                    PackageManagerAssetStoreDuplicatePatch.PageManagerInterfaceName);
                var inProjectPage = PackageManagerAssetStoreDuplicatePatch.GetPage(
                    pageManager,
                    PackageManagerAssetStoreDuplicatePatch.InProjectPageId);
                if (pageManager == null || inProjectPage == null)
                    return false;

                pageManager
                    .GetType()
                    .GetProperty(PackageManagerAssetStoreDuplicatePatch.PageActivePagePropertyName, AnyInstance)
                    ?.SetValue(pageManager, inProjectPage, null);

                inProjectPage
                    .GetType()
                    .GetMethod(PackageManagerAssetStoreDuplicatePatch.PageLoadMethodName, AnyInstance, null, new[] { typeof(string) }, null)
                    ?.Invoke(inProjectPage, new object[] { packageName });

                var setNewSelectionMethod = inProjectPage
                    .GetType()
                    .GetMethod(PackageManagerAssetStoreDuplicatePatch.PageSetNewSelectionMethodName, AnyInstance, null, new[] { typeof(string), typeof(bool) }, null);
                if (setNewSelectionMethod == null)
                    return false;

                return setNewSelectionMethod.Invoke(inProjectPage, new object[] { packageName, false }) is bool selected &&
                       selected;
            }
            catch
            {
                return false;
            }
        }

        static bool TryPingPackageJson(string resolvedPath)
        {
            if (string.IsNullOrEmpty(resolvedPath))
                return false;

            try
            {
                string packageJsonPath = Path.Combine(resolvedPath, "package.json");
                if (!File.Exists(packageJsonPath))
                    return false;

                string projectRoot = GetProjectRoot();
                if (string.IsNullOrEmpty(projectRoot))
                    return false;

                string fullPackageJsonPath = Path.GetFullPath(packageJsonPath).Replace('\\', '/');
                string fullProjectRoot = Path.GetFullPath(projectRoot).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Replace('\\', '/');
                if (!fullPackageJsonPath.StartsWith(fullProjectRoot + "/", PathComparison))
                    return false;

                string assetPath = fullPackageJsonPath.Substring(fullProjectRoot.Length + 1);
                var packageJson = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (packageJson == null)
                    return false;

                EditorGUIUtility.PingObject(packageJson);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static VisualElement FindPackageToolbar(VisualElement header)
        {
            foreach (var element in FindDescendants(header, IsPackageToolbar))
                return element;

            return null;
        }

        static bool IsPackageToolbar(VisualElement element)
        {
            return element != null &&
                   (element.name == ToolbarName || element.GetType().FullName == ToolbarTypeName);
        }

        static bool IsDetailHeader(VisualElement element)
        {
            return element != null && element.GetType().FullName == DetailHeaderTypeName;
        }

        static object GetPackageFromDetailHeader(VisualElement header)
        {
            try
            {
                return header
                    ?.GetType()
                    .GetField(DetailHeaderPackageFieldName, AnyInstance)
                    ?.GetValue(header);
            }
            catch
            {
                return null;
            }
        }

        static VisualElement FindDirectToolbarChildContainingText(VisualElement toolbar, string text)
        {
            foreach (var element in FindDescendants(toolbar, IsTextElementWithText))
            {
                var directChild = GetDirectChild(toolbar, element);
                if (directChild != null)
                    return directChild;
            }

            return null;

            bool IsTextElementWithText(VisualElement element)
            {
                return element is TextElement textElement &&
                       string.Equals(textElement.text, text, StringComparison.Ordinal);
            }
        }

        static VisualElement GetDirectChild(VisualElement ancestor, VisualElement descendant)
        {
            var current = descendant;
            while (current != null && current.parent != null && current.parent != ancestor)
                current = current.parent;

            return current != null && current.parent == ancestor ? current : null;
        }

        static void RemoveExistingButton(VisualElement toolbar)
        {
            var existingButton = toolbar.Q<Button>(ButtonName);
            existingButton?.RemoveFromHierarchy();
        }

        static System.Collections.Generic.IEnumerable<VisualElement> FindDescendants(
            VisualElement root,
            Func<VisualElement, bool> predicate)
        {
            if (root == null || predicate == null)
                yield break;

            var stack = new System.Collections.Generic.Stack<VisualElement>();
            stack.Push(root);
            while (stack.Count > 0)
            {
                var element = stack.Pop();
                if (predicate(element))
                    yield return element;

                for (int i = element.childCount - 1; i >= 0; i--)
                    stack.Push(element[i]);
            }
        }

        static string GetProjectRoot()
        {
            try
            {
                return Directory.GetParent(Application.dataPath)?.FullName ?? string.Empty;
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
