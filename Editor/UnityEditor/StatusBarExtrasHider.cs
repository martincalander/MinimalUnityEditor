using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Editor
{
    // Editor configuration persists across Play Mode sessions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class StatusBarExtrasHider
    {
        const double FallbackPollIntervalSeconds = 15d;
        // Survives domain reloads, so extras hidden earlier in this Editor session can be restored.
        const string HiddenSessionKey = "MinimalEditor.StatusBarExtrasHidden";

        static Func<bool> s_activeInput;
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
            if (IsActive() || SessionState.GetBool(HiddenSessionKey, false))
                Apply();
        }

        internal static void Apply()
        {
            if (AppStatusBarType == null || DrawExtraFeaturesField == null) return;

            var hide = IsActive();
            // Leave Unity's own status-bar setting alone unless this package hid the extras.
            if (!hide && !SessionState.GetBool(HiddenSessionKey, false)) return;

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

            var ownershipVersionKey = HiddenSessionKey + ".PerBarOwnership";
            var restoreLegacyOwnership = SessionState.GetBool(HiddenSessionKey, false) &&
                                         !SessionState.GetBool(ownershipVersionKey, false);
            var ownsHiddenBar = false;
            foreach (var statusBar in statusBars)
            {
                if (statusBar == null) continue;
#if UNITY_7000_0_OR_NEWER
                var key = HiddenSessionKey + "." + statusBar.GetEntityId();
#else
                var key = HiddenSessionKey + "." + statusBar.GetInstanceID();
#endif
                if (restoreLegacyOwnership)
                    SessionState.SetBool(key, true);
                try
                {
                    if (hide)
                    {
                        if ((bool)DrawExtraFeaturesField.GetValue(statusBar))
                        {
                            DrawExtraFeaturesField.SetValue(statusBar, false);
                            SessionState.SetBool(key, true);
                        }
                    }
                    else if (SessionState.GetBool(key, false))
                    {
                        DrawExtraFeaturesField.SetValue(statusBar, true);
                        SessionState.EraseBool(key);
                    }
                }
                catch
                {
                    // Fail silently for editor-internal layout/runtime changes.
                }
                ownsHiddenBar |= SessionState.GetBool(key, false);
            }
            SessionState.SetBool(ownershipVersionKey, true);
            SessionState.SetBool(HiddenSessionKey, ownsHiddenBar);
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
