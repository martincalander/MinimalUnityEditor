using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace MinimalEditor.Tests.Editor.PackageContract
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class MinimalEditorFastPlayModeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        static object s_domainMarker;

        bool m_optionsCaptured;
        bool m_previousOptionsEnabled;
        EnterPlayModeOptions m_previousOptions;
        PropertyInfo m_titleInput;
        object m_previousTitleInput;
        int m_titleCalls;

        [UnityTest]
        public IEnumerator RepeatedPlayMode_WithoutDomainReload_PreservesEditorServices()
        {
            return RunPlayModeCycles(disableSceneReload: false);
        }

        [UnityTest]
        public IEnumerator RepeatedPlayMode_WithoutDomainOrSceneReload_PreservesEditorServices()
        {
            return RunPlayModeCycles(disableSceneReload: true);
        }

        [UnityTearDown]
        public IEnumerator RestoreEditorState()
        {
            try
            {
                if (EditorApplication.isPlayingOrWillChangePlaymode)
                    yield return new ExitPlayMode();
            }
            finally
            {
                if (m_optionsCaptured)
                {
                    EditorSettings.enterPlayModeOptions = m_previousOptions;
                    EditorSettings.enterPlayModeOptionsEnabled = m_previousOptionsEnabled;
                    m_optionsCaptured = false;
                }

                if (m_titleInput != null)
                {
                    m_titleInput.SetValue(null, m_previousTitleInput);
                    m_titleInput = null;
                }

                s_domainMarker = null;
            }
        }

        IEnumerator RunPlayModeCycles(bool disableSceneReload)
        {
            Assert.That(EditorApplication.isPlayingOrWillChangePlaymode, Is.False);
            m_previousOptionsEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            m_previousOptions = EditorSettings.enterPlayModeOptions;
            m_optionsCaptured = true;

            var assertions = CaptureServiceAssertions();
            var marker = new object();
            s_domainMarker = marker;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload |
                (disableSceneReload ? EnterPlayModeOptions.DisableSceneReload : EnterPlayModeOptions.None);
            EditorSettings.enterPlayModeOptionsEnabled = true;

            AssertServices(assertions);
            for (int cycle = 1; cycle <= 3; cycle++)
            {
                yield return new EnterPlayMode(expectDomainReload: false);

                Assert.That(EditorApplication.isPlaying, Is.True);
                Assert.That(s_domainMarker, Is.SameAs(marker), "Entering Play Mode unexpectedly reloaded code.");
                AssertServices(assertions);

                yield return new ExitPlayMode();

                Assert.That(EditorApplication.isPlaying, Is.False);
                Assert.That(s_domainMarker, Is.SameAs(marker), "Exiting Play Mode unexpectedly reloaded code.");
                AssertServices(assertions);
            }
        }

        List<Action> CaptureServiceAssertions()
        {
            var assertions = new List<Action>();
#if !MINIMAL_EDITOR_DISABLE_CONSOLE
            Type consoleSettings = RequireType("MinimalEditor.Console.Editor.ConsoleLogFilterSettings, MinimalEditor.Console.Editor");
            object consoleData = Property(consoleSettings, "Data").GetValue(null);
            string consoleSnapshot = JsonUtility.ToJson(Property(consoleSettings, "Snapshot").GetValue(null));
            Type installer = RequireType("MinimalEditor.Console.Editor.ConsoleLogFilterInstaller, MinimalEditor.Console.Editor");
            object logHandler = Field(installer, "s_activeFilteringLogHandler").GetValue(null);
            Assert.That(logHandler, Is.Not.Null);
            Type toolbar = RequireType("MinimalEditor.Console.Editor.ConsoleAssistantToolbarButtonController, MinimalEditor.Console.Editor");
            Type hyperlinks = RequireType("MinimalEditor.Console.Editor.ConsoleHyperlinks, MinimalEditor.Console.Editor");
            assertions.Add(() =>
            {
                Assert.That(Property(consoleSettings, "Data").GetValue(null), Is.SameAs(consoleData));
                Assert.That(JsonUtility.ToJson(Property(consoleSettings, "Snapshot").GetValue(null)), Is.EqualTo(consoleSnapshot));
                Assert.That(Field(installer, "s_activeFilteringLogHandler").GetValue(null), Is.SameAs(logHandler));
                AssertCallbackCount(typeof(EditorApplication), "update", installer, "OnEditorUpdate", 1);
                AssertCallbackCount(consoleSettings, "SettingsChanged", installer, "ApplySettings", 1);
                AssertCallbackCount(consoleSettings, "SettingsChanged", toolbar, "ApplySettings", 1);
                AssertCallbackCount(typeof(EditorGUI), "hyperLinkClicked", hyperlinks, "OnHyperlinkClicked", 1);
            });
#endif
#if !MINIMAL_EDITOR_DISABLE_MENU_ITEM
            Type menuSettings = RequireType("MinimalEditor.MenuItem.Editor.MenuItemModuleSettings, MinimalEditor.MenuItem.Editor");
            object menuData = Property(menuSettings, "Data").GetValue(null);
            string menuSnapshot = JsonUtility.ToJson(menuData);
            Type menuHider = RequireType("MinimalEditor.MenuItem.Editor.MenuItemHider, MinimalEditor.MenuItem.Editor");
            int menuCallbackCount = (bool)Property(menuSettings, "IsSupportedInCurrentEditor").GetValue(null) ? 1 : 0;
            assertions.Add(() =>
            {
                Assert.That(Property(menuSettings, "Data").GetValue(null), Is.SameAs(menuData));
                Assert.That(JsonUtility.ToJson(Property(menuSettings, "Data").GetValue(null)), Is.EqualTo(menuSnapshot));
                AssertCallbackCount(typeof(EditorApplication), "projectChanged", menuHider, "OnProjectChanged", menuCallbackCount);
                AssertCallbackCount(typeof(EditorApplication), "wantsToQuit", menuHider, "OnWantsToQuit", 0);
                AssertCallbackCount(typeof(EditorApplication), "quitting", menuHider, "SuppressMutationForTeardown", menuCallbackCount);
            });
#endif
#if !MINIMAL_EDITOR_DISABLE_UNITY_EDITOR && !MINIMAL_EDITOR_DISABLE_MENU_ITEM
            Type title = RequireType("MinimalEditor.Editor.WindowTitleShortener, MinimalEditor.Editor");
            Type statusBar = RequireType("MinimalEditor.Editor.StatusBarExtrasHider, MinimalEditor.Editor");
            m_titleInput = Property(title, "ActiveInput");
            m_previousTitleInput = m_titleInput.GetValue(null);
            m_titleInput.SetValue(null, new Func<bool>(() =>
            {
                m_titleCalls++;
                return true;
            }));
            MethodInfo buildTitle = Method(typeof(EditorApplication), "BuildMainWindowTitle");
            assertions.Add(() =>
            {
                AssertCallbackCount(typeof(EditorApplication), "updateMainWindowTitle", title, "OnUpdateMainWindowTitle", 1);
                AssertCallbackCount(typeof(EditorApplication), "update", statusBar, "PollFallback", 1);
                int previousCalls = m_titleCalls;
                object result = buildTitle.Invoke(null, null);
                Assert.That(m_titleCalls - previousCalls, Is.EqualTo(1), "The title callback must run once per title refresh.");
                if (!string.IsNullOrWhiteSpace(PlayerSettings.productName))
                    Assert.That(result, Is.EqualTo(PlayerSettings.productName));
            });
#endif
#if UNITY_6000_0_OR_NEWER && !MINIMAL_EDITOR_DISABLE_PACKAGE_MANAGER
            Type patch = RequireType("MinimalEditor.PackageManager.Editor.PackageManagerAssetStoreDuplicatePatch, MinimalEditor.PackageManager.Editor");
            assertions.Add(() =>
            {
                AssertSinglePostfix(patch, "GetTargetMethod", "GetPostfixMethod");
                AssertSinglePostfix(patch, "GetPackageToolbarRefreshMethod", "GetPackageToolbarRefreshPostfixMethod");
            });
#endif
            if (assertions.Count == 0)
                Assert.Ignore("No Minimal Editor modules with lifecycle services are enabled.");
            return assertions;
        }

        static void AssertServices(IEnumerable<Action> assertions)
        {
            foreach (Action assertion in assertions)
                assertion();
        }

        static void AssertCallbackCount(Type publisher, string eventName, Type subscriber, string methodName, int expected)
        {
            int count = EventCallbacks(publisher, eventName).Count(
                handler => handler.Method.DeclaringType == subscriber && handler.Method.Name == methodName);
            Assert.That(count, Is.EqualTo(expected), $"{subscriber.Name}.{methodName} registrations on {publisher.Name}.{eventName}");
        }

        static IEnumerable<Delegate> EventCallbacks(Type publisher, string eventName)
        {
            FieldInfo field = publisher.GetField(eventName, AnyStatic);
            if (field != null)
                return (field.GetValue(null) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();

            // Newer Editors store these events in a performance-tracked collection.
            string trackerName = eventName == "projectChanged" ? "m_ProjectChangedEvent" :
                eventName == "wantsToQuit" ? "m_WantsToQuitEvent" :
                eventName == "quitting" ? "m_QuittingEvent" : null;
            Assert.That(trackerName, Is.Not.Null, $"Unknown event storage: {publisher.Name}.{eventName}");
            object tracker = Field(publisher, trackerName).GetValue(null);
            MethodInfo getEnumerator = tracker.GetType().GetMethod("GetEnumerator");
            Assert.That(getEnumerator, Is.Not.Null);
            var enumerator = (IEnumerator)getEnumerator.Invoke(tracker, null);
            var callbacks = new List<Delegate>();
            try
            {
                while (enumerator.MoveNext())
                    callbacks.Add((Delegate)enumerator.Current);
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
            return callbacks;
        }

        static void AssertSinglePostfix(Type patch, string targetMethodName, string postfixMethodName)
        {
            var target = (MethodInfo)Method(patch, targetMethodName).Invoke(null, null);
            var postfix = (MethodInfo)Method(patch, postfixMethodName).Invoke(null, null);
            Assert.That(target, Is.Not.Null, targetMethodName);
            Assert.That(postfix, Is.Not.Null, postfixMethodName);
            Type harmony = RequireType("HarmonyLib.Harmony, 0Harmony");
            object patchInfo = Method(harmony, "GetPatchInfo").Invoke(null, new object[] { target });
            Assert.That(patchInfo, Is.Not.Null, $"No patches remain on {target.Name}.");
            var postfixes = (IEnumerable)patchInfo.GetType().GetField("Postfixes").GetValue(patchInfo);
            string owner = (string)Field(patch, "HarmonyId").GetValue(null);
            int count = postfixes.Cast<object>().Count(entry =>
                (string)entry.GetType().GetField("owner").GetValue(entry) == owner &&
                Equals(entry.GetType().GetProperty("PatchMethod").GetValue(entry), postfix));
            Assert.That(count, Is.EqualTo(1), $"Owned postfixes on {target.DeclaringType?.Name}.{target.Name}");
        }

        static Type RequireType(string name)
        {
            Type type = Type.GetType(name);
            Assert.That(type, Is.Not.Null, $"Enabled module type is missing: {name}");
            return type;
        }

        static FieldInfo Field(Type type, string name)
        {
            FieldInfo field = type.GetField(name, AnyStatic);
            Assert.That(field, Is.Not.Null, $"{type.FullName}.{name}");
            return field;
        }

        static PropertyInfo Property(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name, AnyStatic);
            Assert.That(property, Is.Not.Null, $"{type.FullName}.{name}");
            return property;
        }

        static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = type.GetMethod(name, AnyStatic);
            Assert.That(method, Is.Not.Null, $"{type.FullName}.{name}");
            return method;
        }
    }
}
