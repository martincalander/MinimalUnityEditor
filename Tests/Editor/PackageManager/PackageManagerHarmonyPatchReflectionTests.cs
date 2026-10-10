using System.Collections;
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
                "Check the Harmony id, target lookup, postfix lookup, and assembly load order. " +
                PackageManagerAssetStoreDuplicatePatch.LastPatchError);
        }

        [Test]
        public void HarmonyPatch_RepeatedInitializationAndCleanup_PreserveSingleOwnedPostfixes()
        {
            var patchType = typeof(PackageManagerAssetStoreDuplicatePatch);
            const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic;
            var initialize = patchType.GetMethod("Initialize", flags);
            var cleanup = patchType.GetMethod("OnBeforeAssemblyReload", flags);
            Assert.That(initialize, Is.Not.Null);
            Assert.That(cleanup, Is.Not.Null);

            var targets = new[]
            {
                PackageManagerAssetStoreDuplicatePatch.GetTargetMethod(),
                PackageManagerAssetStoreDuplicatePatch.GetPackageDetailsHeaderRefreshMethod()
            };
            try
            {
                initialize.Invoke(null, null);
                initialize.Invoke(null, null);
                foreach (var target in targets)
                    Assert.That(CountOwnedPostfixes(target), Is.EqualTo(1));

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

        static int CountOwnedPostfixes(MethodInfo target)
        {
            Assert.That(target, Is.Not.Null);
            var harmony = System.Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: true);
            var patchInfo = harmony.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) })
                .Invoke(null, new object[] { target });
            if (patchInfo == null)
                return 0;

            var count = 0;
            var postfixes = (IEnumerable)patchInfo.GetType().GetField("Postfixes").GetValue(patchInfo);
            foreach (var postfix in postfixes)
            {
                var owner = postfix.GetType().GetField("owner").GetValue(postfix) as string;
                if (owner == PackageManagerAssetStoreDuplicatePatch.HarmonyId)
                    count++;
            }
            return count;
        }

        [Test]
        public void HarmonyPatch_MissingTargetOrPostfix_IsNotReportedAsPatched()
        {
            Assert.That(
                PackageManagerAssetStoreDuplicatePatch.PatchPostfix(null, PackageManagerAssetStoreDuplicatePatch.GetPostfixMethod()),
                Is.False);
            Assert.That(
                PackageManagerAssetStoreDuplicatePatch.PatchPostfix(PackageManagerAssetStoreDuplicatePatch.GetTargetMethod(), null),
                Is.False);
        }

        [Test]
        public void HarmonyPatch_DetailHeaderRefresh_HasExpectedSignature()
        {
            var method = PackageManagerAssetStoreDuplicatePatch.GetPackageDetailsHeaderRefreshMethod();

            Assert.That(method, Is.Not.Null,
                $"{PackageManagerLocalPackageButton.DetailHeaderTypeName}.{PackageManagerLocalPackageButton.DetailHeaderRefreshMethodName}(IPackage) was not found. " +
                "The local package button is injected when the Package Manager details header refreshes for the selected package.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)),
                "PackageDetailsHeader.Refresh(IPackage) must keep returning void so the postfix can update the toolbar after Unity renders its buttons.");

            var parameters = method.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(1));
            Assert.That(parameters[0].ParameterType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicatePatch.PackageInterfaceTypeName),
                "PackageDetailsHeader.Refresh no longer receives IPackage; update the local package button patch.");
        }

        [Test]
        public void HarmonyPatch_DetailHeaderRefreshPostfix_UsesHarmonyArgumentNames()
        {
            var method = PackageManagerAssetStoreDuplicatePatch.GetPackageDetailsHeaderRefreshPostfixMethod();

            Assert.That(method, Is.Not.Null,
                "PackageManagerAssetStoreDuplicatePatch.PackageDetailsHeaderRefreshPostfix was not found. " +
                "The Harmony patch must bind this method as the PackageDetailsHeader.Refresh postfix.");
            Assert.That(method.IsStatic, Is.True, "Harmony postfix must remain static.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));

            var parameters = method.GetParameters();
            Assert.That(parameters, Has.Length.EqualTo(2),
                "PackageDetailsHeaderRefreshPostfix must keep object __instance and object __0.");
            Assert.That(parameters[0].Name, Is.EqualTo("__instance"),
                "Harmony binds the PackageDetailsHeader instance by the special __instance parameter name.");
            Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(object)));
            Assert.That(parameters[1].Name, Is.EqualTo("__0"),
                "Harmony binds the refreshed package argument by the special __0 parameter name.");
            Assert.That(parameters[1].ParameterType, Is.EqualTo(typeof(object)));
        }

        [Test]
        public void HarmonyPatch_DetailHeaderRefreshRuntimePatchRegistration_ContainsExpectedOwnerAndPostfix()
        {
            PackageManagerAssetStoreDuplicatePatch.TryPatch();

            Assert.That(PackageManagerAssetStoreDuplicatePatch.IsLocalPackageButtonPatchApplied(), Is.True,
                "Harmony does not report the local package button postfix on PackageDetailsHeader.Refresh. " +
                "Check the Harmony id, target lookup, postfix lookup, and assembly load order. " +
                PackageManagerAssetStoreDuplicatePatch.LastPatchError);
        }

        [Test]
        public void PageRebuildHook_SimplePage_SupportedRebuildMethodExists()
        {
            var method = PackageManagerAssetStoreDuplicatePatch.GetPageRebuildMethod();

            Assert.That(method, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.SimplePageTypeName} has neither Rebuild(bool) nor its legacy parameterless rebuild method. " +
                "The duplicate filter uses this after patching so already-open All Packages and Updates pages rebuild immediately.");
            Assert.That(method.ReturnType, Is.EqualTo(typeof(void)));

            var parameters = method.GetParameters();
            if (method.Name == PackageManagerAssetStoreDuplicatePatch.RebuildMethodName)
            {
                Assert.That(parameters, Has.Length.EqualTo(1));
                Assert.That(parameters[0].ParameterType, Is.EqualTo(typeof(bool)),
                    "SimplePage.Rebuild must keep its triggerEvent bool parameter.");
            }
            else
            {
                Assert.That(method.Name, Is.EqualTo(PackageManagerAssetStoreDuplicatePatch.LegacyRebuildMethodName));
                Assert.That(parameters, Is.Empty,
                    "The legacy list rebuild entry point must remain parameterless.");
            }
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
        public void LocalPackageButtonReflection_DetailHeaderPackageField_Exists()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerLocalPackageButton.DetailHeaderTypeName);
            Assert.That(type, Is.Not.Null,
                $"{PackageManagerLocalPackageButton.DetailHeaderTypeName} was not found. " +
                "The local package button refreshes already-open Package Manager windows by reading this header.");

            var field = type.GetField(PackageManagerLocalPackageButton.DetailHeaderPackageFieldName, AnyInstance);
            Assert.That(field, Is.Not.Null,
                "PackageDetailsHeader.m_Package was not found. " +
                "The local package button uses it when refreshing an already-open Package Manager window after domain reload.");
            Assert.That(field.FieldType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicatePatch.PackageInterfaceTypeName));
        }

        [Test]
        public void LocalPackageButtonReflection_PackageToolbarType_Exists()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerLocalPackageButton.ToolbarTypeName);
            Assert.That(type, Is.Not.Null,
                $"{PackageManagerLocalPackageButton.ToolbarTypeName} was not found. " +
                "The local package button inserts itself into this toolbar next to Unity's In Project button.");
            Assert.That(typeof(UnityEngine.UIElements.VisualElement).IsAssignableFrom(type), Is.True,
                "PackageToolbar must remain a VisualElement so the local package button can be inserted into it.");
        }

        [Test]
        public void LocalPackageButtonReflection_PackageManagerWindowType_Exists()
        {
            var type = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerLocalPackageButton.PackageManagerWindowTypeName);
            Assert.That(type, Is.Not.Null,
                $"{PackageManagerLocalPackageButton.PackageManagerWindowTypeName} was not found. " +
                "The local package button scans open Package Manager windows after assembly reload.");
            Assert.That(typeof(UnityEditor.EditorWindow).IsAssignableFrom(type), Is.True);
        }

        [Test]
        public void LocalPackageButtonReflection_InProjectPageSelectionMethods_Exist()
        {
            var pageType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicatePatch.SimplePageTypeName);
            Assert.That(pageType, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.SimplePageTypeName} was not found. " +
                "The local package button uses page selection APIs to jump from an Asset Store row to the local package row.");

            var loadMethod = pageType.GetMethod(
                PackageManagerAssetStoreDuplicatePatch.PageLoadMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string) },
                null);
            Assert.That(loadMethod, Is.Not.Null,
                "IPage.Load(string) was not found. " +
                "The local package button uses it to ensure the local package item is available before selecting it.");

            var setNewSelectionMethod = pageType.GetMethod(
                PackageManagerAssetStoreDuplicatePatch.PageSetNewSelectionMethodName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null,
                new[] { typeof(string), typeof(bool) },
                null);
            Assert.That(setNewSelectionMethod, Is.Not.Null,
                "IPage.SetNewSelection(string, bool) was not found. " +
                "The local package button uses it to select the local package row.");
            Assert.That(setNewSelectionMethod.ReturnType, Is.EqualTo(typeof(bool)));
        }

        [Test]
        public void LocalPackageButtonReflection_PageManagerActivePage_SetterExists()
        {
            var pageManagerType = typeof(UnityEditor.Editor).Assembly.GetType(PackageManagerAssetStoreDuplicatePatch.PageManagerInterfaceName);
            Assert.That(pageManagerType, Is.Not.Null,
                $"{PackageManagerAssetStoreDuplicatePatch.PageManagerInterfaceName} was not found. " +
                "The local package button uses it to switch to All Packages before selecting the local package.");

            var property = pageManagerType.GetProperty(
                PackageManagerAssetStoreDuplicatePatch.PageActivePagePropertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null,
                "IPageManager.activePage was not found. " +
                "The local package button uses it to switch to the InProject page.");
            Assert.That(property.CanWrite, Is.True,
                "IPageManager.activePage must remain settable so the local package button can switch pages.");
            Assert.That(property.PropertyType.FullName, Is.EqualTo(PackageManagerAssetStoreDuplicatePatch.PageInterfaceTypeName));
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
            Assert.That(typeof(IEnumerable).IsAssignableFrom(property.PropertyType), Is.True,
                "IPackageVersion.importedAssets must remain enumerable so the duplicate filter can inspect its assets.");
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
