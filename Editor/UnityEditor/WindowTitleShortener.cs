using System;
using System.Reflection;
using UnityEditor;

namespace Essentials.Editor
{
    // Keep the Editor's title override active throughout Play Mode transitions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class WindowTitleShortener
    {
        static Func<bool> s_activeInput;

        static readonly MethodInfo UpdateTitleMethod =
            typeof(EditorApplication).GetMethod(
                "UpdateMainWindowTitle",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

#if !UNITY_2023_2_OR_NEWER
        static readonly EventInfo UpdateTitleEvent = typeof(EditorApplication).GetEvent(
            "updateMainWindowTitle", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
        static readonly FieldInfo TitleField = typeof(EditorApplication).Assembly
            .GetType("UnityEditor.ApplicationTitleDescriptor")
            ?.GetField("title", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
#endif

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
#if UNITY_2023_2_OR_NEWER
            EditorApplication.updateMainWindowTitle -= OnUpdateMainWindowTitle;
            EditorApplication.updateMainWindowTitle += OnUpdateMainWindowTitle;
#else
            TrySetTitleCallback(register: true);
#endif
            EditorApplication.delayCall -= RefreshTitle;
            EditorApplication.delayCall += RefreshTitle;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void Shutdown()
        {
            EditorApplication.delayCall -= RefreshTitle;
#if UNITY_2023_2_OR_NEWER
            EditorApplication.updateMainWindowTitle -= OnUpdateMainWindowTitle;
#else
            TrySetTitleCallback(register: false);
#endif
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            s_activeInput = null;
        }

        static void RefreshTitle()
        {
            TryRefreshTitle();
        }

#if !UNITY_2023_2_OR_NEWER
        static void TrySetTitleCallback(bool register)
        {
            if (UpdateTitleEvent == null || TitleField == null)
                return;

            try
            {
                var method = typeof(WindowTitleShortener).GetMethod(
                    nameof(OnUpdateMainWindowTitle), BindingFlags.Static | BindingFlags.NonPublic);
                var callback = Delegate.CreateDelegate(UpdateTitleEvent.EventHandlerType, method);
                UpdateTitleEvent.GetRemoveMethod(nonPublic: true)?.Invoke(null, new object[] { callback });
                if (register)
                    UpdateTitleEvent.GetAddMethod(nonPublic: true)?.Invoke(null, new object[] { callback });
            }
            catch
            {
                // Older Editors expose the title callback only through internal APIs.
            }
        }
#endif

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

#if UNITY_2023_2_OR_NEWER
        static void OnUpdateMainWindowTitle(ApplicationTitleDescriptor descriptor)
#else
        static void OnUpdateMainWindowTitle(object descriptor)
#endif
        {
            try
            {
                if (!IsActive()) return;
                if (!string.IsNullOrWhiteSpace(PlayerSettings.productName))
                {
#if UNITY_2023_2_OR_NEWER
                    descriptor.title = PlayerSettings.productName;
#else
                    TitleField?.SetValue(descriptor, PlayerSettings.productName);
#endif
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
