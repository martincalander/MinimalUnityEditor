using System;
using System.Reflection;
using NUnit.Framework;

namespace Essentials.Tests.Editor.ReflectionBridges
{
    /// <summary>
    /// Guards the intentionally menu-free Hierarchy auto-save surface. Auto-save is configured
    /// through Preferences and no longer depends on Unity's private HostView/WindowAction APIs.
    /// </summary>
    public sealed class WindowActionReflectionBridgeTests
    {
        const string HierarchySceneAutoSaveTypeName =
            "Essentials.Editor.HierarchySceneAutoSave, Essentials.Editor";
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly string[] RemovedWindowActionMembers =
        {
            "EnsureWindowActionsRegistered",
            "CreateWindowAction",
            "EnableActionExecute",
            "DisableActionExecute",
            "SaveNowActionExecute",
            "CreateWindowMenuItemMethod",
            "WindowActionsField"
        };

        [Test]
        public void HierarchySceneAutoSave_HasNoWindowActionRegistrationBridge()
        {
            Type type = Type.GetType(HierarchySceneAutoSaveTypeName);
            Assert.That(type, Is.Not.Null,
                "HierarchySceneAutoSave could not be resolved from the Essentials.Editor assembly.");

            foreach (string memberName in RemovedWindowActionMembers)
            {
                Assert.That(type.GetMember(memberName, AnyStatic), Is.Empty,
                    $"HierarchySceneAutoSave should not retain the removed WindowAction member '{memberName}'.");
            }
        }
    }
}
