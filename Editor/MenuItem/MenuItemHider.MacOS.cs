using System;
using System.Collections.Generic;
using UnityEditor;

namespace Essentials.MenuItem.Editor
{
    internal static partial class MenuItemHider
    {
#if UNITY_EDITOR_OSX
        static MacOSMenuVisibility s_nativeMenuVisibility;
        static bool s_nativeMenuVisibilityFailed;
#endif

        static void InitializeNativeRootVisibility()
        {
#if UNITY_EDITOR_OSX
            if (s_nativeMenuVisibilityFailed) return;
            s_nativeMenuVisibility = s_nativeMenuVisibility ?? new MacOSMenuVisibility();
            s_nativeMenuVisibility.Configure(ResolveActiveState() ? ResolveMenuPaths() : null);
            EditorApplication.update -= UpdateNativeRootVisibility;
            EditorApplication.update += UpdateNativeRootVisibility;
            UpdateNativeRootVisibility();
#endif
        }

        static void ConfigureNativeRootVisibility(IReadOnlyList<string> patterns)
        {
#if UNITY_EDITOR_OSX
            s_nativeMenuVisibility?.Configure(patterns);
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
            try
            {
                // Unity can replace or unhide native roots without a projectChanged event.
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
