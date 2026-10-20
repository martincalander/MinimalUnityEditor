using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Filter = MinimalEditor.PackageManager.Editor.PackageManagerAssetStoreDuplicateFilter;
using LocalButton = MinimalEditor.PackageManager.Editor.PackageManagerLocalPackageButton;
using Patch = MinimalEditor.PackageManager.Editor.PackageManagerAssetStoreDuplicatePatch;

namespace MinimalEditor.Tests.Editor.PackageManager
{
    public sealed class PackageManagerHarmonyPatchReflectionTests
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [TestCase(false)]
        [TestCase(true)]
        public void HarmonyPatch_TargetMethod_HasExpectedSignature(bool detailHeader)
        {
            var method = detailHeader ? Patch.GetPackageDetailsHeaderRefreshMethod() : Patch.GetTargetMethod();
            AssertMethodSignature(method, detailHeader ? typeof(void) : typeof(bool), GetUnityType(Patch.PackageInterfaceTypeName));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HarmonyPatch_PostfixMethod_UsesHarmonyArgumentNames(bool detailHeader)
        {
            var method = detailHeader ? Patch.GetPackageDetailsHeaderRefreshPostfixMethod() : Patch.GetPostfixMethod();
            AssertMethodSignature(method, typeof(void), typeof(object), detailHeader ? typeof(object) : typeof(bool).MakeByRefType());
            Assert.That(method.IsStatic, Is.True);
            Assert.That(method.GetParameters().Select(parameter => parameter.Name),
                Is.EqualTo(detailHeader ? new[] { "__instance", "__0" } : new[] { "__0", "__result" }));
        }

        [Test]
        public void HarmonyPatch_RepeatedInitializationAndCleanup_PreserveSingleOwnedPostfixes()
        {
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var initialize = typeof(Patch).GetMethod("Initialize", flags);
            var cleanup = typeof(Patch).GetMethod("OnBeforeAssemblyReload", flags);
            Assert.That(initialize, Is.Not.Null);
            Assert.That(cleanup, Is.Not.Null);

            var targets = new[] { Patch.GetTargetMethod(), Patch.GetPackageDetailsHeaderRefreshMethod() };
            try
            {
                initialize.Invoke(null, null);
                initialize.Invoke(null, null);
                foreach (var target in targets)
                    Assert.That(CountOwnedPostfixes(target), Is.EqualTo(1));
                Assert.That(Patch.IsPatchApplied(), Is.True, Patch.LastPatchError);
                Assert.That(Patch.IsLocalPackageButtonPatchApplied(), Is.True, Patch.LastPatchError);

                cleanup.Invoke(null, null);
                cleanup.Invoke(null, null);
                foreach (var target in targets)
                    Assert.That(CountOwnedPostfixes(target), Is.Zero);

                initialize.Invoke(null, null);
                foreach (var target in targets)
                    Assert.That(CountOwnedPostfixes(target), Is.EqualTo(1));
            }
            finally
            {
                initialize.Invoke(null, null);
            }
        }

        [Test]
        public void HarmonyPatch_MissingTargetOrPostfix_IsNotReportedAsPatched()
        {
            Assert.That(Patch.PatchPostfix(null, Patch.GetPostfixMethod()), Is.False);
            Assert.That(Patch.PatchPostfix(Patch.GetTargetMethod(), null), Is.False);
        }

        [Test]
        public void PageRebuildHook_SimplePage_SupportedRebuildMethodExists()
        {
            var method = Patch.GetPageRebuildMethod();
            Assert.That(method, Is.Not.Null, "SimplePage has no supported rebuild method.");
            if (method.Name == Patch.RebuildMethodName)
                AssertMethodSignature(method, typeof(void), typeof(bool));
            else
            {
                Assert.That(method.Name, Is.EqualTo(Patch.LegacyRebuildMethodName));
                AssertMethodSignature(method, typeof(void));
            }
        }

        [Test]
        public void PageManagerReflection_NavigationAndSelectionApis_AreAvailable()
        {
            var pageManagerType = GetUnityType(Patch.PageManagerInterfaceName);
            var pageType = GetUnityType(Patch.SimplePageTypeName);
            var getPage = pageManagerType.GetMethod(Patch.GetPageMethodName, AnyInstance, null, new[] { typeof(string) }, null);
            AssertMethodSignature(getPage, GetUnityType(Patch.PageInterfaceTypeName), typeof(string));

            var activePage = AssertProperty(Patch.PageManagerInterfaceName, Patch.PageActivePagePropertyName, Patch.PageInterfaceTypeName);
            Assert.That(activePage.CanWrite, Is.True, "The local package button must be able to switch pages.");
            Assert.That(pageType.GetMethod(Patch.PageLoadMethodName, AnyInstance, null, new[] { typeof(string) }, null),
                Is.Not.Null, "SimplePage.Load(string) is required before selecting a local package.");
            AssertMethodSignature(pageType.GetMethod(Patch.PageSetNewSelectionMethodName, AnyInstance, null,
                new[] { typeof(string), typeof(bool) }, null), typeof(bool), typeof(string), typeof(bool));
        }

        [Test]
        public void PageRebuildHook_ServicesContainer_CanResolvePageManager()
        {
            GetUnityType(Patch.PageManagerInterfaceName);
            var resolveMethod = Patch.GetServiceResolveMethod(GetUnityType(Patch.ServicesContainerTypeName));
            Assert.That(resolveMethod, Is.Not.Null, "ServicesContainer.Resolve<T>() is required to reach IPageManager.");
            Assert.That(resolveMethod.IsGenericMethodDefinition, Is.True);
            Assert.That(resolveMethod.GetParameters(), Is.Empty);
        }

        [TestCase(LocalButton.ToolbarTypeName, typeof(UnityEngine.UIElements.VisualElement))]
        [TestCase(LocalButton.PackageManagerWindowTypeName, typeof(UnityEditor.EditorWindow))]
        public void LocalPackageButtonReflection_UiType_HasExpectedBaseType(string typeName, Type baseType)
        {
            Assert.That(baseType.IsAssignableFrom(GetUnityType(typeName)), Is.True);
        }

        [TestCase(LocalButton.DetailHeaderTypeName, LocalButton.DetailHeaderPackageFieldName, Patch.PackageInterfaceTypeName)]
        [TestCase(Filter.AssetTypeName, Filter.AssetImportedPathFieldName, "System.String")]
        public void ReflectedField_HasExpectedType(string typeName, string fieldName, string expectedType)
        {
            var field = GetUnityType(typeName).GetField(fieldName, AnyInstance);
            Assert.That(field, Is.Not.Null, $"{typeName}.{fieldName} was not found.");
            Assert.That(field.FieldType.FullName, Is.EqualTo(expectedType));
        }

        [TestCase(Filter.PackageConcreteTypeName, Filter.PackageNamePropertyName, "System.String")]
        [TestCase(Patch.PackageInterfaceTypeName, Filter.PackageProductPropertyName, Filter.ProductInterfaceTypeName)]
        [TestCase(Patch.PackageInterfaceTypeName, Filter.PackageVersionsPropertyName, Filter.VersionListInterfaceTypeName)]
        public void FilterReflection_PackageProperty_HasExpectedType(string typeName, string propertyName, string expectedType)
        {
            AssertProperty(typeName, propertyName, expectedType);
        }

        [TestCase(Filter.VersionListImportedPropertyName)]
        [TestCase(Filter.VersionListPrimaryPropertyName)]
        [TestCase(Filter.VersionListImportAvailablePropertyName)]
        [TestCase(Filter.VersionListSuggestedUpdatePropertyName)]
        [TestCase(Filter.VersionListLatestPropertyName)]
        public void FilterReflection_VersionSlot_ReturnsPackageVersion(string propertyName)
        {
            AssertProperty(Filter.VersionListInterfaceTypeName, propertyName, Filter.PackageVersionInterfaceTypeName);
        }

        [Test]
        public void FilterReflection_ImportedAssets_AreEnumerable()
        {
            var property = GetUnityType(Filter.PackageVersionInterfaceTypeName).GetProperty(Filter.VersionImportedAssetsPropertyName, AnyInstance);
            Assert.That(property, Is.Not.Null, "IPackageVersion.importedAssets was not found.");
            Assert.That(typeof(IEnumerable).IsAssignableFrom(property.PropertyType), Is.True);
        }

        static Type GetUnityType(string typeName)
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType(typeName);
            Assert.That(type, Is.Not.Null, $"{typeName} was not found in this Unity version.");
            return type;
        }

