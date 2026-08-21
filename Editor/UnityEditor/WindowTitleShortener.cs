using System;
using System.Reflection;
using UnityEditor;

namespace Essentials.Editor
{
    [InitializeOnLoad]
    internal static class WindowTitleShortener
    {
        static Func<bool> s_activeInput;

        static readonly MethodInfo UpdateTitleMethod =
            typeof(EditorApplication).GetMethod("UpdateMainWindowTitle", BindingFlags.Static | BindingFlags.NonPublic);

        static WindowTitleShortener()
        {
            EditorApplication.updateMainWindowTitle -= OnUpdateMainWindowTitle;
            EditorApplication.updateMainWindowTitle += OnUpdateMainWindowTitle;
            TryRefreshTitle();
        }

        internal static bool Active
        {
            get => UnityEditorPreferences.ShortWindowTitle;
            set => SetActive(value);
        }

        internal static Func<bool> ActiveInput
        {
            get => s_activeInput;
            set => SetActiveInput(value);
        }

        internal static void SetActive(bool active)
        {
            if (Active == active) return;
            UnityEditorPreferences.ShortWindowTitle = active;
            TryRefreshTitle();
        }

        internal static void SetActiveInput(Func<bool> activeInput)
        {
            s_activeInput = activeInput;
            TryRefreshTitle();
        }

        internal static void ClearActiveInput()
        {
            if (s_activeInput == null) return;
            s_activeInput = null;
            TryRefreshTitle();
        }

        internal static bool TryRefreshTitle()
        {
            if (UpdateTitleMethod == null) return false;
            try
            {
                UpdateTitleMethod.Invoke(null, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static void OnUpdateMainWindowTitle(ApplicationTitleDescriptor descriptor)
        {
            try
            {
                if (!IsActive()) return;
                if (!string.IsNullOrWhiteSpace(PlayerSettings.productName))
                {
                    descriptor.title = PlayerSettings.productName;
                }
            }
            catch
            {
                // Fail silently for editor-internal callback behavior changes.
            }
        }

        static bool IsActive()
        {
            var input = s_activeInput;
            if (input == null) return Active;

            try
            {
                return input.Invoke();
            }
            catch
            {
                return Active;
            }
        }
    }
}
