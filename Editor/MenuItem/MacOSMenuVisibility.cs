#if UNITY_EDITOR_OSX
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace Essentials.MenuItem.Editor
{
    // AppKit owns the visible root items separately from Unity's command registry.
    internal sealed class MacOSMenuVisibility : IDisposable
    {
        readonly HashSet<IntPtr> ownedItems = new HashSet<IntPtr>();
        readonly HashSet<IntPtr> currentItems = new HashSet<IntPtr>();
        readonly List<IntPtr> retiredItems = new List<IntPtr>();
        readonly HashSet<string> hiddenRoots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal void Configure(IReadOnlyList<string> patterns)
        {
            hiddenRoots.Clear();
            if (patterns == null) return;
            foreach (var pattern in patterns)
            {
                var root = GetRoot(pattern);
                if (root != null) hiddenRoots.Add(root);
            }
        }

        internal static string GetRoot(string pattern)
        {
            pattern = pattern?.Trim();
            if (string.IsNullOrEmpty(pattern) || !pattern.EndsWith("/*", StringComparison.Ordinal) ||
                pattern.IndexOf('/') != pattern.Length - 2 ||
                MenuItemPatternMatcher.IsUnsupportedEntry(pattern) ||
                MenuItemPatternMatcher.IsBlacklistedEntry(pattern)) return null;
            var root = pattern.Substring(0, pattern.Length - 2);
            return root.Length == 0 || root.IndexOf('*') >= 0 ? null : root;
        }

        internal void Apply()
        {
            if (hiddenRoots.Count == 0)
            {
                Dispose();
                return;
            }
            ApplyToMenu(Native.Get(Native.Get(Native.ApplicationClass, Native.SharedApplication), Native.MainMenu));
        }

        internal void ApplyToMenu(IntPtr menu)
        {
            currentItems.Clear();
            // Copy the array so menu-change notifications cannot invalidate our iteration.
            var items = menu == IntPtr.Zero ? IntPtr.Zero : Native.Get(Native.Get(menu, Native.ItemArray), Native.Copy);
            try
            {
                var count = items == IntPtr.Zero ? 0 : Native.Get(items, Native.Count).ToInt64();
                // The first item is the application menu, which must remain available.
                for (long index = 1; index < count; index++)
                {
                    var item = Native.GetAt(items, Native.ObjectAtIndex, new IntPtr(index));
                    var title = Native.Get(item, Native.Title);
                    var text = Marshal.PtrToStringAnsi(Native.Get(title, Native.UTF8String));
                    if (text == null || !hiddenRoots.Contains(text)) continue;

                    if (Native.GetBool(item, Native.IsHidden) == 0)
                    {
                        // Retained ownership also makes restoration safe after Unity replaces a root.
                        if (ownedItems.Add(item)) Native.Get(item, Native.Retain);
                        Native.SetBool(item, Native.SetHidden, 1);
                    }
                    if (ownedItems.Contains(item)) currentItems.Add(item);
                }
            }
            finally
            {
                if (items != IntPtr.Zero) Native.Send(items, Native.Release);
            }

            retiredItems.Clear();
            foreach (var item in ownedItems)
                if (!currentItems.Contains(item)) retiredItems.Add(item);
            foreach (var item in retiredItems)
            {
                Restore(item);
                ownedItems.Remove(item);
            }
        }

        public void Dispose()
        {
            foreach (var item in ownedItems) Restore(item);
            ownedItems.Clear();
            currentItems.Clear();
            retiredItems.Clear();
        }

        static void Restore(IntPtr item)
        {
            try
            {
                Native.SetBool(item, Native.SetHidden, 0);
            }
            finally
            {
                Native.Send(item, Native.Release);
            }
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
        static class Native
        {
            const string Library = "/usr/lib/libobjc.A.dylib";
            internal static readonly IntPtr ApplicationClass = objc_getClass("NSApplication");
            internal static readonly IntPtr SharedApplication = sel_registerName("sharedApplication");
            internal static readonly IntPtr MainMenu = sel_registerName("mainMenu");
            internal static readonly IntPtr ItemArray = sel_registerName("itemArray");
            internal static readonly IntPtr Count = sel_registerName("count");
            internal static readonly IntPtr ObjectAtIndex = sel_registerName("objectAtIndex:");
            internal static readonly IntPtr Title = sel_registerName("title");
            internal static readonly IntPtr UTF8String = sel_registerName("UTF8String");
            internal static readonly IntPtr IsHidden = sel_registerName("isHidden");
            internal static readonly IntPtr SetHidden = sel_registerName("setHidden:");
            internal static readonly IntPtr Retain = sel_registerName("retain");
            internal static readonly IntPtr Copy = sel_registerName("copy");
            internal static readonly IntPtr Release = sel_registerName("release");

            [DllImport(Library)] static extern IntPtr objc_getClass(string name);
            [DllImport(Library)] static extern IntPtr sel_registerName(string name);
            [DllImport(Library, EntryPoint = "objc_msgSend")] internal static extern IntPtr Get(IntPtr receiver, IntPtr selector);
            [DllImport(Library, EntryPoint = "objc_msgSend")] internal static extern IntPtr GetAt(IntPtr receiver, IntPtr selector, IntPtr index);
            [DllImport(Library, EntryPoint = "objc_msgSend")] internal static extern byte GetBool(IntPtr receiver, IntPtr selector);
            [DllImport(Library, EntryPoint = "objc_msgSend")] internal static extern void SetBool(IntPtr receiver, IntPtr selector, byte value);
            [DllImport(Library, EntryPoint = "objc_msgSend")] internal static extern void Send(IntPtr receiver, IntPtr selector);
        }
    }
}
#endif
