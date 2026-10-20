using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Essentials.Tests.Editor.MenuItem
{
    public sealed class MenuItemReflectionBridgeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static Type Hider => Type.GetType(
            "Essentials.MenuItem.Editor.MenuItemHider, Essentials.MenuItem.Editor", throwOnError: true);

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
                "Essentials.MenuItem.Editor.MenuItemPreferencesProvider, Essentials.MenuItem.Editor", throwOnError: true);
            var provider = (SettingsProvider)RequireMethod(type, "CreateProvider").Invoke(null, null);
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Project/Essentials/Menu Item"));
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
                "Essentials.MenuItem.Editor.MenuItemSettingsData, Essentials.MenuItem.Editor", throwOnError: true);
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