        static PropertyInfo AssertProperty(string typeName, string propertyName, string expectedType)
        {
            var property = GetUnityType(typeName).GetProperty(propertyName, AnyInstance);
            Assert.That(property, Is.Not.Null, $"{typeName}.{propertyName} was not found.");
            Assert.That(property.PropertyType.FullName, Is.EqualTo(expectedType), $"{typeName}.{propertyName}");
            return property;
        }

        static void AssertMethodSignature(MethodInfo method, Type returnType, params Type[] parameterTypes)
        {
            Assert.That(method, Is.Not.Null, "The required Package Manager method was not found.");
            Assert.That(method.ReturnType, Is.EqualTo(returnType), method.Name);
            Assert.That(method.GetParameters().Select(parameter => parameter.ParameterType), Is.EqualTo(parameterTypes), method.Name);
        }

        static int CountOwnedPostfixes(MethodInfo target)
        {
            Assert.That(target, Is.Not.Null);
            var harmony = Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: true);
            var patchInfo = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) })
                .Invoke(null, new object[] { target });
            if (patchInfo == null)
                return 0;

            var postfixes = (IEnumerable)patchInfo.GetType().GetField("Postfixes").GetValue(patchInfo);
            return postfixes.Cast<object>().Count(postfix =>
                (string)postfix.GetType().GetField("owner").GetValue(postfix) == Patch.HarmonyId);
        }
    }
}
