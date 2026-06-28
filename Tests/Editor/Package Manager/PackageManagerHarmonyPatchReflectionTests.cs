using System.Reflection;
using Essentials.PackageManager.Editor;
using NUnit.Framework;

namespace Essentials.Tests.Editor.PackageManager
{
    public sealed class PackageManagerHarmonyPatchReflectionTests
    {
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Test]
        public void HarmonyPatch_TargetType_InProjectPage_ExistsInUnityEditorAssembly()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicatePatch.TargetTypeName);

            Assert.That(type, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.TargetTypeName} was not found. " +
                "Unity moved or renamed the in-project Package Manager page, so the duplicate filter cannot patch All Packages or Updates.");
        }

        [Test]
        public void HarmonyPatch_TargetMethod_ShouldInclude_HasExpectedSignature()
        {
            var method = PackageManagerAssetStoreDuplicatePatch.GetTargetMethod();

            Assert.That(method, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.TargetTypeName}.{PackageManagerAssetStoreDuplicatePatch.TargetMethodName}(IPackage) was not found. " +
                "The duplicate filter patches this method because All Packages and Updates both use it to decide whether rows are visible.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(bool)),
                "InProjectPage.ShouldInclude must return bool so the postfix can turn visible Asset Store duplicate rows off.");

            var parameters = method.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1),
                "InProjectPage.ShouldInclude must keep exactly one package parameter for the Harmony __0 postfix binding.");
            Assert.That(parameters[0].ParameterType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicatePatch.PackageInterfaceTypeName),
                "InProjectPage.ShouldInclude no longer receives UnityEditor.PackageManager.UI.Internal.IPackage; update the patch and filter reflection.");
        }

        [Test]
        public void HarmonyPatch_PostfixMethod_UsesHarmonyArgumentNamesAndResultRef()
        {
            var method = PackageManagerAssetStoreDuplicatePatch.GetPostfixMethod();

            Assert.That(method, Is.Not.Null,
                "PackageManagerAssetStoreDuplicatePatch.ShouldIncludePostfix was not found. " +
                "The Harmony patch must bind this method as the InProjectPage.ShouldInclude postfix.");
            Assert.That(method.IsStatic, Is.True, "Harmony postfix must remain static.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)), "Harmony postfix should mutate __result and return void.");

            var parameters = method.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(2),
                "ShouldIncludePostfix must keep object __0 and ref bool __result.");
            Assert.That(parameters[0].Name, Is.EqualTo("__0"),
                "Harmony binds the original package argument by the special __0 parameter name.");
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(object)),
                "The original package argument stays typed as object because Unity's IPackage interface is internal.");
            Assert.That(parameters[1].Name, Is.EqualTo("__result"),
                "Harmony binds the original bool result by the special __result parameter name.");
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(bool).MakeByRefType()),
                "The postfix must receive ref bool __result so it can hide only matching rows.");
        }

        [Test]
        public void HarmonyPatch_RuntimePatchRegistration_ContainsExpectedOwnerAndPostfix()
        {
            PackageManagerAssetStoreDuplicatePatch.TryPatch();

            Assert.That(PackageManagerAssetStoreDuplicatePatch.IsPatchApplied(), Is.True,
                "Harmony does not report the Package Manager duplicate filter postfix on InProjectPage.ShouldInclude. " +
                "Check the Harmony id, target lookup, postfix lookup, and assembly load order.");
        }

        [Test]
        public void PageRebuildHook_SimplePage_RebuildBoolMethodExists()
        {
            var method = PackageManagerAssetStoreDuplicatePatch.GetPageRebuildMethod();

            Assert.That(method, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.SimplePageTypeName}.{PackageManagerAssetStoreDuplicatePatch.RebuildMethodName}(bool) was not found. " +
                "The duplicate filter uses this after patching so already-open All Packages and Updates pages rebuild immediately.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));

            var parameters = method.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(bool)),
                "SimplePage.Rebuild must keep its triggerEvent bool parameter.");
        }

        [Test]
        public void PageRebuildHook_PageManager_GetPageStringMethodExists()
        {
            var pageManagerType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicatePatch.PageManagerInterfaceName);
            Assert.That(pageManagerType, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.PageManagerInterfaceName} was not found. " +
                "The duplicate filter cannot ask Package Manager for the All Packages and Updates pages to rebuild.");

            var method = pageManagerType.GetMethod(
                PackageManagerAssetStoreDuplicatePatch.GetPageMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);

            Assert.That(method, Is.Not.Null,
                "IPageManager.GetPage(string) was not found. " +
                "The duplicate filter uses it to rebuild the InProject and Updates pages after installing the Harmony patch.");
            Assert.That(method.ReturnType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicatePatch.PageInterfaceTypeName));
        }

        [Test]
        public void PageRebuildHook_ServicesContainer_CanResolvePageManager()
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var servicesContainerType = editorAssembly.GetType(PackageManagerAssetStoreDuplicatePatch.ServicesContainerTypeName);
            var pageManagerType = editorAssembly.GetType(PackageManagerAssetStoreDuplicatePatch.PageManagerInterfaceName);

            Assert.That(servicesContainerType, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.ServicesContainerTypeName} was not found. " +
                "The duplicate filter cannot reach Package Manager services to trigger a page rebuild.");
            Assert.That(pageManagerType, Is.Not.Null);

            var resolveMethod = PackageManagerAssetStoreDuplicatePatch.GetServiceResolveMethod(servicesContainerType);

            Assert.That(resolveMethod, Is.Not.Null,
                "ServicesContainer.Resolve<T>() was not found. " +
                "The duplicate filter uses it to resolve IPageManager after patching.");
            Assert.That(resolveMethod.IsGenericMethodDefinition, Is.True,
                "ServicesContainer.Resolve must remain generic so the filter can resolve IPageManager by reflected type.");
            Assert.That(resolveMethod.GetParameters(), Is.Empty,
                "ServicesContainer.Resolve<T>() must stay parameterless so the filter can invoke it by reflection.");
        }

        [Test]
        public void FilterReflection_ConcretePackage_NameProperty_ExistsAndReturnsString()
        {
            var packageType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicateFilter.PackageConcreteTypeName);
            Assert.That(packageType, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.Package was not found. " +
                "Harmony receives the concrete package instance as __0, and the duplicate filter reflects this type for the technical package name.");

            var property = packageType.GetProperty(
                PackageManagerAssetStoreDuplicateFilter.PackageNamePropertyName,
                AnyInstance);

            Assert.That(property, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.Package no longer exposes the name property used to match Asset Store rows to registered UPM packages.");
            Assert.That(property.PropertyType, Is.EqualTo(typeof(string)),
                "Package.name must remain a string technical package name such as com.vendor.tool.");
        }

        [Test]
        public void FilterReflection_IPackage_ProductProperty_ExistsAndIdentifiesAssetStoreRows()
        {
            var packageType = GetInternalPackageInterface();
            Assume.That(packageType, Is.Not.Null);

            var property = packageType.GetProperty(
                PackageManagerAssetStoreDuplicateFilter.PackageProductPropertyName,
                AnyInstance);

            Assert.That(property, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.IPackage no longer exposes the product property used to detect Asset Store-backed rows.");
            Assert.That(property.PropertyType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicateFilter.ProductInterfaceTypeName),
                "IPackage.product must keep returning UnityEditor.PackageManager.UI.Internal.IProduct so null means non-Asset-Store and non-null means Asset Store-backed.");
        }

        [Test]
        public void FilterReflection_IPackage_VersionsProperty_ExistsAndReturnsInternalVersionList()
        {
            var packageType = GetInternalPackageInterface();
            Assume.That(packageType, Is.Not.Null);

            var property = packageType.GetProperty(
                PackageManagerAssetStoreDuplicateFilter.PackageVersionsPropertyName,
                AnyInstance);

            Assert.That(property, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.IPackage no longer exposes the versions property used to inspect imported Asset Store assets.");
            Assert.That(property.PropertyType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicateFilter.VersionListInterfaceTypeName),
                "IPackage.versions must keep returning UnityEditor.PackageManager.UI.Internal.IVersionList.");
        }

        [Test]
        public void FilterReflection_IVersionList_ImportedAndPrimaryProperties_Exist()
        {
            var versionListType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicateFilter.VersionListInterfaceTypeName);
            Assert.That(versionListType, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.IVersionList was not found. " +
                "The duplicate filter uses it to inspect imported legacy Asset Store versions.");

            AssertVersionProperty(versionListType, PackageManagerAssetStoreDuplicateFilter.VersionListImportedPropertyName);
            AssertVersionProperty(versionListType, PackageManagerAssetStoreDuplicateFilter.VersionListPrimaryPropertyName);
            AssertVersionProperty(versionListType, PackageManagerAssetStoreDuplicateFilter.VersionListImportAvailablePropertyName);
            AssertVersionProperty(versionListType, PackageManagerAssetStoreDuplicateFilter.VersionListSuggestedUpdatePropertyName);
            AssertVersionProperty(versionListType, PackageManagerAssetStoreDuplicateFilter.VersionListLatestPropertyName);
        }

        [Test]
        public void FilterReflection_IPackageVersion_ImportedAssetsProperty_Exists()
        {
            var versionType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicateFilter.PackageVersionInterfaceTypeName);
            Assert.That(versionType, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.IPackageVersion was not found. " +
                "The duplicate filter uses it to read importedAssets from legacy Asset Store versions.");

            var property = versionType.GetProperty(
                PackageManagerAssetStoreDuplicateFilter.VersionImportedAssetsPropertyName,
                AnyInstance);

            Assert.That(property, Is.Not.Null,
                "IPackageVersion.importedAssets was not found. " +
                "Without it, the filter cannot map legacy Asset Store rows back to package asset paths.");
            Assert.That(property.PropertyType.FullName, Does.Contain("IReadOnlyCollection"),
                "IPackageVersion.importedAssets should remain a collection of imported Asset Store assets.");
        }

        [Test]
        public void FilterReflection_Asset_ImportedPathField_ExistsAndReturnsString()
        {
            var assetType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicateFilter.AssetTypeName);
            Assert.That(assetType, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.Asset was not found. " +
                "The duplicate filter uses it to read legacy Asset Store imported asset paths.");

            var field = assetType.GetField(
                PackageManagerAssetStoreDuplicateFilter.AssetImportedPathFieldName,
                AnyInstance);

            Assert.That(field, Is.Not.Null,
                "Asset.importedPath was not found. " +
                "Without it, the filter cannot detect Asset Store rows whose files live under Packages/<package-name>/.");
            Assert.That(field.FieldType, Is.EqualTo(typeof(string)),
                "Asset.importedPath must stay a string project-relative path.");
        }

        [Test]
        public void PackageInfo_FindForPackageName_ReturnsRegisteredPackageWithResolvedPath()
        {
            var method = typeof(UnityEditor.PackageManager.PackageInfo).GetMethod(
                nameof(UnityEditor.PackageManager.PackageInfo.FindForPackageName),
                BindingFlags.Static | BindingFlags.Public);

            Assert.That(method, Is.Not.Null,
                "PackageInfo.FindForPackageName(string) was not found. " +
                "The duplicate filter uses it to locate the real installed package behind an Asset Store row.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(UnityEditor.PackageManager.PackageInfo)),
                "PackageInfo.FindForPackageName must keep returning PackageInfo.");

            var parameters = method.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(string)));

            var resolvedPathProperty = typeof(UnityEditor.PackageManager.PackageInfo).GetProperty(
                nameof(UnityEditor.PackageManager.PackageInfo.resolvedPath),
                BindingFlags.Instance | BindingFlags.Public);

            Assert.That(resolvedPathProperty, Is.Not.Null,
                "PackageInfo.resolvedPath was not found. " +
                "The duplicate filter uses it to distinguish project-local Packages/ installs from read-only PackageCache installs.");
            Assert.That(resolvedPathProperty.PropertyType, Is.EqualTo(typeof(string)));
        }

        static System.Type GetInternalPackageInterface()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicatePatch.PackageInterfaceTypeName);
            Assert.That(type, Is.Not.Null,
                "UnityEditor.PackageManager.UI.Internal.IPackage was not found. " +
                "The Package Manager internal package model changed; update filter reflection and Harmony target tests.");
            return type;
        }

        static void AssertVersionProperty(System.Type versionListType, string propertyName)
        {
            var property = versionListType.GetProperty(propertyName, AnyInstance);
            Assert.That(property, Is.Not.Null,
                $"IVersionList.{propertyName} was not found. " +
                "The duplicate filter reads this version slot while searching for legacy Asset Store imported assets.");
            Assert.That(property.PropertyType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicateFilter.PackageVersionInterfaceTypeName),
                $"IVersionList.{propertyName} must keep returning IPackageVersion.");
        }
    }
}
