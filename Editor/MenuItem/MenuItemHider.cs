using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MinimalEditor.MenuItem.Editor
{
    // Menu configuration and queued Editor work must survive Play Mode transitions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class MenuItemHider
    {
        static bool s_active = MenuItemModuleSettings.ModuleEnabled;
        static Func<bool> s_activeInput;

        static readonly List<string> s_manualMenuItemsToHide = new List<string>();
        static bool s_useManualMenuItems;
        static Func<IEnumerable<string>> s_menuItemsInput;

        static bool s_isApplying;
        static bool s_applyQueued;
        static bool s_forceApplyQueued;
        static bool s_failSafeMode;
        static bool s_mutationSuppressed;
        static bool s_loggedUnsupportedWarning;

        // Do not invoke Unity's internal menu rebuild from this module. Rebuilding menus from the
        // projectChanged/import path has caused native editor crashes in Unity's RebuildOSMainMenu.
        static readonly MethodInfo RemoveMenuItemMethod =
            typeof(Menu).GetMethod(
                "RemoveMenuItem",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);

        static readonly MethodInfo ExtractSubmenusMethod =
            typeof(Menu).GetMethod(
                "ExtractSubmenus",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
        static readonly MethodInfo MenuItemExistsMethod = typeof(Menu).GetMethod(
            "MenuItemExists", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
            null, new[] { typeof(string) }, null);
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

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            if (!MenuItemModuleSettings.IsSupportedInCurrentEditor)
            {
                LogUnsupportedVersionWarningIfNeeded();
                return;
            }

            s_mutationSuppressed = false;
            InitializeNativeRootVisibility();
            // A quit request can be canceled, so keep hiding active until quitting is committed.
            EditorApplication.quitting -= SuppressMutationForTeardown;
            EditorApplication.quitting += SuppressMutationForTeardown;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= SuppressMutationForTeardown;
            AssemblyReloadEvents.beforeAssemblyReload += SuppressMutationForTeardown;
#endif
            QueueApply(force: true);
            EditorApplication.projectChanged -= OnProjectChanged;
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

        internal static void SetMenuItems(IEnumerable<string> menuItems)
        {
            if (menuItems == null)
            {
                s_useManualMenuItems = false;
                s_manualMenuItemsToHide.Clear();
                Apply();
                return;
            }

            // Enumerate before changing the current configuration, including when the input
            // refers to the current list or throws partway through enumeration.
            var sanitizedItems = MenuItemPatternMatcher.SanitizeEntries(menuItems);
            s_useManualMenuItems = true;
            s_manualMenuItemsToHide.Clear();
            s_manualMenuItemsToHide.AddRange(sanitizedItems);
            Apply();
        }

        internal static void SetMenuItemsInput(Func<IEnumerable<string>> menuItemsInput)
        {
            s_menuItemsInput = menuItemsInput;
            Apply();
        }

        internal static void Apply()
        {
            QueueApply(force: true);
        }

        static void OnProjectChanged()
        {
            QueueApply(force: false);
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void SuppressMutationForTeardown()
        {
            s_mutationSuppressed = true;
            StopNativeRootVisibility();
            s_applyQueued = false;
            s_forceApplyQueued = false;
            EditorApplication.delayCall -= FlushQueuedApply;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.quitting -= SuppressMutationForTeardown;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= SuppressMutationForTeardown;
#endif
        }

        static void QueueApply(bool force)
        {
            if (!MenuItemModuleSettings.IsSupportedInCurrentEditor)
            {
                LogUnsupportedVersionWarningIfNeeded();
                return;
            }

            if (s_mutationSuppressed)
            {
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

            if (s_mutationSuppressed)
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
            if (s_isApplying || s_failSafeMode || s_mutationSuppressed) return;

            var isActive = ResolveActiveState();
            if (!isActive && !force) return;

            s_isApplying = true;
            try
            {
                var resolvedItems = MenuItemMoveRules.Split(isActive ? ResolveMenuPaths() : null, out var moveRules);
                ConfigureNativeRootVisibility(resolvedItems);
                // Moves run first so hide rules can also target the new paths. Without active
                // rules this puts moved commands back.
                ApplyMoveRules(isActive ? (IReadOnlyList<MenuItemMoveRule>)moveRules ?? Array.Empty<MenuItemMoveRule>() : null);
                if (!isActive) return;
                if (RemoveMenuItemMethod == null) return;

                // Rules that only hide a native heading never reach Unity's menu registry.
                var items = MenuItemPatternMatcher.ExcludeNativeRootOnlyEntries(resolvedItems);

                if (items == null || items.Count == 0) return;

                // Reading menus between removals is much slower, so read everything the rules need first.
                // Wildcard and exact rules share one enumeration per root. After the first apply,
                // exact rules no longer find their commands and look them up there.
                var wildcardRoots = BuildMenuRootSet(items);
                var pathsByRoot = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var root in wildcardRoots)
                    GetMenuPaths(root, pathsByRoot);
                var exactPathExists = new bool[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    var path = items[i]?.Trim();
                    if (string.IsNullOrEmpty(path) || path.EndsWith("*", StringComparison.Ordinal) ||
                        !CanRemoveDirectPath(path)) continue;

                    path = NormalizeMenuPath(path);
                    exactPathExists[i] = MenuItemExists(path);
                    if (!exactPathExists[i])
                        GetMenuPaths(MenuRootOf(path), pathsByRoot);
                }

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

                        foreach (var root in wildcardRoots)
                        {
                            if (!WildcardCoversRoot(path, root)) continue;
                            foreach (var candidate in GetMenuPaths(root, pathsByRoot))
                            {
                                if (!MenuItemPatternMatcher.IsMatch(candidate, path)) continue;
                                if (!CanRemoveDirectPath(candidate)) continue;
                                TryRemoveMenuItem(candidate);
                            }
                        }

                        continue;
                    }

                    if (!CanRemoveDirectPath(path)) continue;
                    path = NormalizeMenuPath(path);
                    if (exactPathExists[i])
                        TryRemoveMenuItem(path);
                    else
                        RemoveMissingExactMenuItem(path, GetMenuPaths(MenuRootOf(path), pathsByRoot));
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
            var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (var i = 0; i < items.Count; i++)
            {
                var path = items[i]?.Trim();
                // Exact paths are removed directly and do not need menu enumeration.
                if (string.IsNullOrEmpty(path) || !path.EndsWith("*", StringComparison.Ordinal)) continue;

                var separatorIndex = path.IndexOf('/');
                if (separatorIndex < 0)
                {
                    // A prefix such as "Tool*" can span known roots.
                    var prefix = path.Substring(0, path.Length - 1);
                    if (prefix.Length == 0) continue;
                    foreach (var knownRoot in MenuRoots)
                        if (knownRoot.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) roots.Add(knownRoot);
                    for (var j = 0; j < items.Count; j++)
                    {
                        var otherPath = items[j]?.Trim();
                        var otherSeparator = otherPath?.IndexOf('/') ?? -1;
                        if (otherSeparator <= 0) continue;
                        var otherRoot = otherPath.Substring(0, otherSeparator).Trim();
                        if (otherRoot.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) roots.Add(otherRoot);
                    }
                    continue;
                }
                if (separatorIndex == 0) continue;

                var root = path.Substring(0, separatorIndex).Trim();
                if (!string.IsNullOrEmpty(root))
                {
                    roots.Add(root);
                }
            }

            return roots;
        }

        // A wildcard such as "Window/Analysis/*" only matches commands under its own root, and
        // a root prefix such as "Tool*" only matches roots that start with that prefix.
        static bool WildcardCoversRoot(string wildcard, string root)
        {
            var separatorIndex = wildcard.IndexOf('/');
            if (separatorIndex >= 0)
                return string.Equals(wildcard.Substring(0, separatorIndex).Trim(), root, StringComparison.OrdinalIgnoreCase);

            var prefix = wildcard.Substring(0, wildcard.Length - 1);
            return prefix.Length > 0 && root.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
        }

        static HashSet<string> GetMenuPaths(string root, Dictionary<string, HashSet<string>> pathsByRoot)
        {
            root = CanonicalMenuRoot(root);
            if (!pathsByRoot.TryGetValue(root, out var paths))
            {
                paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                AddEnumeratedMenuPaths(root, paths);
                pathsByRoot.Add(root, paths);
            }

            return paths;
        }

        static void AddEnumeratedMenuPaths(string menuRoot, HashSet<string> allMenuPaths)
        {
            if (s_mutationSuppressed || string.IsNullOrWhiteSpace(menuRoot) || allMenuPaths == null) return;
            menuRoot = CanonicalMenuRoot(menuRoot);

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

        static string CanonicalMenuRoot(string root)
        {
            foreach (var knownRoot in MenuRoots)
                if (string.Equals(root, knownRoot, StringComparison.OrdinalIgnoreCase))
                    return knownRoot;
            return root;
        }

        static string MenuRootOf(string path) => path.Substring(0, path.IndexOf('/'));

        static bool MenuItemExists(string path)
        {
            try
            {
                return MenuItemExistsMethod?.Invoke(null, new object[] { path }) is bool exists && exists;
            }
            catch
            {
                // Enumeration still works when the optional existence check is unavailable.
                return false;
            }
        }

        // Correctly cased commands are removed directly. Resolve spelling only on a miss.
        static void RemoveMissingExactMenuItem(string path, HashSet<string> candidates)
        {
            var childPrefix = path + "/";
            foreach (var candidate in candidates)
            {
                if (string.Equals(candidate, path, StringComparison.OrdinalIgnoreCase) ||
                    candidate.StartsWith(childPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    TryRemoveMenuItem(candidate.Substring(0, path.Length));
                    return;
                }
            }
            TryRemoveMenuItem(path);
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
            ConfigureNativeRootVisibility(null);
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
            if (s_mutationSuppressed || string.IsNullOrWhiteSpace(path)) return;

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

    internal enum MenuItemBlockedEntryKind
    {
        Unsupported,
        ErrorProne
    }

    internal readonly struct MenuItemBlockedEntry
    {
        internal MenuItemBlockedEntry(string menuPath, MenuItemBlockedEntryKind kind, string warning)
        {
            MenuPath = menuPath;
            Kind = kind;
            Warning = warning;
        }

        internal string MenuPath { get; }
        internal MenuItemBlockedEntryKind Kind { get; }
        internal string Warning { get; }
    }

#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static class MenuItemPatternMatcher
    {
#if UNITY_EDITOR_OSX
        internal const string UnsupportedServicesWarning =
            "Unity regenerates Services menu items internally, so they cannot be removed. \"Services/*\" hides the Services heading in the macOS menu bar without removing its commands. Other Services entries are ignored.";
#else
        internal const string UnsupportedServicesWarning =
            "Services entries are ignored. Unity regenerates Services menu items internally, so they cannot be hidden consistently. Hiding the Services heading is only available on macOS.";
#endif

        const string ServicesRootRule = "Services/*";

        static readonly MenuItemBlockedEntry[] BlacklistedMenuItems =
        {
            new MenuItemBlockedEntry(
                "GameObject/Center On Children",
                MenuItemBlockedEntryKind.ErrorProne,
                "\"GameObject/Center On Children\" is blacklisted. Definitions that hide it are ignored because Unity still queries that menu item internally and will emit \"Could not find ... menu item\" errors.")
        };

        internal static bool IsUnsupportedEntry(string entry)
        {
            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed)) return false;

            // Unity regenerates Services menu entries internally; hiding them is not reliable.
            if (trimmed.Equals("Services", StringComparison.OrdinalIgnoreCase)) return true;
            if (trimmed.Equals("Services/*", StringComparison.OrdinalIgnoreCase)) return true;
            return trimmed.StartsWith("Services/", StringComparison.OrdinalIgnoreCase);
        }

        // A rule that can only hide a native menu-bar heading. Its commands must stay registered.
        internal static bool IsNativeRootOnlyEntry(string entry)
        {
            return string.Equals(entry?.Trim(), ServicesRootRule, StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyList<string> ExcludeNativeRootOnlyEntries(IReadOnlyList<string> entries)
        {
            if (entries == null) return null;

            List<string> result = null;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!IsNativeRootOnlyEntry(entries[i])) continue;
                result = new List<string>(entries.Count);
                break;
            }

            if (result == null) return entries;
            for (var i = 0; i < entries.Count; i++)
                if (!IsNativeRootOnlyEntry(entries[i])) result.Add(entries[i]);
            return result;
        }

        internal static bool IsBlacklistedEntry(string entry)
        {
            return TryGetWarningMessage(entry, MenuItemBlockedEntryKind.ErrorProne, out _);
        }

        internal static IReadOnlyList<string> GetWarningMessages(IEnumerable<string> entries, MenuItemBlockedEntryKind kind)
        {
            var warnings = new List<string>();
            if (entries == null) return warnings;

            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var entry in entries)
            {
                if (MenuItemMoveRules.IsMoveEntry(entry)) continue;
                if (!TryGetWarningMessage(entry, kind, out var warning)) continue;
                if (!seen.Add(warning)) continue;
                warnings.Add(warning);
            }

            return warnings;
        }

        internal static bool IsMatch(string input, string pattern)
        {
            if (string.IsNullOrWhiteSpace(input) || string.IsNullOrWhiteSpace(pattern)) return false;

            var trimmed = pattern.Trim();
            if (trimmed.EndsWith("*", StringComparison.Ordinal))
            {
                var prefixLength = trimmed.Length - 1;
                return prefixLength > 0 && input.Length >= prefixLength &&
                       string.Compare(input, 0, trimmed, 0, prefixLength, StringComparison.OrdinalIgnoreCase) == 0;
            }

            return string.Equals(input, trimmed, StringComparison.OrdinalIgnoreCase);
        }

        static bool TryGetWarningMessage(string entry, MenuItemBlockedEntryKind kind, out string warning)
        {
            var trimmed = entry?.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                warning = null;
                return false;
            }

            if (kind == MenuItemBlockedEntryKind.Unsupported && IsUnsupportedEntry(trimmed))
            {
                warning = UnsupportedServicesWarning;
                return true;
            }

            for (var i = 0; i < BlacklistedMenuItems.Length; i++)
            {
                var blacklistedItem = BlacklistedMenuItems[i];
                if (blacklistedItem.Kind != kind) continue;
                if (!TargetsMenuPath(trimmed, blacklistedItem.MenuPath)) continue;

                warning = blacklistedItem.Warning;
                return true;
            }

            warning = null;
            return false;
        }

        static bool TargetsMenuPath(string entry, string menuPath)
        {
            if (string.IsNullOrWhiteSpace(entry) || string.IsNullOrWhiteSpace(menuPath)) return false;

            var trimmedEntry = entry.Trim().TrimEnd('/');
            var trimmedMenuPath = menuPath.Trim();
            if (string.Equals(trimmedEntry, trimmedMenuPath, StringComparison.OrdinalIgnoreCase) ||
                IsMatch(trimmedMenuPath, trimmedEntry)) return true;

            // Applying "Path/*" also removes "Path" itself, so it must receive the same
            // protection as an exact rule for that command.
            return trimmedEntry.EndsWith("*", StringComparison.Ordinal) &&
                   string.Equals(
                       trimmedEntry.Substring(0, trimmedEntry.Length - 1).Trim().TrimEnd('/'),
                       trimmedMenuPath,
                       StringComparison.OrdinalIgnoreCase);
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

                if (MenuItemMoveRules.IsMoveEntry(trimmed))
                {
                    // Move rules are validated on their own and split from hide rules before applying.
                    if (MenuItemMoveRules.TryParse(trimmed, out _, out _) && seen.Add(trimmed)) result.Add(trimmed);
                    continue;
                }

                // Native-root-only rules pass through here and are excluded again before registry removal.
                if (IsUnsupportedEntry(trimmed) && !IsNativeRootOnlyEntry(trimmed)) continue;
                if (IsBlacklistedEntry(trimmed)) continue;

                if (!seen.Add(trimmed)) continue;
                result.Add(trimmed);
            }

            return result;
        }
    }
}
