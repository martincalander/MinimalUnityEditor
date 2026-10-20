using System;
using System.Collections.Generic;
using UnityEditor;

namespace MinimalEditor.Console.Editor
{
    // Editor clients keep their registrations until they explicitly unregister.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public static partial class ConsoleHyperlinks
    {
        static readonly Dictionary<string, Action<Dictionary<string, string>>> s_handlers =
            new Dictionary<string, Action<Dictionary<string, string>>>(StringComparer.Ordinal);

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        internal static void Initialize()
        {
            EditorGUI.hyperLinkClicked -= OnHyperlinkClicked;
            EditorGUI.hyperLinkClicked += OnHyperlinkClicked;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void Shutdown()
        {
            EditorGUI.hyperLinkClicked -= OnHyperlinkClicked;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            s_handlers.Clear();
        }

        public static void Register(string href, Action<Dictionary<string, string>> handler)
        {
            s_handlers[href] = handler;
        }

        public static void Unregister(string href)
        {
            s_handlers.Remove(href);
        }

        static void OnHyperlinkClicked(EditorWindow window, HyperLinkClickedEventArgs args)
        {
            if (!args.hyperLinkData.TryGetValue("href", out var href))
                return;

            if (s_handlers.TryGetValue(href, out var handler))
            {
                handler.Invoke(args.hyperLinkData);
            }
        }
    }
}
