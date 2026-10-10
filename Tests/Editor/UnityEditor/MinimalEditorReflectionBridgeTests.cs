using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Essentials.Tests.Editor.UnityEditor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class MinimalEditorReflectionBridgeTests
    {
        static readonly Assembly UnityEditorAssembly = typeof(global::UnityEditor.Editor).Assembly;

        [Test]
        public void AppStatusBar_TypeExists_InUnityEditorAssembly()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.AppStatusBar");
            Assert.That(type, Is.Not.Null,
                "UnityEditor.AppStatusBar was not found. The status bar extras hider will do nothing.");
        }

        [Test]
        public void AppStatusBar_DrawExtraFeaturesFieldExists()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.AppStatusBar");
            Assume.That(type, Is.Not.Null);

            FieldInfo field = type.GetField(
                "m_DrawExtraFeatures",
                BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "AppStatusBar.m_DrawExtraFeatures was not found. The status bar extras hider cannot toggle it.");
        }

        [Test]
        public void EditorApplication_UpdateMainWindowTitleMethodExists()
        {
            MethodInfo method = typeof(EditorApplication).GetMethod(
                "UpdateMainWindowTitle",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null,
                "EditorApplication.UpdateMainWindowTitle() was not found. Window-title changes cannot be refreshed.");
        }

        [Test]
        public void WindowTitleShortener_Callback_UsesProductNameWhenEnabled()
        {
            Assume.That(string.IsNullOrWhiteSpace(PlayerSettings.productName), Is.False);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var shortener = Type.GetType("Essentials.Editor.WindowTitleShortener, Essentials.Editor");
            Assert.That(shortener, Is.Not.Null);
            var activeInput = shortener.GetProperty("ActiveInput", flags);
            Assert.That(activeInput, Is.Not.Null);
            var buildTitle = typeof(EditorApplication).GetMethod("BuildMainWindowTitle", flags);
            Assert.That(buildTitle, Is.Not.Null);

            var previousInput = activeInput.GetValue(null);
            try
            {
                activeInput.SetValue(null, new Func<bool>(() => true));
                Assert.That(buildTitle.Invoke(null, null), Is.EqualTo(PlayerSettings.productName),
                    "The title callback must be registered even when the Editor exposes it as an internal event.");
            }
            finally
            {
                activeInput.SetValue(null, previousInput);
            }
        }

        [Test]
        public void WindowTitleShortener_RepeatedInitialization_RegistersOneCallback_AndShutdownRemovesIt()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            var shortener = Type.GetType("Essentials.Editor.WindowTitleShortener, Essentials.Editor");
            Assert.That(shortener, Is.Not.Null);
            var initialize = shortener.GetMethod("Initialize", flags);
            var shutdown = shortener.GetMethod("Shutdown", flags);
            var activeInput = shortener.GetProperty("ActiveInput", flags);
            var buildTitle = typeof(EditorApplication).GetMethod("BuildMainWindowTitle", flags);
            Assert.That(initialize, Is.Not.Null);
            Assert.That(shutdown, Is.Not.Null);
            Assert.That(activeInput, Is.Not.Null);
            Assert.That(buildTitle, Is.Not.Null);

            var previousInput = activeInput.GetValue(null);
            try
            {
                initialize.Invoke(null, null);
                initialize.Invoke(null, null);
                var calls = 0;
                activeInput.SetValue(null, new Func<bool>(() => { calls++; return false; }));
                calls = 0;
                buildTitle.Invoke(null, null);
                Assert.That(calls, Is.EqualTo(1), "Repeated initialization must not duplicate the title callback.");

                shutdown.Invoke(null, null);
                shutdown.Invoke(null, null);
                Assert.That(activeInput.GetValue(null), Is.Null);
                calls = 0;
                // Set the field directly so checking teardown does not itself refresh the title.
                shortener.GetField("s_activeInput", flags).SetValue(null, new Func<bool>(() => { calls++; return false; }));
                buildTitle.Invoke(null, null);
                Assert.That(calls, Is.Zero, "Code unloading must release the Editor's title callback.");
            }
            finally
            {
                initialize.Invoke(null, null);
                activeInput.SetValue(null, previousInput);
            }
        }

        [Test]
        public void RemovedGitPackageUpdater_DoesNotReturn()
        {
            Assert.That(
                Type.GetType("Essentials.Editor.UpdateAllGitUpmTool, Essentials.Editor"),
                Is.Null,
                "The removed Git UPM updater implementation must not remain in Essentials.Editor.");
        }

    }
}
