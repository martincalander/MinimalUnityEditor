using System;
using System.Collections;
using System.Reflection;
using UnityEditor;

namespace MinimalEditor.PackageManager.Editor
{
#if UNITY_6000_5_OR_NEWER
    // Editor patches stay active in Play Mode and are explicitly removed before code reload.
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class PackageManagerAssetStoreDuplicatePatch
    {
        internal const string HarmonyId = "com.martincalander.minimaleditor.package-manager.editor";
        internal const string TargetTypeName = "UnityEditor.PackageManager.UI.Internal.InProjectPage";
        internal const string TargetMethodName = "ShouldInclude";
        internal const string PackageInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IPackage";
        internal const string PageManagerInterfaceName = "UnityEditor.PackageManager.UI.Internal.IPageManager";
        internal const string PageInterfaceTypeName = "UnityEditor.PackageManager.UI.Internal.IPage";
        internal const string ServicesContainerTypeName = "UnityEditor.PackageManager.UI.Internal.ServicesContainer";
        internal const string SimplePageTypeName = "UnityEditor.PackageManager.UI.Internal.SimplePage";
        internal const string RebuildMethodName = "Rebuild";
        internal const string LegacyRebuildMethodName = "RebuildVisualStatesAndUpdateVisibilityWithSearchText";
        internal const string GetPageMethodName = "GetPage";
        internal const string ResolveMethodName = "Resolve";
        internal const string InProjectPageId = "InProject";
        internal const string UpdatesPageId = "Updates";
        internal const string PageActivePagePropertyName = "activePage";
        internal const string PageLoadMethodName = "Load";
        internal const string PageSetNewSelectionMethodName = "SetNewSelection";

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static object s_Harmony;
        static bool s_Patched;
        internal static string LastPatchError { get; private set; }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            TryPatch();
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
#endif
        }

        internal static void TryPatch()
        {
            if (s_Patched)
                return;

            try
            {
                LastPatchError = null;
                s_Harmony = Activator.CreateInstance(GetHarmonyType(), new object[] { HarmonyId });
                bool patchedAnyTarget =
                    PatchPostfix(GetTargetMethod(), GetPostfixMethod()) |
                    PatchPostfix(GetPackageToolbarRefreshMethod(), GetPackageToolbarRefreshPostfixMethod());
                if (!patchedAnyTarget)
                {
                    s_Harmony = null;
                    return;
                }

                s_Patched = true;
                EditorApplication.delayCall += RefreshPackageManagerWindow;
            }
            catch (Exception exception)
            {
                LastPatchError = exception.ToString();
                try
                {
                    RemovePatches();
                }
                catch
                {
                    // Preserve the original Package Manager failure while cleaning up best-effort.
                }

                s_Harmony = null;
                s_Patched = false;
            }
        }

