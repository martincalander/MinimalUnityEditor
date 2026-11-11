using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MinimalEditor.MenuItem.Editor
{
    // A rule such as "FMOD/* => Tools/FMOD". Source and Destination are path prefixes without
    // a trailing slash or wildcard.
    internal readonly struct MenuItemMoveRule
    {
        internal MenuItemMoveRule(string source, string destination)
        {
            Source = source;
            Destination = destination;
        }

        internal string Source { get; }
        internal string Destination { get; }

        // Returns the new path for a command, or null when the rule does not cover it.
        internal string Map(string path)
        {
            if (string.IsNullOrEmpty(path) || path.Length < Source.Length ||
                string.Compare(path, 0, Source, 0, Source.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return null;
            if (path.Length == Source.Length) return Destination;
            return path[Source.Length] == '/' ? Destination + path.Substring(Source.Length) : null;
        }
    }

    internal static class MenuItemMoveRules
    {
        internal const string Arrow = "=>";

        // Commands under these roots depend on the current selection or are rebuilt by Unity,
        // so a copy registered somewhere else would not behave like the original.
        static readonly string[] ProtectedRoots = { "CONTEXT", "Assets", "GameObject", "Component", "Services" };

        internal static bool IsMoveEntry(string entry)
        {
            return entry != null && entry.IndexOf(Arrow, StringComparison.Ordinal) >= 0;
        }

        internal static bool TryParse(string entry, out MenuItemMoveRule rule, out string warning)
        {
            rule = default;
            warning = null;
            var trimmed = entry?.Trim();
            var arrowIndex = trimmed?.IndexOf(Arrow, StringComparison.Ordinal) ?? -1;
            if (arrowIndex < 0) return false;

            var source = trimmed.Substring(0, arrowIndex).Trim();
            var destination = trimmed.Substring(arrowIndex + Arrow.Length).Trim().TrimEnd('/').TrimEnd();
            if (source.EndsWith("/*", StringComparison.Ordinal)) source = source.Substring(0, source.Length - 2);
            source = source.TrimEnd('/').TrimEnd();

            if (source.Length == 0 || destination.Length == 0)
                warning = "\"" + trimmed + "\" needs a menu path on both sides of \"=>\".";
            else if (source.IndexOf('*') >= 0)
                warning = "\"" + trimmed + "\" is ignored. A move rule only supports \"*\" as a trailing \"/*\" on the source.";
            else if (destination.IndexOf('*') >= 0 || destination.IndexOf(Arrow, StringComparison.Ordinal) >= 0)
                warning = "\"" + trimmed + "\" is ignored. The destination must be a plain menu path.";
            else if (source.StartsWith("/", StringComparison.Ordinal) || destination.StartsWith("/", StringComparison.Ordinal))
                warning = "\"" + trimmed + "\" is ignored. Menu paths cannot start with \"/\".";
            else if (IsProtected(source))
                warning = "\"" + trimmed + "\" is ignored. Commands under " + RootOf(source) + " cannot be moved.";
            else if (IsProtected(destination))
                warning = "\"" + trimmed + "\" is ignored. Commands cannot be moved into " + RootOf(destination) + ".";
            else if (new MenuItemMoveRule(source, source).Map(destination) != null)
                warning = "\"" + trimmed + "\" is ignored. The destination cannot be inside the source.";

            if (warning != null) return false;
            rule = new MenuItemMoveRule(source, destination);
            return true;
        }

        // Splits resolved entries into the paths to hide and the move rules.
        internal static IReadOnlyList<string> Split(IReadOnlyList<string> entries, out List<MenuItemMoveRule> rules)
        {
            rules = null;
            if (entries == null) return null;

            List<string> hidden = null;
            for (var i = 0; i < entries.Count; i++)
            {
                if (!IsMoveEntry(entries[i]))
                {
                    hidden?.Add(entries[i]);
                    continue;
                }

                if (hidden == null)
                {
                    hidden = new List<string>(entries.Count);
                    for (var j = 0; j < i; j++) hidden.Add(entries[j]);
                }

                if (!TryParse(entries[i], out var rule, out _)) continue;
                rules = rules ?? new List<MenuItemMoveRule>();
                rules.Add(rule);
            }

            return hidden ?? entries;
        }

        internal static IReadOnlyList<string> GetWarningMessages(IEnumerable<string> entries)
        {
            var warnings = new List<string>();
            if (entries == null) return warnings;
            foreach (var entry in entries)
            {
                if (!IsMoveEntry(entry) || TryParse(entry, out _, out var warning)) continue;
                if (warning != null && !warnings.Contains(warning)) warnings.Add(warning);
            }

            return warnings;
        }

        internal static string RootOf(string path)
        {
            var separator = path.IndexOf('/');
            return separator < 0 ? path : path.Substring(0, separator);
        }

        static bool IsProtected(string path)
        {
            var root = RootOf(path).Trim();
            for (var i = 0; i < ProtectedRoots.Length; i++)
                if (string.Equals(root, ProtectedRoots[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }

    internal static partial class MenuItemHider
    {
        static readonly MethodInfo AddMenuItemMethod = typeof(Menu).GetMethod(
            "AddMenuItem", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            new[] { typeof(string), typeof(string), typeof(bool), typeof(int), typeof(Action), typeof(Func<bool>) },
            null);
        static readonly MethodInfo FindHotkeyStartIndexMethod = typeof(Menu).GetMethod(
            "FindHotkeyStartIndex", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null,
            new[] { typeof(string) }, null);
        // Looked up by name because static fields in the other part of this class may not be set yet.
        static readonly PropertyInfo ScriptingMenuItemPriorityProperty =
            typeof(Menu).Assembly.GetType("UnityEditor.ScriptingMenuItem")?.GetProperty(
                "priority", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Moved commands by their original path, and the original path that owns each new path.
        static readonly Dictionary<string, MovedMenuItem> s_movedItems =
            new Dictionary<string, MovedMenuItem>(StringComparer.Ordinal);
        static readonly Dictionary<string, string> s_moveDestinationOwners =
            new Dictionary<string, string>(StringComparer.Ordinal);
        static Dictionary<string, MenuCommandMethods> s_menuCommandMethods;
        static bool s_loggedMoveUnavailable;

        static bool CanMoveMenuItems =>
            AddMenuItemMethod != null && RemoveMenuItemMethod != null && MenuItemExistsMethod != null &&
            GetMenuItemsMethod != null && ScriptingMenuItemPathProperty != null &&
            ScriptingMenuItemPriorityProperty != null;

        // Pass null to put every moved command back.
        static void ApplyMoveRules(IReadOnlyList<MenuItemMoveRule> rules)
        {
            if (s_mutationSuppressed) return;
            if (!CanMoveMenuItems)
            {
                if (rules != null && rules.Count > 0 && !s_loggedMoveUnavailable)
                {
                    s_loggedMoveUnavailable = true;
                    UnityEngine.Debug.LogWarning(
                        "[MenuItemHider] Moving menu items is not available in this Unity version. Move rules are ignored.");
                }

                return;
            }

            RestoreMovedItemsWithoutRule(rules);
            if (rules == null) return;
            for (var i = 0; i < rules.Count; i++)
                ApplyMoveRule(rules[i]);
        }

        static void ApplyMoveRule(MenuItemMoveRule rule)
        {
            var root = CanonicalMenuRoot(MenuItemMoveRules.RootOf(rule.Source));
            if (!(GetMenuItemsMethod.Invoke(null, new object[] { root, false, false }) is Array items)) return;

            for (var i = 0; i < items.Length; i++)
            {
                var item = items.GetValue(i);
                var path = ScriptingMenuItemPathProperty.GetValue(item, null) as string;
                // A command this module placed is never a source, so rules cannot chain or loop.
                if (string.IsNullOrEmpty(path) || s_moveDestinationOwners.ContainsKey(path)) continue;

                var destination = rule.Map(path);
                if (destination == null) continue;
                var priority = ScriptingMenuItemPriorityProperty.GetValue(item, null) is int value ? value : 0;
                if (MoveMenuItem(path, destination, priority))
                    RemoveEmptyParentMenus(path);
            }
        }

        static bool MoveMenuItem(string source, string destination, int priority)
        {
            if (MenuItemExists(destination))
            {
                // Unity registered the original again after it was moved. Anything else at the
                // destination belongs to someone else, so the original stays where it is.
                if (!s_moveDestinationOwners.TryGetValue(destination, out var owner) ||
                    !string.Equals(owner, source, StringComparison.Ordinal)) return false;
                TryRemoveMenuItem(source);
                return true;
            }

            // Only commands declared with a MenuItem attribute can be called from a new path.
            if (!TryGetMenuCommandMethods(source, out var methods)) return false;

            Action execute = () => InvokeMenuCommand(methods.Execute);
            Func<bool> validate = () => ValidateMenuCommand(methods.Validate);
            AddMenuItemMethod.Invoke(null, new object[] { destination, methods.Hotkey, false, priority, execute, validate });
            if (!MenuItemExists(destination)) return false;

            TryRemoveMenuItem(source);
            s_movedItems[source] = new MovedMenuItem(destination, methods.Hotkey, priority, execute, validate);
            s_moveDestinationOwners[destination] = source;
            return true;
        }

        static void RestoreMovedItemsWithoutRule(IReadOnlyList<MenuItemMoveRule> rules)
        {
            if (s_movedItems.Count == 0) return;

            List<string> stale = null;
            foreach (var pair in s_movedItems)
            {
                string destination = null;
                for (var i = 0; rules != null && destination == null && i < rules.Count; i++)
                    destination = rules[i].Map(pair.Key);
                if (string.Equals(destination, pair.Value.Destination, StringComparison.Ordinal)) continue;
                stale = stale ?? new List<string>();
                stale.Add(pair.Key);
            }

            if (stale == null) return;
            for (var i = 0; i < stale.Count; i++)
            {
                var source = stale[i];
                var moved = s_movedItems[source];
                s_movedItems.Remove(source);
                s_moveDestinationOwners.Remove(moved.Destination);

                if (MenuItemExists(moved.Destination))
                {
                    TryRemoveMenuItem(moved.Destination);
                    RemoveEmptyParentMenus(moved.Destination);
                }

                if (!MenuItemExists(source))
                    AddMenuItemMethod.Invoke(
                        null, new object[] { source, moved.Hotkey, false, moved.Priority, moved.Execute, moved.Validate });
            }
        }

        // Unity keeps a submenu after its last command is removed. Every parent of a command
        // this module removed was a submenu, so an empty one can be removed as well.
        static void RemoveEmptyParentMenus(string path)
        {
            for (var separator = path.LastIndexOf('/'); separator > 0; separator = path.LastIndexOf('/'))
            {
                path = path.Substring(0, separator);
                if (path.IndexOf('/') < 0 && IsStandardMenuRoot(path)) return;
                if (!MenuItemExists(path)) continue;
                if (!(GetMenuItemsMethod.Invoke(null, new object[] { path, false, false }) is Array items) ||
                    items.Length > 0) return;
                TryRemoveMenuItem(path);
            }
        }

        static bool IsStandardMenuRoot(string root)
        {
            foreach (var knownRoot in MenuRoots)
                if (string.Equals(root, knownRoot, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        static bool TryGetMenuCommandMethods(string path, out MenuCommandMethods methods)
        {
            if (s_menuCommandMethods == null) s_menuCommandMethods = CollectMenuCommandMethods();
            return s_menuCommandMethods.TryGetValue(path, out methods) && methods.Execute != null;
        }

        static Dictionary<string, MenuCommandMethods> CollectMenuCommandMethods()
        {
            var commands = new Dictionary<string, MenuCommandMethods>(StringComparer.Ordinal);
            foreach (var method in TypeCache.GetMethodsWithAttribute<global::UnityEditor.MenuItem>())
            {
                if (!method.IsStatic || method.ContainsGenericParameters) continue;
                var parameters = method.GetParameters();
                if (parameters.Length > 1 ||
                    (parameters.Length == 1 && parameters[0].ParameterType != typeof(MenuCommand))) continue;

                foreach (var attribute in method.GetCustomAttributes(typeof(global::UnityEditor.MenuItem), false))
                {
                    var menuItem = (global::UnityEditor.MenuItem)attribute;
                    SplitMenuItemName(menuItem.menuItem, out var path, out var hotkey);
                    if (string.IsNullOrEmpty(path)) continue;

                    commands.TryGetValue(path, out var methods);
                    if (menuItem.validate)
                    {
                        if (method.ReturnType != typeof(bool)) continue;
                        methods.Validate = method;
                    }
                    else
                    {
                        methods.Execute = method;
                        methods.Hotkey = hotkey;
                    }

                    commands[path] = methods;
                }
            }

            return commands;
        }

        // "Window/General/Console %#c" is the command "Window/General/Console" with the hotkey "%#c".
        internal static void SplitMenuItemName(string name, out string path, out string hotkey)
        {
            path = name?.Trim() ?? string.Empty;
            hotkey = string.Empty;
            var index = -1;
            try
            {
                if (FindHotkeyStartIndexMethod?.Invoke(null, new object[] { path }) is int found) index = found;
            }
            catch
            {
                // Fall back to the documented hotkey format below.
            }

            if (FindHotkeyStartIndexMethod == null || index < 0)
            {
                var space = path.LastIndexOf(' ');
                index = space >= 0 && space + 1 < path.Length && "%#&^_".IndexOf(path[space + 1]) >= 0
                    ? space + 1
                    : path.Length;
            }

            if (index <= 0 || index >= path.Length) return;
            hotkey = path.Substring(index).Trim();
            path = path.Substring(0, index).TrimEnd();
        }

        static void InvokeMenuCommand(MethodInfo method)
        {
            try
            {
                method.Invoke(null, MenuCommandArguments(method));
            }
            catch (TargetInvocationException exception)
            {
                if (!(exception.InnerException is UnityEngine.ExitGUIException))
                    UnityEngine.Debug.LogException(exception.InnerException ?? exception);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
            }
        }

        static bool ValidateMenuCommand(MethodInfo method)
        {
            if (method == null) return true;
            try
            {
                return method.Invoke(null, MenuCommandArguments(method)) is bool enabled && enabled;
            }
            catch
            {
                return false;
            }
        }

        static object[] MenuCommandArguments(MethodInfo method)
        {
            return method.GetParameters().Length == 0 ? null : new object[] { new MenuCommand(null) };
        }

        struct MenuCommandMethods
        {
            internal MethodInfo Execute;
            internal MethodInfo Validate;
            internal string Hotkey;
        }

        readonly struct MovedMenuItem
        {
            internal MovedMenuItem(string destination, string hotkey, int priority, Action execute, Func<bool> validate)
            {
                Destination = destination;
                Hotkey = hotkey;
                Priority = priority;
                Execute = execute;
                Validate = validate;
            }

            internal string Destination { get; }
            internal string Hotkey { get; }
            internal int Priority { get; }
            internal Action Execute { get; }
            internal Func<bool> Validate { get; }
        }
    }
}
