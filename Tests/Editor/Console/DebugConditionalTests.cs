using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using NUnit.Framework;
using EssentialsDebug = MartinCalander.Essentials.Console.Debug;
using Object = UnityEngine.Object;

namespace MartinCalander.Essentials.Tests.Editor
{
    public sealed class DebugConditionalTests
    {
        [Test]
        public void ExpensiveDebugHelpers_AreConditionalForEditorOrDevelopmentBuilds()
        {
            AssertConditionals(
                typeof(EssentialsDebug).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(method =>
                        method.Name == nameof(EssentialsDebug.Log) &&
                        method.IsGenericMethodDefinition &&
                        method.GetParameters().Length == 1 &&
                        method.GetParameters()[0].ParameterType.IsGenericType &&
                        method.GetParameters()[0].ParameterType.GetGenericTypeDefinition() == typeof(Expression<>)));

            AssertConditionals(
                typeof(EssentialsDebug).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Single(method => method.Name == nameof(EssentialsDebug.LogChanges) && method.IsGenericMethodDefinition));

            AssertConditionals(typeof(EssentialsDebug).GetMethod(nameof(EssentialsDebug.LogState), new[] { typeof(object) }));
            AssertConditionals(typeof(EssentialsDebug).GetMethod(nameof(EssentialsDebug.LogState), new[] { typeof(object), typeof(Object) }));
        }

        [Test]
        public void PlainDebugWrappers_RemainAlwaysAvailable()
        {
            Assert.That(
                typeof(EssentialsDebug).GetMethod(nameof(EssentialsDebug.Log), new[] { typeof(object) })?.GetCustomAttributes<ConditionalAttribute>().Any(),
                Is.False);
            Assert.That(
                typeof(EssentialsDebug).GetMethod(nameof(EssentialsDebug.LogError), new[] { typeof(object) })?.GetCustomAttributes<ConditionalAttribute>().Any(),
                Is.False);
        }

        static void AssertConditionals(MethodInfo method)
        {
            Assert.That(method, Is.Not.Null);

            var conditionals = method.GetCustomAttributes<ConditionalAttribute>()
                .Select(attribute => attribute.ConditionString)
                .ToArray();

            Assert.That(conditionals, Is.EquivalentTo(new[] { "UNITY_EDITOR", "DEVELOPMENT_BUILD" }));
        }
    }
}
