using System;
using System.Reflection;
using UnityEditor;

namespace MinimalEditor.Console.Editor
{
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class ConsoleAssistantToolbarButtonController
    {
        private const string AssistantBridgeAssemblyName = "Unity.AI.Assistant.Bridge.Editor";
        private const string AssistantBridgeConsoleUtilsTypeName = "Unity.AI.Assistant.Bridge.Editor.ConsoleUtils";
        private const string AssistantUiAssemblyName = "Unity.AI.Assistant.UI.Editor";
        private const string AssistantUiConsoleTypeName = "Unity.AI.Assistant.UI.Editor.Scripts.ConsoleUI";
        private const string ToolbarEventName = "s_DrawCustomToolbarGuiEvent";
        private const string ToolbarHandlerMethodName = "OnDrawCustomGuiEvent";
        private static bool s_toolbarHandlerResolved;
        private static EventInfo s_toolbarEvent;
        private static Delegate s_toolbarHandler;

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        internal static void Initialize()
        {
            s_toolbarHandlerResolved = false;
            ConsoleLogFilterSettings.SettingsChanged -= ApplySettings;
            ConsoleLogFilterSettings.SettingsChanged += ApplySettings;
            EditorApplication.delayCall -= ApplySettings;
            EditorApplication.delayCall += ApplySettings;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        internal static void Shutdown()
        {
            s_toolbarHandlerResolved = false;
            s_toolbarEvent = null;
            s_toolbarHandler = null;
            ConsoleLogFilterSettings.SettingsChanged -= ApplySettings;
            EditorApplication.delayCall -= ApplySettings;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
        }

        internal static bool IsAssistantConsoleToolbarAvailable => TryGetToolbarHandler(out _, out _);

        private static void ApplySettings()
        {
            if (!TryGetToolbarHandler(out var toolbarEvent, out var toolbarHandler))
                return;

            try
            {
                var arguments = new object[] { toolbarHandler };
                toolbarEvent.GetRemoveMethod(true)?.Invoke(null, arguments);
                if (!ConsoleLogFilterSettings.Data.hideAssistantToolbarButton)
                    toolbarEvent.GetAddMethod(true)?.Invoke(null, arguments);
            }
            catch
            {
                // An incompatible optional integration must not interrupt settings updates.
                s_toolbarEvent = null;
                s_toolbarHandler = null;
            }
        }

        private static bool TryGetToolbarHandler(out EventInfo toolbarEvent, out Delegate toolbarHandler)
        {
            if (!s_toolbarHandlerResolved)
            {
                s_toolbarHandlerResolved = true;
                try
                {
                    ResolveToolbarHandler(out s_toolbarEvent, out s_toolbarHandler);
                }
                catch
                {
                    s_toolbarEvent = null;
                    s_toolbarHandler = null;
                }
            }

            toolbarEvent = s_toolbarEvent;
            toolbarHandler = s_toolbarHandler;
            return toolbarEvent != null && toolbarHandler != null;
        }

        private static bool ResolveToolbarHandler(out EventInfo toolbarEvent, out Delegate toolbarHandler)
        {
            toolbarEvent = null;
            toolbarHandler = null;

            var consoleUtilsType = FindLoadedType(AssistantBridgeAssemblyName, AssistantBridgeConsoleUtilsTypeName);
            var consoleUiType = FindLoadedType(AssistantUiAssemblyName, AssistantUiConsoleTypeName);
            if (consoleUtilsType == null || consoleUiType == null)
                return false;

            toolbarEvent = consoleUtilsType.GetEvent(
                ToolbarEventName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (toolbarEvent?.EventHandlerType == null)
                return false;

            var toolbarHandlerMethod = consoleUiType.GetMethod(
                ToolbarHandlerMethodName,
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (toolbarHandlerMethod == null)
                return false;

            toolbarHandler = Delegate.CreateDelegate(toolbarEvent.EventHandlerType, toolbarHandlerMethod);
            return toolbarHandler != null;
        }

        private static Type FindLoadedType(string assemblyName, string typeName)
        {
#if UNITY_6000_5_OR_NEWER
            var assemblies = UnityEngine.Assemblies.CurrentAssemblies.GetLoadedAssemblies();
#else
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
#endif
            foreach (var assembly in assemblies)
            {
                if (assembly == null)
                    continue;

                var currentAssemblyName = assembly.GetName().Name;
                if (!string.Equals(currentAssemblyName, assemblyName, StringComparison.Ordinal))
                    continue;

                return assembly.GetType(typeName, false);
            }

            return null;
        }
    }
}
