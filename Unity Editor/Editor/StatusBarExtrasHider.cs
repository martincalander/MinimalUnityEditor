using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Essentials.Editor
{
    [InitializeOnLoad]
    internal static class StatusBarExtrasHider
    {
        const double FallbackPollIntervalSeconds = 15d;

        static bool s_active = true;
        static Func<bool> s_activeInput;
        static bool s_lastHideState;
        static int s_lastStatusBarCount = -1;
        static double s_lastPollTimestamp;

        static readonly Type AppStatusBarType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
        static readonly FieldInfo DrawExtraFeaturesField =
            AppStatusBarType?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);

        static StatusBarExtrasHider()
        {
            EditorApplication.delayCall += Apply;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            EditorApplication.update += PollFallback;
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

        internal static void SetActive(bool active)
        {
            if (s_active == active) return;
            s_active = active;
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

        static void OnPlayModeStateChanged(PlayModeStateChange _)
        {
            Apply();
        }

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
            if (input == null) return s_active;

            try
            {
                return input.Invoke();
            }
            catch
            {
                return s_active;
            }
        }
    }
}
