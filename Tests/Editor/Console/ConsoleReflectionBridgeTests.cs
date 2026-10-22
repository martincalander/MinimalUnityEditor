using System;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;

namespace MinimalEditor.Tests.Editor.Console
{
    // Guard the Unity internals used to read, classify and replace Console entries.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    public sealed class ConsoleReflectionBridgeTests
    {
        const BindingFlags AnyStatic = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        const BindingFlags AnyInstance = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        [TestCase("Clear")]
        [TestCase("GetCountsByType")]
        [TestCase("StartGettingEntries")]
        [TestCase("EndGettingEntries")]
        [TestCase("GetEntryInternal")]
        public void LogEntries_RequiredStaticMethodExists(string name)
        {
            Assert.That(RequireUnityType("LogEntries").GetMethod(name, AnyStatic), Is.Not.Null,
                $"LogEntries.{name} is required for Console entry maintenance.");
        }

        [Test]
        public void LogEntries_AddMessage_AtLeastOneVariantExists_WithSingleParameter()
        {
            var candidateNames = new[]
            {
                "AddMessageWithDoubleClickCallback",
                "AddMessage",
                "AddMessageWithDoubleClickCallbackInternal"
            };
            var methods = RequireUnityType("LogEntries").GetMethods(AnyStatic);

            Assert.That(methods.Any(method =>
                candidateNames.Contains(method.Name) && method.GetParameters().Length == 1), Is.True,
                "No supported AddMessage variant exists to restore filtered Console entries.");
        }

        [TestCase("message", typeof(string))]
        [TestCase("file", typeof(string))]
        [TestCase("callstackTextStartUTF8", typeof(int))]
        [TestCase("callstackTextStartUTF16", typeof(int))]
        public void LogEntry_FieldHasExpectedType(string name, Type expectedType)
        {
            Assert.That(RequireUnityType("LogEntry").GetField(name, AnyInstance)?.FieldType,
                Is.EqualTo(expectedType), $"LogEntry.{name} must exist with the expected type.");
        }

        [Test]
        public void LogEntry_ModeSupportsIntegerMasking()
        {
            Assert.That(RequireUnityType("ConsoleWindow+Mode").IsEnum, Is.True);
            var field = RequireUnityType("LogEntry").GetField("mode", AnyInstance);
            Assert.That(field, Is.Not.Null);
            var valueType = field.FieldType.IsEnum ? Enum.GetUnderlyingType(field.FieldType) : field.FieldType;
            Assert.That(valueType, Is.EqualTo(typeof(int)));
        }

        static Type RequireUnityType(string name)
        {
            var type = typeof(EditorWindow).Assembly.GetType("UnityEditor." + name);
            Assert.That(type, Is.Not.Null, $"UnityEditor.{name} is required by the Console reflection bridge.");
            return type;
        }
    }
}
