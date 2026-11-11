using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using MinimalEditor.Editor;

namespace MinimalEditor.Tests.Editor.UnityEditor
{
    public sealed class AutomatedEditorQuitTests
    {
        bool hadKey;
        bool previousValue;

        [SetUp]
        public void SetUp()
        {
            hadKey = EditorPrefs.HasKey(UnityEditorPreferences.QuitAutomatedEditorKey);
            previousValue = EditorPrefs.GetBool(UnityEditorPreferences.QuitAutomatedEditorKey, false);
        }

        [TearDown]
        public void TearDown()
        {
            EditorFeatureToggles.QuitAutomatedEditor =
                hadKey ? previousValue : UnityEditorPreferences.QuitAutomatedEditorDefaultValue;
            if (!hadKey)
                EditorPrefs.DeleteKey(UnityEditorPreferences.QuitAutomatedEditorKey);
        }

        [Test]
        public void IsSupported_OnlyOnUnity7AndNewer()
        {
#if UNITY_7000_0_OR_NEWER
            Assert.That(AutomatedEditorQuit.GetQuitRequestMethod(), Is.Not.Null,
                "StageNavigationManager.Internal_AskUserToSaveModifiedStageBeforeQuitting must be a static bool method without parameters.");
            Assert.That(AutomatedEditorQuit.IsSupported, Is.True, AutomatedEditorQuit.LastPatchError);
#else
            Assert.That(AutomatedEditorQuit.IsSupported, Is.False);
            Assert.That(AutomatedEditorQuit.IsInstalled, Is.False);
#endif
        }

        [Test]
        public void IsAutomated_RequiresTheExactArgument()
        {
            Assert.That(AutomatedEditorQuit.IsAutomated(new[] { "Unity", "-projectpath", "x", "-automated" }), Is.True);
            Assert.That(AutomatedEditorQuit.IsAutomated(new[] { "Unity", "-automatedTests", "automated" }), Is.False);
            Assert.That(AutomatedEditorQuit.IsAutomated(Array.Empty<string>()), Is.False);
            Assert.That(AutomatedEditorQuit.IsAutomated(null), Is.False);
        }

        [Test]
        public void ShouldHandleQuit_NeedsSupportPreferenceAndAnInteractiveAutomatedEditor()
        {
            var automated = new[] { "Unity", "-automated" };
            var normal = new[] { "Unity" };

            Assert.That(AutomatedEditorQuit.ShouldHandleQuit(true, true, false, automated), Is.True);
            Assert.That(AutomatedEditorQuit.ShouldHandleQuit(false, true, false, automated), Is.False, "unsupported Editor");
            Assert.That(AutomatedEditorQuit.ShouldHandleQuit(true, false, false, automated), Is.False, "preference off");
            Assert.That(AutomatedEditorQuit.ShouldHandleQuit(true, true, true, automated), Is.False, "batch mode");
            Assert.That(AutomatedEditorQuit.ShouldHandleQuit(true, true, false, normal), Is.False, "normal Editor");
        }

        [Test]
        public void Preference_IsOnByDefault_AndTogglesTheQuitPatch()
        {
            EditorPrefs.DeleteKey(UnityEditorPreferences.QuitAutomatedEditorKey);
            Assert.That(EditorFeatureToggles.QuitAutomatedEditor, Is.True);

            bool expectedWhenOn = AutomatedEditorQuit.ShouldHandleQuit(
                AutomatedEditorQuit.IsSupported, true, Application.isBatchMode, Environment.GetCommandLineArgs());

            EditorFeatureToggles.QuitAutomatedEditor = false;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.QuitAutomatedEditorKey, true), Is.False);
            Assert.That(AutomatedEditorQuit.IsInstalled, Is.False);

            EditorFeatureToggles.QuitAutomatedEditor = true;
            Assert.That(EditorPrefs.GetBool(UnityEditorPreferences.QuitAutomatedEditorKey, false), Is.True);
            Assert.That(AutomatedEditorQuit.IsInstalled, Is.EqualTo(expectedWhenOn), AutomatedEditorQuit.LastPatchError);
        }

#if UNITY_7000_0_OR_NEWER
        [Test]
        public void QuitRequest_IsCancelledOnce_ThenLeftToUnity()
        {
            Assert.That(AutomatedEditorQuit.IsQuitPending, Is.False);
            try
            {
                bool result = true;
                Assert.That(AutomatedEditorQuit.QuitRequestPrefix(ref result), Is.False, "Unity's own quit must be skipped.");
                Assert.That(result, Is.False, "Unity's own quit must be cancelled.");
                Assert.That(AutomatedEditorQuit.IsQuitPending, Is.True);

                result = true;
                Assert.That(AutomatedEditorQuit.QuitRequestPrefix(ref result), Is.True, "The replacement exit must not be intercepted.");
                Assert.That(result, Is.True);
            }
            finally
            {
                AutomatedEditorQuit.CancelPendingQuit();
            }

            Assert.That(AutomatedEditorQuit.IsQuitPending, Is.False);
        }
#endif
    }
}
