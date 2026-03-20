using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace MartinCalander.Essentials.Tests.Editor.ReflectionBridges
{
    /// <summary>
    /// Verifies that the HostView + WindowAction internals used by the Hierarchy auto-save
    /// pane-menu integration still match what the production code expects.
    /// </summary>
    public sealed class WindowActionReflectionBridgeTests
    {
        static readonly Assembly UnityEditorAssembly = typeof(UnityEditor.Editor).Assembly;

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void HostView_TypeExists_InUnityEditorAssembly()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.HostView");
            Assert.That(type, Is.Not.Null,
                "UnityEditor.HostView was not found. The Hierarchy auto-save menu cannot register its pane actions.");
        }

        [Test]
        public void HostView_s_windowActions_PrivateStaticFieldExists()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.HostView");
            Assume.That(type, Is.Not.Null);

            FieldInfo field = type.GetField("s_windowActions", AnyStatic);
            Assert.That(field, Is.Not.Null,
                "HostView.s_windowActions was not found. The Hierarchy auto-save menu cannot append custom pane actions.");
        }

        [Test]
        public void HostView_windowActions_PrivateStaticPropertyExists()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.HostView");
            Assume.That(type, Is.Not.Null);

            PropertyInfo property = type.GetProperty("windowActions", AnyStatic);
            Assert.That(property, Is.Not.Null,
                "HostView.windowActions was not found. The Hierarchy auto-save menu cannot read the existing pane actions.");
        }

        [Test]
        public void WindowAction_TypeExists_InUnityEditorAssembly()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.WindowAction");
            Assert.That(type, Is.Not.Null,
                "UnityEditor.WindowAction was not found. The Hierarchy auto-save menu cannot create pane actions.");
        }

        [Test]
        public void WindowAction_CreateWindowMenuItem_StaticMethodExists()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.WindowAction");
            Assume.That(type, Is.Not.Null);

            Type executeHandlerType = type.GetNestedType("ExecuteHandler", AnyStatic);
            Assume.That(executeHandlerType, Is.Not.Null);

            MethodInfo method = type.GetMethod(
                "CreateWindowMenuItem",
                AnyStatic,
                binder: null,
                types: new[] { typeof(string), executeHandlerType, typeof(string) },
                modifiers: null);

            Assert.That(method, Is.Not.Null,
                "WindowAction.CreateWindowMenuItem(string, ExecuteHandler, string) was not found. " +
                "The Hierarchy auto-save pane action cannot be created.");
        }

        [Test]
        public void WindowAction_ExecuteHandler_And_ValidateHandler_NestedDelegatesExist()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.WindowAction");
            Assume.That(type, Is.Not.Null);

            Type executeHandlerType = type.GetNestedType("ExecuteHandler", AnyInstance | BindingFlags.Static);
            Type validateHandlerType = type.GetNestedType("ValidateHandler", AnyInstance | BindingFlags.Static);

            Assert.That(executeHandlerType, Is.Not.Null,
                "WindowAction.ExecuteHandler was not found. The Hierarchy auto-save pane action cannot bind its execute callback.");
            Assert.That(validateHandlerType, Is.Not.Null,
                "WindowAction.ValidateHandler was not found. The Hierarchy auto-save pane action cannot restrict itself to the Hierarchy window.");
        }

        [Test]
        public void WindowAction_HasExpectedEditableFields()
        {
            Type type = UnityEditorAssembly.GetType("UnityEditor.WindowAction");
            Assume.That(type, Is.Not.Null);

            FieldInfo idField = type.GetField("id", AnyInstance);
            FieldInfo menuPathField = type.GetField("menuPath", AnyInstance);
            FieldInfo priorityField = type.GetField("priority", AnyInstance);
            FieldInfo validateHandlerField = type.GetField("validateHandler", AnyInstance);

            Assert.That(idField, Is.Not.Null,
                "WindowAction.id was not found. The Hierarchy auto-save pane action cannot avoid duplicate registration.");
            Assert.That(menuPathField, Is.Not.Null,
                "WindowAction.menuPath was not found. The Hierarchy auto-save pane action cannot keep its label in sync.");
            Assert.That(priorityField, Is.Not.Null,
                "WindowAction.priority was not found. The Hierarchy auto-save pane action cannot control menu grouping.");
            Assert.That(validateHandlerField, Is.Not.Null,
                "WindowAction.validateHandler was not found. The Hierarchy auto-save pane action cannot hide itself outside the Hierarchy.");
        }
    }
}
