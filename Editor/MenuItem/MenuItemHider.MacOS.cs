using System;
using System.Collections.Generic;
using UnityEditor;

namespace MinimalEditor.MenuItem.Editor
{
    internal static partial class MenuItemHider
    {
#if UNITY_EDITOR_OSX
        static MacOSMenuVisibility s_nativeMenuVisibility;
        static bool s_nativeMenuVisibilityFailed;
        static double s_nextNativeMenuUpdate;
        const double NativeMenuUpdateInterval = 0.2;
#endif

        static void InitializeNativeRootVisibility()
        {
#if UNITY_EDITOR_OSX
            if (s_nativeMenuVisibilityFailed) return;
            ConfigureNativeRootVisibility(ResolveActiveState() ? ResolveMenuPaths() : null);
#endif
        }

        static void ConfigureNativeRootVisibility(IReadOnlyList<string> patterns)
        {
#if UNITY_EDITOR_OSX
            if (IsMutationSuppressed() || s_nativeMenuVisibilityFailed) return;
            s_nativeMenuVisibility = s_nativeMenuVisibility ?? new MacOSMenuVisibility();
            s_nativeMenuVisibility.Configure(patterns);
            EditorApplication.update -= UpdateNativeRootVisibility;
            if (s_nativeMenuVisibility.HasHiddenRoots)
                EditorApplication.update += UpdateNativeRootVisibility;
            s_nextNativeMenuUpdate = 0;
            UpdateNativeRootVisibility();
#endif
        }

        static void StopNativeRootVisibility()
        {
#if UNITY_EDITOR_OSX
            EditorApplication.update -= UpdateNativeRootVisibility;
            s_nativeMenuVisibility?.Dispose();
            s_nativeMenuVisibility = null;
#endif
        }

#if UNITY_EDITOR_OSX
        static void UpdateNativeRootVisibility()
        {
            if (IsMutationSuppressed() || s_nativeMenuVisibilityFailed) return;
            var now = EditorApplication.timeSinceStartup;
            if (now < s_nextNativeMenuUpdate) return;
            s_nextNativeMenuUpdate = now + NativeMenuUpdateInterval;
            try
            {
                // Unity can replace or unhide native roots without a projectChanged event.
                // Poll at a bounded rate instead of copying native menu titles every frame.
                s_nativeMenuVisibility?.Apply();
            }
            catch (Exception exception)
            {
                s_nativeMenuVisibilityFailed = true;
                StopNativeRootVisibility();
                UnityEngine.Debug.LogWarning("[MenuItemHider] macOS root menu hiding is unavailable: " + exception.Message);
            }
        }
#endif
    }
}
