using System;
using System.Reflection;
using MinimalEditor.Editor;
using NUnit.Framework;
using UnityEditor;

namespace MinimalEditor.Tests.Editor.UnityEditor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class MinimalEditorReflectionBridgeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void AppStatusBar_DrawExtraFeaturesFieldSupportsToggle()
        {
            var type = typeof(global::UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
            var field = type?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field?.FieldType, Is.EqualTo(typeof(bool)),
                "AppStatusBar.m_DrawExtraFeatures must exist as a bool for the status bar extras toggle.");
        }

        [Test]
        public void StatusBarExtrasHider_FallbackPoll_RepairsResetVisibility()
        {
            var type = typeof(global::UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar");
            var field = type.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            var timestamp = typeof(StatusBarExtrasHider).GetField("s_lastPollTimestamp", AnyStatic);
            var poll = typeof(StatusBarExtrasHider).GetMethod("PollFallback", AnyStatic);
            var previousInput = StatusBarExtrasHider.ActiveInput;
            var previousTimestamp = timestamp.GetValue(null);
            var statusBar = UnityEngine.ScriptableObject.CreateInstance(type);
            try
            {
                StatusBarExtrasHider.ActiveInput = () => true;
                Assert.That(field.GetValue(statusBar), Is.False);

                field.SetValue(statusBar, true);
                timestamp.SetValue(null, -15d);
                poll.Invoke(null, null);
                Assert.That(field.GetValue(statusBar), Is.False,
                    "The fallback must repair a reset bar even when the number of bars stays unchanged.");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(statusBar);
                StatusBarExtrasHider.ActiveInput = previousInput;
                timestamp.SetValue(null, previousTimestamp);
            }
        }

        [Test]
        public void WindowTitleShortener_Callback_UsesProductNameWhenEnabled()
        {
            Assert.That(WindowTitleShortener.TryRefreshTitle(), Is.True,
                "Window-title changes must be refreshable through the Unity reflection bridge.");
            Assume.That(string.IsNullOrWhiteSpace(PlayerSettings.productName), Is.False);
            var previousInput = WindowTitleShortener.ActiveInput;
            try
            {
                WindowTitleShortener.ActiveInput = () => true;
                Assert.That(BuildMainWindowTitle(), Is.EqualTo(PlayerSettings.productName),
                    "The title callback must be registered even when the Editor exposes it as an internal event.");
            }
            finally
            {
                WindowTitleShortener.ActiveInput = previousInput;
            }
        }

        [Test]
        public void WindowTitleShortener_RepeatedInitialization_RegistersOneCallback_AndShutdownRemovesIt()
        {
            var shortener = typeof(WindowTitleShortener);
            var initialize = shortener.GetMethod("Initialize", AnyStatic);
            var shutdown = shortener.GetMethod("Shutdown", AnyStatic);
            var previousInput = WindowTitleShortener.ActiveInput;
            try
            {
                initialize.Invoke(null, null);
                initialize.Invoke(null, null);
                var calls = 0;
                WindowTitleShortener.ActiveInput = () => { calls++; return false; };
                calls = 0;
                BuildMainWindowTitle();
                Assert.That(calls, Is.EqualTo(1), "Repeated initialization must not duplicate the title callback.");

                shutdown.Invoke(null, null);
                shutdown.Invoke(null, null);
                Assert.That(WindowTitleShortener.ActiveInput, Is.Null);
                calls = 0;
                // Set the field directly so checking teardown does not itself refresh the title.
                shortener.GetField("s_activeInput", AnyStatic).SetValue(null, new Func<bool>(() => { calls++; return false; }));
                BuildMainWindowTitle();
                Assert.That(calls, Is.Zero, "Code unloading must release the Editor's title callback.");
            }
            finally
            {
                initialize.Invoke(null, null);
                WindowTitleShortener.ActiveInput = previousInput;
            }
        }

        [Test]
        public void RemovedGitPackageUpdater_DoesNotReturn()
        {
            Assert.That(
                Type.GetType("MinimalEditor.Editor.UpdateAllGitUpmTool, MinimalEditor.Editor"),
                Is.Null,
                "The removed Git UPM updater implementation must not remain in MinimalEditor.Editor.");
        }

        static object BuildMainWindowTitle()
        {
            var method = typeof(EditorApplication).GetMethod("BuildMainWindowTitle", AnyStatic);
            Assert.That(method, Is.Not.Null, "The Editor title callback must remain accessible.");
            return method.Invoke(null, null);
        }
    }
}
