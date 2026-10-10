using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Essentials.Editor
{
    // Editor configuration persists across Play Mode sessions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class StatusBarExtrasHider
    {
        const double FallbackPollIntervalSeconds = 15d;

        static Func<bool> s_activeInput;
        static bool s_lastHideState;
        static int s_lastStatusBarCount = -1;
        static double s_lastPollTimestamp;

        static readonly Type AppStatusBarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
        static readonly FieldInfo DrawExtraFeaturesField =
            AppStatusBarType?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            EditorApplication.update -= PollFallback;
            EditorApplication.update += PollFallback;
#if !UNITY_6000_5_OR_NEWER
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
            QueueApply();
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void Shutdown()
        {
            EditorApplication.delayCall -= Apply;
            EditorApplication.update -= PollFallback;
#if !UNITY_6000_5_OR_NEWER
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            s_activeInput = null;
            s_lastStatusBarCount = -1;
            s_lastPollTimestamp = 0d;
        }

        internal static bool Active
        {
            get => UnityEditorPreferences.HideStatusBarExtras;
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
            UnityEditorPreferences.HideStatusBarExtras = active;
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

        internal static void Apply()
        {
            ApplyCore(force: true);
        }

#if UNITY_6000_5_OR_NEWER
        [UnityEngine.OnEnteringPlayMode]
        [UnityEditor.Scripting.LifecycleManagement.OnEnteringEditMode]
#endif
        static void QueueApply()
        {
            EditorApplication.delayCall -= Apply;
            EditorApplication.delayCall += Apply;
        }

#if !UNITY_6000_5_OR_NEWER
        static void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            QueueApply();
        }
#endif

        static void PollFallback()
        {
            if (EditorApplication.timeSinceStartup - s_lastPollTimestamp < FallbackPollIntervalSeconds) return;
            s_lastPollTimestamp = EditorApplication.timeSinceStartup;
            ApplyCore(force: false);
        }

        static void ApplyCore(bool force)
        {
            if (AppStatusBarType == null || DrawExtraFeaturesField == null) return;

            UnityEngine.Object[] statusBars;
            try
            {
                statusBars = Resources.FindObjectsOfTypeAll(AppStatusBarType);
            }
            catch
            {
                return;
            }

            if (statusBars == null) return;

            var hide = IsActive();
            if (!force && hide == s_lastHideState && statusBars.Length == s_lastStatusBarCount) return;

            s_lastHideState = hide;
            s_lastStatusBarCount = statusBars.Length;

            foreach (var statusBar in statusBars)
            {
                if (statusBar == null) continue;
                try
                {
                    DrawExtraFeaturesField.SetValue(statusBar, !hide);
                }
                catch
                {
                    // Fail silently for editor-internal layout/runtime changes.
                }
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
