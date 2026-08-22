using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Essentials.Tests.Editor.UnityEditor
{
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
        public void RemovedGitPackageUpdater_DoesNotReturn()
        {
            Assert.That(
                Type.GetType("Essentials.Editor.UpdateAllGitUpmTool, Essentials.Editor"),
                Is.Null,
                "The removed Git UPM updater implementation must not remain in Essentials.Editor.");
        }

    }
}
