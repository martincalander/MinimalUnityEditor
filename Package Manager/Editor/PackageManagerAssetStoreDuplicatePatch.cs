using System;
using System.Reflection;
using HarmonyLib;
using UnityEditor;

namespace Essentials.PackageManager.Editor
{
    [InitializeOnLoad]
    internal static class PackageManagerAssetStoreDuplicatePatch
    {
        internal const string HarmonyId = "com.essentials.extensions.package-manager.editor";
        internal const string TargetTypeName = "UnityEditor.PackageManager.UI.Internal.InProjectPage";
        internal const string TargetMethodName = "ShouldInclude";
        internal const string PackageInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IPackage";
        internal const string PageManagerInterfaceName = "UnityEditor.PackageManager.UI.Internal.IPageManager";
        internal const string PageInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IPage";
        internal const string ServicesContainerTypeName = "UnityEditor.PackageManager.UI.Internal.ServicesContainer";
        internal const string SimplePageTypeName = "UnityEditor.PackageManager.UI.Internal.SimplePage";
        internal const string RebuildMethodName = "Rebuild";
        internal const string GetPageMethodName = "GetPage";
        internal const string ResolveMethodName = "Resolve";
        internal const string InProjectPageId = "InProject";
        internal const string UpdatesPageId = "Updates";

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static Harmony s_Harmony;
        static bool s_Patched;

        static PackageManagerAssetStoreDuplicatePatch()
        {
            TryPatch();
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
        }

        internal static void TryPatch()
        {
            if (s_Patched)
                return;

            try
            {
                var shouldIncludeMethod = GetTargetMethod();
                var postfixMethod = GetPostfixMethod();
                if (shouldIncludeMethod == null || postfixMethod == null)
                    return;

                s_Harmony = new Harmony(HarmonyId);
                s_Harmony.Patch(shouldIncludeMethod, postfix: new HarmonyMethod(postfixMethod));
                s_Patched = true;
                EditorApplication.delayCall += ForceRebuildFilteredPages;
            }
            catch
            {
                s_Patched = false;
            }
        }

        internal static MethodInfo GetTargetMethod()
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var inProjectPageType = editorAssembly.GetType(TargetTypeName);
            var packageType = editorAssembly.GetType(PackageInterfaceTypeName);
            if (inProjectPageType == null || packageType == null)
                return null;

            return inProjectPageType.GetMethod(TargetMethodName, AnyInstance, null, new[] { packageType }, null);
        }

        internal static MethodInfo GetPostfixMethod()
        {
            return typeof(PackageManagerAssetStoreDuplicatePatch).GetMethod(nameof(ShouldIncludePostfix), AnyStatic);
        }

        internal static bool IsPatchApplied()
        {
            try
            {
                var targetMethod = GetTargetMethod();
                var postfixMethod = GetPostfixMethod();
                if (targetMethod == null || postfixMethod == null)
                    return false;

                var patchInfo = Harmony.GetPatchInfo(targetMethod);
                if (patchInfo == null)
                    return false;

                foreach (var postfix in patchInfo.Postfixes)
                {
                    if (postfix.owner == HarmonyId && postfix.PatchMethod == postfixMethod)
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        internal static void ForceRebuildFilteredPages()
        {
            try
            {
                var pageManager = ResolvePackageManagerService(PageManagerInterfaceName);
                if (pageManager == null)
                    return;

                RebuildPage(GetPage(pageManager, InProjectPageId));
                RebuildPage(GetPage(pageManager, UpdatesPageId));
            }
            catch
            {
                // The filter still applies on the next natural Package Manager rebuild.
            }
        }

        internal static MethodInfo GetPageRebuildMethod()
        {
            var simplePageType = typeof(UnityEditor.Editor).Assembly.GetType(SimplePageTypeName);
            return simplePageType?.GetMethod(RebuildMethodName, AnyInstance, null, new[] { typeof(bool) }, null);
        }

        static object ResolvePackageManagerService(string serviceInterfaceName)
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var servicesContainerType = editorAssembly.GetType(ServicesContainerTypeName);
            var serviceType = editorAssembly.GetType(serviceInterfaceName);
            if (servicesContainerType == null || serviceType == null)
                return null;

            var singletonType = typeof(ScriptableSingleton<>).MakeGenericType(servicesContainerType);
            var singletonInstance = singletonType
                .GetProperty("instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(null, null);
            if (singletonInstance == null)
                return null;

            var resolveMethod = GetServiceResolveMethod(servicesContainerType);
            return resolveMethod
                ?.MakeGenericMethod(serviceType)
                .Invoke(singletonInstance, null);
        }

        internal static MethodInfo GetServiceResolveMethod(Type servicesContainerType)
        {
            if (servicesContainerType == null)
                return null;

            foreach (var method in servicesContainerType.GetMethods(AnyInstance))
            {
                if (method.Name == ResolveMethodName &&
                    method.IsGenericMethodDefinition &&
                    method.GetParameters().Length == 0)
                {
                    return method;
                }
            }

            return null;
        }

        static object GetPage(object pageManager, string pageId)
        {
            return pageManager
                ?.GetType()
                .GetMethod(GetPageMethodName, AnyInstance, null, new[] { typeof(string) }, null)
                ?.Invoke(pageManager, new object[] { pageId });
        }

        static void RebuildPage(object page)
        {
            var rebuildMethod = GetPageRebuildMethod();
            rebuildMethod?.Invoke(page, new object[] { true });
        }

        static void ShouldIncludePostfix(object __0, ref bool __result)
        {
            if (!__result)
                return;

            try
            {
                if (PackageManagerAssetStoreDuplicateFilter.ShouldHideAssetStoreDuplicate(__0))
                    __result = false;
            }
            catch
            {
                __result = true;
            }
        }

        static void OnBeforeAssemblyReload()
        {
            try
            {
                s_Harmony?.UnpatchAll(HarmonyId);
            }
            catch
            {
                // Unity is tearing down the domain; fail open.
            }
        }
    }
}
