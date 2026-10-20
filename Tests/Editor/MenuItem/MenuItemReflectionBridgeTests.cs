using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.Tests.Editor.MenuItem
{
    public sealed class MenuItemReflectionBridgeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type Hider => Type.GetType(
            "MinimalEditor.MenuItem.Editor.MenuItemHider, MinimalEditor.MenuItem.Editor", throwOnError: true);

        [SetUp, TearDown]
        public void ResetMenuItemHiderState()
        {
            Invoke("SuppressMutationForTeardown");
            SetBool("s_failSafeMode", false);
            SetBool("s_isApplying", false);
            Invoke("Initialize");
            EditorApplication.delayCall -= CreateDelayCall("FlushQueuedApply");
            SetBool("s_applyQueued", false);
            SetBool("s_forceApplyQueued", false);
        }

        [Test]
        public void MenuItemSettingsProvider_IsProjectScoped()
        {
            var type = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemPreferencesProvider, MinimalEditor.MenuItem.Editor", throwOnError: true);
            var provider = (SettingsProvider)RequireMethod(type, "CreateProvider").Invoke(null, null);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Project/Minimal Editor/Menu Item"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.Project));
        }

        [Test]
        public void QueueApply_TeardownClearsWork_AndPreventsFurtherQueueing()
        {
            Invoke("QueueApply", true);
            Assert.That(GetBool("s_applyQueued"), Is.True);
            Assert.That(GetBool("s_forceApplyQueued"), Is.True);

            Invoke("SuppressMutationForTeardown");
            Assert.That(GetBool("s_mutationSuppressed"), Is.True);
            Assert.That(GetBool("s_applyQueued"), Is.False);
            Assert.That(GetBool("s_forceApplyQueued"), Is.False);

            Invoke("QueueApply", true);
            Assert.That(GetBool("s_applyQueued"), Is.False);
            Assert.That(GetBool("s_forceApplyQueued"), Is.False);
        }

        [Test]
        public void CancelledQuitRequest_KeepsMenuHidingActive()
        {
            // Invoke only this module's handlers and a veto, without requesting a real Editor quit.
            var callbacks = QuitRequestCallbacks().Where(callback => callback.Method.DeclaringType == Hider)
                .Cast<Func<bool>>().Concat(new Func<bool>[] { () => false });
            Assert.That(callbacks.All(callback => callback()), Is.False);
            Assert.That(GetBool("s_mutationSuppressed"), Is.False);

            Invoke("QueueApply", true);

            Assert.That(GetBool("s_applyQueued"), Is.True);
        }

        [Test]
        public void SettingsData_NullBundleEntriesAndList_PreserveUsableSettings()
        {
            var type = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemSettingsData, MinimalEditor.MenuItem.Editor", throwOnError: true);
            var data = Activator.CreateInstance(type, true);
            JsonUtility.FromJsonOverwrite(
                "{\"customDefinitionsText\":\"keep\",\"bundles\":[{\"id\":\"jobs\",\"enabled\":false}]}", data);
            var bundles = type.GetField("bundles", AnyInstance);
            var get = type.GetMethod("GetBundleEnabled", AnyInstance);
            var set = type.GetMethod("SetBundleEnabled", AnyInstance);
            ((IList)bundles.GetValue(data)).Insert(0, null);

            Assert.That(get.Invoke(data, new object[] { "jobs", true }), Is.False);
            set.Invoke(data, new object[] { "jobs", true });
            set.Invoke(data, new object[] { "tools", false });
            Assert.That(get.Invoke(data, new object[] { "jobs", false }), Is.True);
            Assert.That(get.Invoke(data, new object[] { "tools", true }), Is.False);

            bundles.SetValue(data, null);
            Assert.That(get.Invoke(data, new object[] { "jobs", true }), Is.True);
            set.Invoke(data, new object[] { "jobs", false });
            Assert.That(get.Invoke(data, new object[] { "jobs", true }), Is.False);
            Assert.That(type.GetField("customDefinitionsText", AnyInstance).GetValue(data), Is.EqualTo("keep"));
        }

        [Test]
        public void RepeatedInitialization_QueuesOnce_AndTeardownCancelsWork()
        {
            var callback = CreateDelayCall("FlushQueuedApply");
            Invoke("Initialize");
            Invoke("Initialize");
            Assert.That(EditorApplication.delayCall.GetInvocationList().Count(item => item.Equals(callback)), Is.EqualTo(1));

            Invoke("SuppressMutationForTeardown");
            Invoke("SuppressMutationForTeardown");
            Assert.That(EditorApplication.delayCall?.GetInvocationList().Count(item => item.Equals(callback)) ?? 0, Is.Zero);
            Assert.That(GetBool("s_applyQueued"), Is.False);
        }

        [Test]
        public void MenuRemovalAndEnumeration_HaveExpectedSignatures()
        {
            Assert.That(RequireMethod(typeof(Menu), "RemoveMenuItem", typeof(string)).ReturnType, Is.EqualTo(typeof(void)));
            Assert.That(RequireMethod(typeof(Menu), "ExtractSubmenus", typeof(string)).ReturnType, Is.EqualTo(typeof(string[])));
            var getItems = RequireMethod(typeof(Menu), "GetMenuItems", typeof(string), typeof(bool), typeof(bool));
            Assert.That(getItems.ReturnType.IsArray, Is.True);
            var path = getItems.ReturnType.GetElementType().GetProperty("path", AnyInstance);
            Assert.That(path, Is.Not.Null, "Menu items must expose their path.");
            Assert.That(path.PropertyType, Is.EqualTo(typeof(string)));
        }

        [Test]
        public void MenuRootScan_ExactRules_DoNotEnumerateMenus()
        {
            Assert.That(MenuRoots("Assets/Create/Material", "Custom/Command", "Jobs"), Is.Empty);
        }

        [Test]
        public void MenuRootScan_OnlyIncludesRootsNeededByWildcards()
        {
            Assert.That(MenuRoots("Assets/Create/Material", "Window/Analysis/*", " window/UI* ", "Custom/Tools/*"),
                Is.EquivalentTo(new[] { "Window", "Custom" }));
        }

        [Test]
        public void MenuRootScan_RootPrefixesPreserveKnownCustomRoots()
        {
            Assert.That(MenuRoots("Tool*", "Custom/Command", "Custom*", "*", null, ""),
                Is.EquivalentTo(new[] { "Tools", "Custom" }));
        }

        [TestCase("GameObject/Center On Children")]
        [TestCase(" GameObject/Center On Children/ ")]
        [TestCase("GameObject/Center On Children/*")]
        [TestCase("GameObject/Center On Children//*")]
        [TestCase("GameObject/Center On*")]
        [TestCase("GameObject/*")]
        public void ProtectedCommand_CannotBeHiddenThroughEquivalentRules(string rule)
        {
            var matcher = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemPatternMatcher, MinimalEditor.MenuItem.Editor", true);
            var paths = (IEnumerable<string>)RequireMethod(matcher, "SanitizeEntries", typeof(IEnumerable<string>))
                .Invoke(null, new object[] { new[] { rule, "Window/Analysis/*" } });

            Assert.That(paths, Is.EqualTo(new[] { "Window/Analysis/*" }));
        }

        [Test]
        public void ManualRules_CanReuseCurrentList()
        {
            WithManualRules(list =>
            {
                SetMenuItems(list);
                Assert.That(list, Is.EqualTo(new[] { "Tools/Keep" }));
            });
        }

        [Test]
        public void ManualRules_FailedEnumerationPreservesPreviousConfiguration()
        {
            WithManualRules(list =>
            {
                SetBool("s_useManualMenuItems", false);
                var exception = Assert.Throws<TargetInvocationException>(() => SetMenuItems(ThrowingPaths()));
                Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
                Assert.That(list, Is.EqualTo(new[] { "Tools/Keep" }));
                Assert.That(GetBool("s_useManualMenuItems"), Is.False);
            });
        }

        [Test]
        public void CustomDefinitionCache_RefreshesWhenSettingsTextChanges()
        {
            var settings = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemModuleSettings, MinimalEditor.MenuItem.Editor", true);
            var dataField = settings.GetField("s_data", AnyStatic);
            var originalData = dataField.GetValue(null);
            var data = Activator.CreateInstance(dataField.FieldType, true);
            var text = dataField.FieldType.GetField("customDefinitionsText", AnyInstance);
            var getEntries = RequireMethod(settings, "GetCustomDefinitionEntries");
            var warningKind = Type.GetType(
                "MinimalEditor.MenuItem.Editor.MenuItemBlockedEntryKind, MinimalEditor.MenuItem.Editor", true);
            var getWarnings = RequireMethod(settings, "GetCustomDefinitionWarnings", warningKind);
            try
            {
                dataField.SetValue(null, data);
                text.SetValue(data, "Services/*\n# ignored\n'Window/Analysis/*'");
                var entries = getEntries.Invoke(null, null);
                Assert.That((IEnumerable<string>)entries, Is.EqualTo(new[] { "Services/*", "Window/Analysis/*" }));
                Assert.That(getEntries.Invoke(null, null), Is.SameAs(entries));
                var warnings = getWarnings.Invoke(null, new[] { Enum.Parse(warningKind, "Unsupported") });
                Assert.That(((IEnumerable<string>)warnings).Count(), Is.EqualTo(1));
                Assert.That(getWarnings.Invoke(null, new[] { Enum.Parse(warningKind, "Unsupported") }), Is.SameAs(warnings));

                text.SetValue(data, "Tools/Keep");
                Assert.That((IEnumerable<string>)getEntries.Invoke(null, null), Is.EqualTo(new[] { "Tools/Keep" }));
                Assert.That((IEnumerable<string>)getWarnings.Invoke(null, new[] { Enum.Parse(warningKind, "Unsupported") }),
                    Is.Empty);
            }
            finally
            {
                dataField.SetValue(null, originalData);
            }
        }

#if UNITY_EDITOR_OSX
        [Test]
        public void NativeRootPolling_OnlySubscribesOnceWhileWholeRootRulesExist()
        {
            var configure = RequireMethod(Hider, "ConfigureNativeRootVisibility", typeof(IReadOnlyList<string>));
            var update = typeof(EditorApplication).GetField("update", AnyStatic);
            Assert.That(update, Is.Not.Null);
            int SubscriptionCount() => (update.GetValue(null) as Delegate)?.GetInvocationList().Count(
                callback => callback.Method.DeclaringType == Hider &&
                            callback.Method.Name == "UpdateNativeRootVisibility") ?? 0;

            configure.Invoke(null, new object[] { new[] { "Minimal Editor Test Root/*" } });
            configure.Invoke(null, new object[] { new[] { "Minimal Editor Test Root/*" } });
            Assert.That(SubscriptionCount(), Is.EqualTo(1));

            configure.Invoke(null, new object[] { new[] { "Tools/Exact Command" } });
            Assert.That(SubscriptionCount(), Is.Zero);

            configure.Invoke(null, new object[] { new[] { "Minimal Editor Test Root/*" } });
            Invoke("SuppressMutationForTeardown");
            Assert.That(SubscriptionCount(), Is.Zero);
        }
#endif

        static IEnumerable<string> MenuRoots(params string[] rules) =>
            (IEnumerable<string>)RequireMethod(Hider, "BuildMenuRootSet", typeof(IReadOnlyList<string>))
                .Invoke(null, new object[] { rules });

        static void SetMenuItems(IEnumerable<string> items) =>
            RequireMethod(Hider, "SetMenuItems", typeof(IEnumerable<string>)).Invoke(null, new object[] { items });

        static IEnumerable<string> ThrowingPaths()
        {
            yield return "Tools/Partial";
            throw new InvalidOperationException("Incomplete input");
        }

        static void WithManualRules(Action<List<string>> assertion)
        {
            var list = (List<string>)RequireField("s_manualMenuItemsToHide").GetValue(null);
            var originalItems = list.ToArray();
            var originalManualState = GetBool("s_useManualMenuItems");
            Invoke("SuppressMutationForTeardown");
            try
            {
                SetMenuItems(new[] { "Tools/Keep" });
                assertion(list);
            }
            finally
            {
                list.Clear();
                list.AddRange(originalItems);
                SetBool("s_useManualMenuItems", originalManualState);
            }
        }

        static bool GetBool(string name) => (bool)RequireField(name).GetValue(null);
        static void SetBool(string name, bool value) => RequireField(name).SetValue(null, value);

        static IEnumerable<Delegate> QuitRequestCallbacks()
        {
            var field = typeof(EditorApplication).GetField("wantsToQuit", AnyStatic);
            if (field != null)
                return (field.GetValue(null) as Delegate)?.GetInvocationList() ?? Array.Empty<Delegate>();

            var tracker = typeof(EditorApplication).GetField("m_WantsToQuitEvent", AnyStatic)?.GetValue(null);
            Assert.That(tracker, Is.Not.Null);
            var enumerator = (IEnumerator)tracker.GetType().GetMethod("GetEnumerator").Invoke(tracker, null);
            var callbacks = new List<Delegate>();
            try
            {
                while (enumerator.MoveNext()) callbacks.Add((Delegate)enumerator.Current);
            }
            finally
            {
                (enumerator as IDisposable)?.Dispose();
            }
            return callbacks;
        }

        static void Invoke(string name, params object[] arguments)
        {
            var method = RequireMethod(Hider, name, arguments.Select(value => value.GetType()).ToArray());
            method.Invoke(null, arguments);
        }

        static EditorApplication.CallbackFunction CreateDelayCall(string name) =>
            (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction), RequireMethod(Hider, name));

        static FieldInfo RequireField(string name)
        {
            var field = Hider.GetField(name, AnyStatic);
            Assert.That(field, Is.Not.Null, name);
            return field;
        }

        static MethodInfo RequireMethod(Type type, string name, params Type[] arguments)
        {
            var method = type.GetMethod(name, AnyStatic, null, arguments, null);
            Assert.That(method, Is.Not.Null, type.FullName + "." + name);
            return method;
        }
    }
}
