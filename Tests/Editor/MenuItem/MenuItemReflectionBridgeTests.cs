using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace Essentials.Tests.Editor.MenuItem
{
    /// <summary>
    /// Verifies that Unity's internal Menu API — accessed via reflection in
    /// MenuItemHider — still matches what the production code expects after a Unity update.
    ///
    /// A failure here means menu item hiding will silently stop working or throw exceptions.
    /// </summary>
    public sealed class MenuItemReflectionBridgeTests
    {
        const string MenuItemHiderTypeName = "Essentials.MenuItem.Editor.MenuItemHider, Essentials.MenuItem.Editor";
        const string MenuItemPreferencesProviderTypeName =
            "Essentials.MenuItem.Editor.MenuItemPreferencesProvider, Essentials.MenuItem.Editor";
        const BindingFlags AnyStatic   = BindingFlags.Static   | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void RedundantApplyMenuItemPath_IsNotRegistered()
        {
            Type type = Type.GetType(MenuItemHiderTypeName);
            Assert.That(type, Is.Not.Null);

            foreach (MethodInfo method in type.GetMethods(AnyStatic))
            {
                foreach (CustomAttributeData attribute in method.GetCustomAttributesData())
                {
                    if (attribute.AttributeType != typeof(global::UnityEditor.MenuItem) || attribute.ConstructorArguments.Count == 0)
                        continue;

                    Assert.That(attribute.ConstructorArguments[0].Value, Is.Not.EqualTo("Help/Apply Menu Item Hider"),
                        $"{type.FullName}.{method.Name} should not register the redundant apply command.");
                }
            }
        }

        [Test]
        public void MenuItemSettingsProvider_IsProjectScoped()
        {
            var providerType = Type.GetType(MenuItemPreferencesProviderTypeName);
            Assert.That(providerType, Is.Not.Null,
                "MenuItemPreferencesProvider could not be resolved from the Essentials.MenuItem.Editor assembly.");

            var createProvider = providerType.GetMethod("CreateProvider", AnyStatic);
            Assert.That(createProvider, Is.Not.Null,
                "MenuItemPreferencesProvider.CreateProvider() was not found.");

            var provider = createProvider.Invoke(null, null) as SettingsProvider;
            Assert.That(provider, Is.Not.Null);
            Assert.That(provider.settingsPath, Is.EqualTo("Project/Essentials/Menu Item"));
            Assert.That(provider.scope, Is.EqualTo(SettingsScope.Project));
        }

        [SetUp]
        public void SetUp()
        {
            ResetMenuItemHiderState();
        }

        [TearDown]
        public void TearDown()
        {
            ResetMenuItemHiderState();
        }

        [Test]
        public void MenuItemHider_SuppressMutationForTeardown_SetsSuppressedState_AndClearsQueuedFlags()
        {
            SetMenuItemHiderBool("s_applyQueued", true);
            SetMenuItemHiderBool("s_forceApplyQueued", true);

            InvokeMenuItemHiderParameterlessMethod("SuppressMutationForTeardown");

            Assert.That(GetMenuItemHiderBool("s_mutationSuppressed"), Is.True,
                "SuppressMutationForTeardown() should mark menu mutation as suppressed.");
            Assert.That(GetMenuItemHiderBool("s_applyQueued"), Is.False,
                "SuppressMutationForTeardown() should clear any queued apply state.");
            Assert.That(GetMenuItemHiderBool("s_forceApplyQueued"), Is.False,
                "SuppressMutationForTeardown() should clear any queued forced apply state.");
        }

        [Test]
        public void MenuItemHider_QueueApply_Force_DoesNothing_WhenMutationIsSuppressed()
        {
            SetMenuItemHiderBool("s_mutationSuppressed", true);

            InvokeMenuItemHiderBoolMethod("QueueApply", true);

            Assert.That(GetMenuItemHiderBool("s_applyQueued"), Is.False,
                "QueueApply(force: true) should not queue work once teardown suppression is active.");
            Assert.That(GetMenuItemHiderBool("s_forceApplyQueued"), Is.False,
                "QueueApply(force: true) should not remember a forced apply once teardown suppression is active.");
        }

        [Test]
        public void MenuItemHider_QueueApply_Force_Queues_WhenMutationIsNotSuppressed()
        {
            InvokeMenuItemHiderBoolMethod("QueueApply", true);

            Assert.That(GetMenuItemHiderBool("s_applyQueued"), Is.True,
                "QueueApply(force: true) should queue a delayed apply during normal editor operation.");
            Assert.That(GetMenuItemHiderBool("s_forceApplyQueued"), Is.True,
                "QueueApply(force: true) should remember that the pending apply is forced during normal editor operation.");
        }

        [Test]
        public void MenuItemHider_RepeatedInitialization_QueuesOnce_AndTeardownCancelsWork()
        {
            var callback = CreateDelayCall("FlushQueuedApply");

            InvokeMenuItemHiderParameterlessMethod("Initialize");
            InvokeMenuItemHiderParameterlessMethod("Initialize");

            Assert.That(EditorApplication.delayCall.GetInvocationList().Count(item => item.Equals(callback)), Is.EqualTo(1));
            InvokeMenuItemHiderParameterlessMethod("SuppressMutationForTeardown");
            InvokeMenuItemHiderParameterlessMethod("SuppressMutationForTeardown");
            Assert.That(EditorApplication.delayCall?.GetInvocationList().Count(item => item.Equals(callback)) ?? 0, Is.Zero);
            Assert.That(GetMenuItemHiderBool("s_applyQueued"), Is.False);
        }

        // ── Menu.RemoveMenuItem ──────────────────────────────────────────────
        // Production code: MenuItemHider.RemoveMenuItemMethod

        [Test]
        public void Menu_RemoveMenuItem_StaticMethodExists()
        {
            var method = typeof(Menu).GetMethod("RemoveMenuItem", AnyStatic);
            Assert.That(method, Is.Not.Null,
                "Menu.RemoveMenuItem() was not found. MenuItemHider cannot remove any menu items — " +
                "the feature will silently do nothing.");
        }

        // ── Menu.ExtractSubmenus ─────────────────────────────────────────────
        // Production code: MenuItemHider.ExtractSubmenusMethod — used for wildcard path enumeration

        [Test]
        public void Menu_ExtractSubmenus_StaticMethodExists_WithStringParameter()
        {
            var method = typeof(Menu).GetMethod(
                "ExtractSubmenus",
                AnyStatic,
                null,
                new[] { typeof(string) },
                null);

            Assert.That(method, Is.Not.Null,
                "Menu.ExtractSubmenus(string) was not found. Wildcard menu path expansion will fall back " +
                "to Unsupported.GetSubmenus only, which may miss some entries.");
        }

        [Test]
        public void Menu_ExtractSubmenus_ReturnsStringArray()
        {
            var method = typeof(Menu).GetMethod(
                "ExtractSubmenus",
                AnyStatic,
                null,
                new[] { typeof(string) },
                null);

            Assume.That(method, Is.Not.Null);
            Assert.That(method.ReturnType, Is.EqualTo(typeof(string[])),
                $"Menu.ExtractSubmenus(string) return type changed from string[] to {method.ReturnType.FullName}. " +
                "The result cannot be cast to string[] — wildcard path enumeration will fail.");
        }

        // ── Menu.GetMenuItems ────────────────────────────────────────────────
        // Production code: MenuItemHider.GetMenuItemsMethod — used to enumerate existing menu paths

        [Test]
        public void Menu_GetMenuItems_StaticMethodExists_WithStringBoolBoolParameters()
        {
            var method = typeof(Menu).GetMethod(
                "GetMenuItems",
                AnyStatic,
                null,
                new[] { typeof(string), typeof(bool), typeof(bool) },
                null);

            Assert.That(method, Is.Not.Null,
                "Menu.GetMenuItems(string, bool, bool) was not found. " +
                "MenuItemHider cannot enumerate menu paths for wildcard matching.");
        }

        [Test]
        public void Menu_GetMenuItems_ReturnType_IsArray()
        {
            var method = typeof(Menu).GetMethod(
                "GetMenuItems",
                AnyStatic,
                null,
                new[] { typeof(string), typeof(bool), typeof(bool) },
                null);

            Assume.That(method, Is.Not.Null);
            Assert.That(method.ReturnType.IsArray, Is.True,
                $"Menu.GetMenuItems(string, bool, bool) no longer returns an array (got {method.ReturnType.FullName}). " +
                "The result cannot be iterated — menu path enumeration will fail.");
        }

        // ── ScriptingMenuItem.path ───────────────────────────────────────────
        // Production code: MenuItemHider.ScriptingMenuItemPathProperty

        [Test]
        public void GetMenuItems_ReturnedElementType_HasPathProperty_AsString()
        {
            var method = typeof(Menu).GetMethod(
                "GetMenuItems",
                AnyStatic,
                null,
                new[] { typeof(string), typeof(bool), typeof(bool) },
                null);

            Assume.That(method, Is.Not.Null);
            Assume.That(method.ReturnType.IsArray, Is.True);

            var elementType = method.ReturnType.GetElementType();
            Assert.That(elementType, Is.Not.Null,
                "GetMenuItems return type has no element type. Cannot inspect menu item path.");

            var pathProperty = elementType.GetProperty("path", AnyInstance);
            Assert.That(pathProperty, Is.Not.Null,
                $"The 'path' property was not found on {elementType.FullName}. " +
                "MenuItemHider cannot read menu item paths — wildcard matching will not work.");
            Assert.That(pathProperty.PropertyType, Is.EqualTo(typeof(string)),
                $"The 'path' property on {elementType.FullName} is no longer a string. " +
                "Menu path extraction will be broken.");
        }

        static void ResetMenuItemHiderState()
        {
            InvokeMenuItemHiderParameterlessMethod("SuppressMutationForTeardown");
            SetMenuItemHiderBool("s_failSafeMode", false);
            SetMenuItemHiderBool("s_isApplying", false);
            InvokeMenuItemHiderParameterlessMethod("Initialize");
            EditorApplication.delayCall -= CreateDelayCall("FlushQueuedApply");
            SetMenuItemHiderBool("s_applyQueued", false);
            SetMenuItemHiderBool("s_forceApplyQueued", false);
        }

        static bool GetMenuItemHiderBool(string fieldName)
        {
            return (bool)GetRequiredMenuItemHiderField(fieldName).GetValue(null);
        }

        static void SetMenuItemHiderBool(string fieldName, bool value)
        {
            GetRequiredMenuItemHiderField(fieldName).SetValue(null, value);
        }

        static void InvokeMenuItemHiderParameterlessMethod(string methodName)
        {
            GetRequiredMenuItemHiderMethod(methodName).Invoke(null, null);
        }

        static void InvokeMenuItemHiderBoolMethod(string methodName, bool argument)
        {
            GetRequiredMenuItemHiderMethod(methodName, typeof(bool)).Invoke(null, new object[] { argument });
        }

        static EditorApplication.CallbackFunction CreateDelayCall(string methodName)
        {
            return (EditorApplication.CallbackFunction)Delegate.CreateDelegate(
                typeof(EditorApplication.CallbackFunction),
                GetRequiredMenuItemHiderMethod(methodName));
        }

        static FieldInfo GetRequiredMenuItemHiderField(string fieldName)
        {
            var field = GetRequiredMenuItemHiderType().GetField(fieldName, AnyStatic);
            Assert.That(field, Is.Not.Null,
                $"MenuItemHider field '{fieldName}' was not found. The shutdown-suppression tests can no longer inspect production state.");
            return field;
        }

        static MethodInfo GetRequiredMenuItemHiderMethod(string methodName, params Type[] parameterTypes)
        {
            var method = GetRequiredMenuItemHiderType().GetMethod(
                methodName,
                AnyStatic,
                null,
                parameterTypes ?? Type.EmptyTypes,
                null);

            Assert.That(method, Is.Not.Null,
                $"MenuItemHider method '{methodName}' was not found. The shutdown-suppression tests can no longer invoke production behavior.");
            return method;
        }

        static Type GetRequiredMenuItemHiderType()
        {
            var type = Type.GetType(MenuItemHiderTypeName);
            Assert.That(type, Is.Not.Null,
                "MenuItemHider type could not be resolved from the Essentials.MenuItem.Editor assembly.");
            return type;
        }
    }
}
