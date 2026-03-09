using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace MartinCalander.Essentials.Tests.Editor.ReflectionBridges
{
    /// <summary>
    /// Verifies that Unity's internal editor-wide APIs — accessed via reflection in
    /// InspectorPreferencesProvider, StatusBarExtrasHider, OpenTerminalTool, and
    /// AudioSourceAddonEditor — still match what the production code expects after a Unity update.
    ///
    /// A failure here means the corresponding feature will silently degrade or stop working.
    /// </summary>
    public sealed class EditorReflectionBridgeTests
    {
        static readonly Assembly UnityEditorAssembly = typeof(UnityEditor.Editor).Assembly;

        const BindingFlags AnyStatic   = BindingFlags.Static   | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        // ── InspectorPreferencesProvider ─────────────────────────────────────
        // Production code: InspectorPreferencesProvider.RepaintAllViewsMethod
        // Lookup: Type.GetType("UnityEditorInternal.InternalEditorUtility, UnityEditor")?.GetMethod("RepaintAllViews", …)

        [Test]
        public void InternalEditorUtility_TypeExists()
        {
            var type = Type.GetType("UnityEditorInternal.InternalEditorUtility, UnityEditor");
            Assert.That(type, Is.Not.Null,
                "UnityEditorInternal.InternalEditorUtility was not found. " +
                "Inspector preference changes will not trigger a repaint of all views.");
        }

        [Test]
        public void InternalEditorUtility_RepaintAllViews_StaticMethodExists()
        {
            var type = Type.GetType("UnityEditorInternal.InternalEditorUtility, UnityEditor");
            Assume.That(type, Is.Not.Null);
            var method = type.GetMethod("RepaintAllViews", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "InternalEditorUtility.RepaintAllViews() was not found. " +
                "Toggling inspector hiders from Preferences will not repaint open inspector windows.");
        }

        // ── StatusBarExtrasHider ─────────────────────────────────────────────
        // Production code: StatusBarExtrasHider.AppStatusBarType / DrawExtraFeaturesField
        // Lookup: typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.AppStatusBar")
        //         AppStatusBarType?.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance)

        [Test]
        public void AppStatusBar_TypeExists_InUnityEditorAssembly()
        {
            var type = UnityEditorAssembly.GetType("UnityEditor.AppStatusBar");
            Assert.That(type, Is.Not.Null,
                "UnityEditor.AppStatusBar was not found. " +
                "The status bar extras hider will silently do nothing.");
        }

        [Test]
        public void AppStatusBar_m_DrawExtraFeatures_NonPublicInstanceFieldExists()
        {
            // Production code uses BindingFlags.NonPublic | BindingFlags.Instance exactly.
            var type = UnityEditorAssembly.GetType("UnityEditor.AppStatusBar");
            Assume.That(type, Is.Not.Null);
            var field = type.GetField("m_DrawExtraFeatures", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.That(field, Is.Not.Null,
                "AppStatusBar.m_DrawExtraFeatures (non-public instance) was not found. " +
                "The status bar extras hider cannot toggle the animation/memory stat display.");
        }

        // ── OpenTerminalTool ─────────────────────────────────────────────────
        // Production code: OpenTerminalTool.GetAssetPathFromProjectBrowser()
        // Lookup: typeof(ProjectWindowUtil).GetMethod("GetActiveFolderPath",
        //         BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)

        [Test]
        public void ProjectWindowUtil_GetActiveFolderPath_StaticMethodExists()
        {
            var method = typeof(ProjectWindowUtil).GetMethod(
                "GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            Assert.That(method, Is.Not.Null,
                "ProjectWindowUtil.GetActiveFolderPath() was not found. " +
                "\"Assets / Open Terminal Here\" will fall back to the project root instead of the selected folder.");
        }

        [Test]
        public void ProjectWindowUtil_GetActiveFolderPath_ReturnsString()
        {
            var method = typeof(ProjectWindowUtil).GetMethod(
                "GetActiveFolderPath",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);

            Assume.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(string)),
                "ProjectWindowUtil.GetActiveFolderPath() no longer returns a string — " +
                "the result cast will produce null and the terminal will open at the project root.");
        }

        // ── AudioSourceAddonEditor ───────────────────────────────────────────
        // Production code: AudioSourceAddonEditor.OnEnable()
        // Lookup: Assembly.GetAssembly(typeof(UnityEditor.Editor)).GetType("UnityEditor.AudioSourceInspector")

        [Test]
        public void AudioSourceInspector_TypeExists_InUnityEditorAssembly()
        {
            var type = UnityEditorAssembly.GetType("UnityEditor.AudioSourceInspector");
            Assert.That(type, Is.Not.Null,
                "UnityEditor.AudioSourceInspector was not found. " +
                "The AudioSource custom editor will fall back to DrawDefaultInspector and lose built-in controls.");
        }

        [Test]
        public void AudioSourceInspector_IsEditorSubclass()
        {
            var type = UnityEditorAssembly.GetType("UnityEditor.AudioSourceInspector");
            Assume.That(type, Is.Not.Null);
            Assert.That(typeof(UnityEditor.Editor).IsAssignableFrom(type), Is.True,
                "UnityEditor.AudioSourceInspector no longer derives from UnityEditor.Editor. " +
                "CreateEditor(targets, editorType) will fail and the built-in inspector will not be shown.");
        }
    }
}