        internal static bool PatchPostfix(MethodInfo targetMethod, MethodInfo postfixMethod)
        {
            if (targetMethod == null || postfixMethod == null)
                return false;

            var harmonyType = s_Harmony.GetType();
            var harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", throwOnError: true);
            var constructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) })
                ?? throw new MissingMethodException(harmonyMethodType.FullName, ".ctor(MethodInfo)");
            var postfix = constructor.Invoke(new object[] { postfixMethod });
            var patchMethod = harmonyType.GetMethod("Patch", new[]
            {
                typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType
            }) ?? throw new MissingMethodException(harmonyType.FullName, "Patch");
            patchMethod.Invoke(s_Harmony, new[] { (object)targetMethod, null, postfix, null, null });
            return true;
        }

        static Type GetHarmonyType()
        {
            // Unity 7 runs .NET 10 but compiles Editor scripts against .NET Standard.
            // Reflection keeps the selected Harmony runtime build out of the script API surface.
            return Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: true);
        }

        static void RemovePatches()
        {
            if (s_Harmony == null)
                return;

            var harmonyType = s_Harmony.GetType();
            var method = harmonyType.GetMethod("UnpatchAll", new[] { typeof(string) })
                ?? throw new MissingMethodException(harmonyType.FullName, "UnpatchAll");
            method.Invoke(s_Harmony, new object[] { HarmonyId });
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

        internal static MethodInfo GetPackageToolbarRefreshMethod()
        {
            var editorAssembly = typeof(UnityEditor.Editor).Assembly;
            var toolbarType = editorAssembly.GetType(PackageManagerLocalPackageButton.ToolbarTypeName);
            var packageType = editorAssembly.GetType(PackageInterfaceTypeName);
            if (toolbarType == null || packageType == null)
                return null;

            return toolbarType.GetMethod(
                PackageManagerLocalPackageButton.ToolbarRefreshMethodName,
                AnyInstance,
                null,
                new[] { packageType },
                null);
        }

        internal static MethodInfo GetPackageToolbarRefreshPostfixMethod()
        {
            return typeof(PackageManagerAssetStoreDuplicatePatch).GetMethod(nameof(PackageToolbarRefreshPostfix), AnyStatic);
        }

        internal static bool IsPatchApplied()
        {
            return HasPostfix(GetTargetMethod(), GetPostfixMethod());
        }

        internal static bool IsLocalPackageButtonPatchApplied()
        {
            return HasPostfix(GetPackageToolbarRefreshMethod(), GetPackageToolbarRefreshPostfixMethod());
        }

        static bool HasPostfix(MethodInfo targetMethod, MethodInfo postfixMethod)
        {
            try
            {
                if (targetMethod == null || postfixMethod == null)
                    return false;

                var harmonyType = GetHarmonyType();
                var getPatchInfo = harmonyType.GetMethod("GetPatchInfo", new[] { typeof(MethodBase) })
                    ?? throw new MissingMethodException(harmonyType.FullName, "GetPatchInfo");
                var patchInfo = getPatchInfo.Invoke(null, new object[] { targetMethod });
                if (patchInfo == null)
                    return false;

                var postfixes = patchInfo.GetType().GetField("Postfixes")?.GetValue(patchInfo) as IEnumerable;
                if (postfixes == null)
                    return false;

                foreach (var postfix in postfixes)
                {
                    var patchType = postfix.GetType();
                    var owner = patchType.GetField("owner")?.GetValue(postfix) as string;
                    var method = patchType.GetProperty("PatchMethod")?.GetValue(postfix) as MethodInfo;
                    if (owner == HarmonyId && method == postfixMethod)
                        return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        static void RefreshPackageManagerWindow()
        {
            ForceRebuildFilteredPages();
            PackageManagerLocalPackageButton.RefreshOpenPackageManagerWindows();
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
            return simplePageType?.GetMethod(RebuildMethodName, AnyInstance, null, new[] { typeof(bool) }, null)
                ?? simplePageType?.GetMethod(LegacyRebuildMethodName, AnyInstance, null, Type.EmptyTypes, null);
        }

        internal static object ResolvePackageManagerService(string serviceInterfaceName)
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

        internal static object GetPage(object pageManager, string pageId)
        {
            return pageManager
                ?.GetType()
                .GetMethod(GetPageMethodName, AnyInstance, null, new[] { typeof(string) }, null)
                ?.Invoke(pageManager, new object[] { pageId });
        }

        static void RebuildPage(object page)
        {
            if (page == null)
                return;

            var rebuildMethod = GetPageRebuildMethod();
            if (rebuildMethod == null)
                return;

            // The older entry point always rebuilds the list and reapplies its search filter.
            var arguments = rebuildMethod.GetParameters().Length == 0 ? null : new object[] { true };
            rebuildMethod.Invoke(page, arguments);
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

        static void PackageToolbarRefreshPostfix(object __instance, object __0)
        {
            try
            {
                PackageManagerLocalPackageButton.Refresh(__instance, __0);
            }
            catch
            {
                // The Package Manager UI should keep working if Unity internals change.
            }
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void OnBeforeAssemblyReload()
        {
            EditorApplication.delayCall -= RefreshPackageManagerWindow;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
#endif
            try
            {
                RemovePatches();
            }
            catch
            {
                // Leave the Package Manager usable if patch cleanup fails during code reload.
            }
            finally
            {
                s_Harmony = null;
                s_Patched = false;
                LastPatchError = null;
            }
        }
    }
}
