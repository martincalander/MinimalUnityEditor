using System;
using System.Reflection;
using UnityEditor;

namespace MartinCalander.Essentials.Console.Editor
{
    [InitializeOnLoad]
    internal static class ConsoleAssistantToolbarButtonController
    {
        private const string AssistantBridgeAssemblyName = "Unity.AI.Assistant.Bridge.Editor";
        private const string AssistantBridgeConsoleUtilsTypeName = "Unity.AI.Assistant.Bridge.Editor.ConsoleUtils";
        private const string AssistantUiAssemblyName = "Unity.AI.Assistant.UI.Editor";
        private const string AssistantUiConsoleTypeName = "Unity.AI.Assistant.UI.Editor.Scripts.ConsoleUI";
        private const string ToolbarEventName = "s_DrawCustomToolbarGuiEvent";
        private const string ToolbarHandlerMethodName = "OnDrawCustomGuiEvent";

        static ConsoleAssistantToolbarButtonController()
        {
            ConsoleLogFilterSettings.SettingsChanged += ApplySettings;
            EditorApplication.delayCall += ApplySettings;
        }

        internal static bool IsAssistantConsoleToolbarAvailable => TryGetToolbarHandler(out _, out _);

        private static void ApplySettings()
        {
            if (!TryGetToolbarHandler(out var toolbarEvent, out var toolbarHandler))
                return;

            var removeMethod = toolbarEvent.GetRemoveMethod(true);
            removeMethod?.Invoke(null, new object[] { toolbarHandler });

            if (!ConsoleLogFilterSettings.Data.hideAssistantToolbarButton)
            {
                var addMethod = toolbarEvent.GetAddMethod(true);
                addMethod?.Invoke(null, new object[] { toolbarHandler });
            }
        }

        private static bool TryGetToolbarHandler(out EventInfo toolbarEvent, out Delegate toolbarHandler)
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

            try
            {
                toolbarHandler = Delegate.CreateDelegate(toolbarEvent.EventHandlerType, toolbarHandlerMethod);
                return toolbarHandler != null;
            }
            catch
            {
                return false;
            }
        }

        private static Type FindLoadedType(string assemblyName, string typeName)
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            for (var i = 0; i < assemblies.Length; i++)
            {
                var assembly = assemblies[i];
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
