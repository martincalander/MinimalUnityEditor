using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace MartinCalander.Essentials.Tests.Editor.ReflectionBridges
{
    /// <summary>
    /// Verifies that Unity's internal Menu API — accessed via reflection in
    /// MenuItemHider — still matches what the production code expects after a Unity update.
    ///
    /// A failure here means menu item hiding will silently stop working or throw exceptions.
    /// </summary>
    public sealed class MenuItemReflectionBridgeTests
    {
        const BindingFlags AnyStatic   = BindingFlags.Static   | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

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
    }
}
