using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MartinCalander.Essentials.MenuItem.Editor
{
    [InitializeOnLoad]
    internal static class MenuItemHider
    {
        const string ApplyProxyMenuPath = "Tools/Essentials/Internal/Apply Menu Item Hider";

        static bool s_active = MenuItemModuleSettings.ModuleEnabled;
        static Func<bool> s_activeInput;

        static readonly List<string> s_manualMenuItemsToHide = new List<string>();
        static bool s_useManualMenuItems;
        static Func<IEnumerable<string>> s_menuItemsInput;

        static bool s_isApplying;
        static bool s_applyQueued;
        static bool s_forceApplyQueued;
        static bool s_failSafeMode;
        static bool s_loggedUnsupportedWarning;

        // Do not invoke Unity's internal menu rebuild from this module. Rebuilding menus from the
        // projectChanged/import path has caused native editor crashes in Unity's RebuildOSMainMenu.
        static readonly MethodInfo RemoveMenuItemMethod =
            typeof(Menu).GetMethod("RemoveMenuItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

        static readonly MethodInfo ExtractSubmenusMethod =
            typeof(Menu).GetMethod(
                "ExtractSubmenus",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
        static readonly MethodInfo GetMenuItemsMethod =
            typeof(Menu).GetMethod(
                "GetMenuItems",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(bool), typeof(bool) },
                null);
        static readonly PropertyInfo ScriptingMenuItemPathProperty =
            GetMenuItemsMethod?.ReturnType.GetElementType()?.GetProperty(
                "path",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        static readonly string[] MenuRoots =
        {
            "File",
            "Edit",
            "Assets",
            "GameObject",
            "Component",
            "Window",
            "Jobs",
            "Tools",
            "Help"
        };

        static MenuItemHider()
        {
            if (!MenuItemModuleSettings.IsSupportedInCurrentEditor)
            {
                LogUnsupportedVersionWarningIfNeeded();
                return;
            }

            QueueApply(force: true);
            EditorApplication.projectChanged += OnProjectChanged;
        }

        internal static bool Active
        {
            get => s_active;
            set => SetActive(value);
        }

        internal static Func<bool> ActiveInput
        {
            get => s_activeInput;
            set => SetActiveInput(value);
        }

        internal static IReadOnlyList<string> MenuItemsToHide => s_manualMenuItemsToHide;

        internal static Func<IEnumerable<string>> MenuItemsInput
        {
            get => s_menuItemsInput;
            set => SetMenuItemsInput(value);
        }

        internal static void SetActive(bool active)
        {
            if (!MenuItemModuleSettings.IsSupportedInCurrentEditor)
            {
                MenuItemModuleSettings.ModuleEnabled = active;
                s_active = false;
                LogUnsupportedVersionWarningIfNeeded();
                return;
            }

            if (s_active == active && MenuItemModuleSettings.ModuleEnabled == active) return;

            s_active = active;
            MenuItemModuleSettings.ModuleEnabled = active;
            Apply();
        }

        internal static void SetActiveInput(Func<bool> activeInput)
        {
            s_activeInput = activeInput;
            Apply();
        }

        internal static void ClearActiveInput()
        {
            if (s_activeInput == null) return;

            s_activeInput = null;
            Apply();
        }

        internal static void SetMenuItems(IEnumerable<string> menuItems)
        {
            if (menuItems == null)
            {
                s_useManualMenuItems = false;
                s_manualMenuItemsToHide.Clear();
                Apply();
                return;
            }

            s_useManualMenuItems = true;
            s_manualMenuItemsToHide.Clear();
            s_manualMenuItemsToHide.AddRange(MenuItemPatternMatcher.SanitizeEntries(menuItems));
            Apply();
        }

        internal static void SetMenuItemsInput(Func<IEnumerable<string>> menuItemsInput)
        {
            s_menuItemsInput = menuItemsInput;
            Apply();
        }

        internal static void ClearMenuItemsInput()
        {
            if (s_menuItemsInput == null) return;

            s_menuItemsInput = null;
            Apply();
        }

        internal static void Apply()
        {
            QueueApply(force: true);
        }

        internal static void ApplyWithMenuProxy()
        {
            QueueApply(force: true);
        }

        [UnityEditor.MenuItem(ApplyProxyMenuPath, false, 30000)]
        static void ApplyFromProxyMenuItem()
        {
            QueueApply(force: true);
        }

        static void OnProjectChanged()
        {
            QueueApply(force: false);
        }

        static void QueueApply(bool force)
        {
            if (!MenuItemModuleSettings.IsSupportedInCurrentEditor)
            {
                LogUnsupportedVersionWarningIfNeeded();
                return;
            }

            if (force)
            {
                s_forceApplyQueued = true;
            }

            if (s_applyQueued || s_failSafeMode)
            {
                return;
            }

            s_applyQueued = true;
            EditorApplication.delayCall += FlushQueuedApply;
        }

        static void FlushQueuedApply()
        {
            s_applyQueued = false;

            var force = s_forceApplyQueued;
            s_forceApplyQueued = false;

            if (s_failSafeMode)
            {
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                QueueApply(force);
                return;
            }

            ApplyCore(force);
        }

        static void ApplyCore(bool force)
        {
            if (s_isApplying || s_failSafeMode) return;

            var isActive = ResolveActiveState();
            if (!isActive && !force) return;

            s_isApplying = true;
            try
            {
                if (!isActive) return;
                if (RemoveMenuItemMethod == null) return;

                var items = ResolveMenuPaths();
                if (items == null || items.Count == 0) return;

                var menuRootsToScan = BuildMenuRootSet(items);
                var allMenuPaths = EnumerateAllMenuPaths(menuRootsToScan);

                for (var i = 0; i < items.Count; i++)
                {
                    var path = items[i]?.Trim();
                    if (string.IsNullOrEmpty(path)) continue;

                    if (path.EndsWith("*", StringComparison.Ordinal))
                    {
                        var wildcardPrefix = path.Substring(0, path.Length - 1).Trim();
                        var wildcardMenuPath = wildcardPrefix.TrimEnd('/');
                        if (CanRemoveDirectPath(wildcardMenuPath))
                        {
                            TryRemoveMenuItem(wildcardMenuPath);
                            TryRemoveMenuItem(wildcardMenuPath + "/");
                        }

                        foreach (var candidate in allMenuPaths)
                        {
                            if (!MenuItemPatternMatcher.IsMatch(candidate, path)) continue;
                            if (!CanRemoveDirectPath(candidate)) continue;
                            TryRemoveMenuItem(candidate);
                        }

                        continue;
                    }

                    if (!CanRemoveDirectPath(path)) continue;
                    TryRemoveMenuItem(path);
                }
            }
            catch (Exception exception)
            {
                EnterFailSafeMode(exception);
            }
            finally
            {
                s_isApplying = false;
            }
        }

        static bool CanRemoveDirectPath(string path)
        {
            var normalizedPath = NormalizeMenuPath(path);
            return !string.IsNullOrEmpty(normalizedPath) && normalizedPath.Contains("/");
        }

        static HashSet<string> BuildMenuRootSet(IReadOnlyList<string> items)
        {
            var roots = new HashSet<string>(MenuRoots, StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < items.Count; i++)
            {
                var path = items[i]?.Trim();
                if (string.IsNullOrEmpty(path)) continue;

                var separatorIndex = path.IndexOf('/');
                if (separatorIndex <= 0) continue;

                var root = path.Substring(0, separatorIndex).Trim();
                if (!string.IsNullOrEmpty(root))
                {
                    roots.Add(root);
                }
            }

            return roots;
        }

        static HashSet<string> EnumerateAllMenuPaths(HashSet<string> menuRoots)
        {
            var allMenuPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (menuRoots == null || menuRoots.Count == 0) return allMenuPaths;

            foreach (var menuRoot in menuRoots)
            {
                AddEnumeratedMenuPaths(menuRoot, allMenuPaths);
            }

            return allMenuPaths;
        }

        static void AddEnumeratedMenuPaths(string menuRoot, HashSet<string> allMenuPaths)
        {
            if (string.IsNullOrWhiteSpace(menuRoot) || allMenuPaths == null) return;

            try
            {
                var submenus = Unsupported.GetSubmenus(menuRoot);
                AddPaths(submenus, allMenuPaths);
            }
            catch
            {
                // Ignore menu roots that Unity fails to enumerate.
            }

            try
            {
                if (ExtractSubmenusMethod != null)
                {
                    var submenus = ExtractSubmenusMethod.Invoke(null, new object[] { menuRoot }) as string[];
                    AddPaths(submenus, allMenuPaths);
                }
            }
            catch
            {
                // Ignore extraction failures for Unity internals.
            }

            try
            {
                if (GetMenuItemsMethod == null || ScriptingMenuItemPathProperty == null) return;

                var itemsObject = GetMenuItemsMethod.Invoke(null, new object[] { menuRoot, false, false });
                var items = itemsObject as Array;
                if (items == null) return;

                for (var i = 0; i < items.Length; i++)
                {
                    var item = items.GetValue(i);
                    var path = ScriptingMenuItemPathProperty.GetValue(item, null) as string;
                    path = path?.Trim();
                    if (!string.IsNullOrEmpty(path))
                    {
                        allMenuPaths.Add(path);
                    }
                }
            }
            catch
            {
                // Ignore menu-item enumeration failures for Unity internals.
            }
        }

        static void AddPaths(string[] paths, HashSet<string> allMenuPaths)
        {
            if (paths == null || allMenuPaths == null) return;

            for (var i = 0; i < paths.Length; i++)
            {
                var path = paths[i];
                if (!string.IsNullOrWhiteSpace(path))
                {
                    allMenuPaths.Add(path);
                }
            }
        }

        static bool ResolveActiveState()
        {
            if (s_activeInput == null) return s_active;

            try
            {
                return s_activeInput.Invoke();
            }
            catch
            {
                return s_active;
            }
        }

        static IReadOnlyList<string> ResolveMenuPaths()
        {
            if (s_menuItemsInput != null)
            {
                try
                {
                    var paths = s_menuItemsInput.Invoke();
                    if (paths != null)
                    {
                        return MenuItemPatternMatcher.SanitizeEntries(paths);
                    }
                }
                catch
                {
                    // Fall back to manual/default paths.
                }
            }

            if (s_useManualMenuItems)
            {
                return s_manualMenuItemsToHide;
            }

            return MenuItemModuleSettings.ResolveBundlePaths();
        }

        static string NormalizeMenuPath(string path)
        {
            return path?.Trim().TrimEnd('/');
        }

        static void EnterFailSafeMode(Exception exception)
        {
            if (s_failSafeMode) return;

            s_failSafeMode = true;
            UnityEngine.Debug.LogWarning(
                "[MenuItemHider] Disabled menu item hiding for this editor session after an unexpected failure. " +
                "Menu changes will be skipped until the next domain reload. " +
                exception.GetType().Name + ": " + exception.Message);
        }

        static void LogUnsupportedVersionWarningIfNeeded()
        {
            if (s_loggedUnsupportedWarning || !MenuItemModuleSettings.StoredModuleEnabled)
            {
                return;
            }

            s_loggedUnsupportedWarning = true;
            var unsupportedMessage = MenuItemModuleSettings.UnsupportedEditorMessage;
            if (string.IsNullOrEmpty(unsupportedMessage))
            {
                return;
            }

            UnityEngine.Debug.LogWarning("[MenuItemHider] " + unsupportedMessage);
        }

        static void TryRemoveMenuItem(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                RemoveMenuItemMethod.Invoke(null, new object[] { path });
            }
            catch
            {
                // Fail silently when menu entries are not removable in this Unity version.
            }
        }
    }

    internal static class MenuItemPatternMatcher
    {
        internal static bool IsUnsupportedEntry(string entry)
        {
            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return false;

            // Unity regenerates Services menu entries internally; hiding them is not reliable.
            if (trimmed.Equals("Services", StringComparison.OrdinalIgnoreCase)) return true;
            if (trimmed.Equals("Services/*", StringComparison.OrdinalIgnoreCase)) return true;
            return trimmed.StartsWith("Services/", StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsMatch(string input, string pattern)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(pattern)) return false;

            var trimmed = pattern.Trim();
            if (trimmed.EndsWith("*", StringComparison.Ordinal))
            {
                var prefix = trimmed.Substring(0, trimmed.Length - 1);
                return !string.IsNullOrEmpty(prefix) &&
                       input.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            return string.Equals(input, trimmed, StringComparison.OrdinalIgnoreCase);
        }

        internal static List<string> SanitizeEntries(IEnumerable<string> entries)
        {
            var result = new List<string>();
            if (entries == null) return result;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                var trimmed = entry?.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (IsUnsupportedEntry(trimmed)) continue;

                if (!seen.Add(trimmed)) continue;
                result.Add(trimmed);
            }

            return result;
        }
    }
}
