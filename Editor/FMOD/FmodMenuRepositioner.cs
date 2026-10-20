using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.FMOD.Editor
{
    // Menu commands and reflection metadata belong to the Editor session.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    [InitializeOnLoad]
    internal static class FmodMenuRepositioner
    {
        const string TargetRoot = "Tools/FMOD";

        const string EditSettingsPath = TargetRoot + "/Edit Settings";
        const string RefreshBanksPath = TargetRoot + "/Refresh Banks";
        const string SetupWizardPath = TargetRoot + "/Setup Wizard";
        const string EventBrowserPath = TargetRoot + "/Event Browser";
        const string FindAndReplacePath = TargetRoot + "/Find and Replace";
        const string UpdateEventReferencesPath = TargetRoot + "/Update Event References";
        const string ReorganizePluginFilesPath = TargetRoot + "/Reorganize Plugin Files";
        const string GenerateVisualScriptingUnitsPath = TargetRoot + "/Generate Visual Scripting Units";
        const string GettingStartedPath = TargetRoot + "/Help/Getting Started";
        const string IntegrationManualPath = TargetRoot + "/Help/Integration Manual";
        const string ApiManualPath = TargetRoot + "/Help/API Manual";
        const string SupportForumPath = TargetRoot + "/Help/Support Forum";
        const string RevisionHistoryPath = TargetRoot + "/Help/Revision History";
        const string AboutIntegrationPath = TargetRoot + "/About Integration";

        const BindingFlags StaticMemberFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const int MaxRemovalAttempts = 8;

        static readonly MethodInfo RemoveMenuItemMethod =
            typeof(Menu).GetMethod("RemoveMenuItem", StaticMemberFlags, null, new[] { typeof(string) }, null);

        static readonly FmodMenuCommand EditSettingsCommand =
            new FmodMenuCommand("FMOD/Edit Settings", "FMODUnity.EditorSettings", "EditSettings");

        static readonly FmodMenuCommand RefreshBanksCommand =
            new FmodMenuCommand("FMOD/Refresh Banks", "FMODUnity.EventManager", "RefreshBanks");

        static readonly FmodMenuCommand SetupWizardCommand =
            new FmodMenuCommand("FMOD/Setup Wizard", "FMODUnity.SetupWizardWindow", "ShowAssistant");

        static readonly FmodMenuCommand EventBrowserCommand =
            new FmodMenuCommand("FMOD/Event Browser", "FMODUnity.EventBrowser", "ShowWindow");

        static readonly FmodMenuCommand FindAndReplaceCommand =
            new FmodMenuCommand("FMOD/Find and Replace", "FMODUnity.FindAndReplace", "ShowFindAndReplace");

        static readonly FmodMenuCommand UpdateEventReferencesCommand =
            new FmodMenuCommand("FMOD/Update Event References", "FMODUnity.EventReferenceUpdater", "ShowWindow");

        static readonly FmodMenuCommand ReorganizePluginFilesCommand =
            new FmodMenuCommand("FMOD/Reorganize Plugin Files", "FMODUnity.FileReorganizer", "ShowWindow");

        static readonly FmodMenuCommand GenerateVisualScriptingUnitsCommand =
            new FmodMenuCommand(
                "FMOD/Generate Visual Scripting Units",
                "FMODUnity.BoltIntegration",
                "GenerateBoltUnitOptions",
                "IsBoltPresent");

        static readonly FmodMenuCommand GettingStartedCommand =
            new FmodMenuCommand("FMOD/Help/Getting Started", "FMODUnity.EditorUtils", "OnlineGettingStarted");

        static readonly FmodMenuCommand IntegrationManualCommand =
            new FmodMenuCommand("FMOD/Help/Integration Manual", "FMODUnity.EditorUtils", "OnlineManual");

        static readonly FmodMenuCommand ApiManualCommand =
            new FmodMenuCommand("FMOD/Help/API Manual", "FMODUnity.EditorUtils", "OnlineAPIDocs");

        static readonly FmodMenuCommand SupportForumCommand =
            new FmodMenuCommand("FMOD/Help/Support Forum", "FMODUnity.EditorUtils", "OnlineQA");

        static readonly FmodMenuCommand RevisionHistoryCommand =
            new FmodMenuCommand("FMOD/Help/Revision History", "FMODUnity.EditorUtils", "OnlineRevisions");

        static readonly FmodMenuCommand AboutIntegrationCommand =
            new FmodMenuCommand("FMOD/About Integration", "FMODUnity.EditorUtils", "About");

        static readonly FmodMenuCommand[] Commands =
        {
            EditSettingsCommand,
            RefreshBanksCommand,
            SetupWizardCommand,
            EventBrowserCommand,
            FindAndReplaceCommand,
            UpdateEventReferencesCommand,
            ReorganizePluginFilesCommand,
            GenerateVisualScriptingUnitsCommand,
            GettingStartedCommand,
            IntegrationManualCommand,
            ApiManualCommand,
            SupportForumCommand,
            RevisionHistoryCommand,
            AboutIntegrationCommand
        };

        static readonly Dictionary<string, Type> TypeCache = new Dictionary<string, Type>();
        static readonly HashSet<string> MissingCommandWarnings = new HashSet<string>();

        static int s_removalAttemptCount;
        static bool s_loggedMissingRemoveMenuItemMethod;

        static FmodMenuRepositioner()
        {
            QueueRootMenuRemoval();
        }

        [MenuItem(EditSettingsPath, true)]
        static bool ValidateEditSettings()
        {
            return CanInvokeOriginal(EditSettingsCommand);
        }

        [MenuItem(EditSettingsPath, priority = 0)]
        static void EditSettings()
        {
            InvokeOriginal(EditSettingsCommand);
        }

        [MenuItem(RefreshBanksPath, true)]
        static bool ValidateRefreshBanks()
        {
            return CanInvokeOriginal(RefreshBanksCommand);
        }

        [MenuItem(RefreshBanksPath, priority = 1)]
        static void RefreshBanks()
        {
            InvokeOriginal(RefreshBanksCommand);
        }

        [MenuItem(SetupWizardPath, true)]
        static bool ValidateSetupWizard()
        {
            return CanInvokeOriginal(SetupWizardCommand);
        }

        [MenuItem(SetupWizardPath)]
        static void SetupWizard()
        {
            InvokeOriginal(SetupWizardCommand);
        }

        [MenuItem(EventBrowserPath, true)]
        static bool ValidateEventBrowser()
        {
            return CanInvokeOriginal(EventBrowserCommand);
        }

        [MenuItem(EventBrowserPath, priority = 2)]
        static void EventBrowser()
        {
            InvokeOriginal(EventBrowserCommand);
        }

#if !FMOD_SERIALIZE_GUID_ONLY
        [MenuItem(FindAndReplacePath, true)]
        static bool ValidateFindAndReplace()
        {
            return CanInvokeOriginal(FindAndReplaceCommand);
        }

        [MenuItem(FindAndReplacePath, priority = 2)]
        static void FindAndReplace()
        {
            InvokeOriginal(FindAndReplaceCommand);
        }
#endif

        [MenuItem(UpdateEventReferencesPath, true)]
        static bool ValidateUpdateEventReferences()
        {
            return CanInvokeOriginal(UpdateEventReferencesCommand);
        }

        [MenuItem(UpdateEventReferencesPath)]
        static void UpdateEventReferences()
        {
            InvokeOriginal(UpdateEventReferencesCommand);
        }

        [MenuItem(ReorganizePluginFilesPath, true)]
        static bool ValidateReorganizePluginFiles()
        {
            return CanInvokeOriginal(ReorganizePluginFilesCommand);
        }

        [MenuItem(ReorganizePluginFilesPath)]
        static void ReorganizePluginFiles()
        {
            InvokeOriginal(ReorganizePluginFilesCommand);
        }

        [MenuItem(GenerateVisualScriptingUnitsPath, true)]
        static bool ValidateGenerateVisualScriptingUnits()
        {
            return ValidateOriginal(GenerateVisualScriptingUnitsCommand);
        }

        [MenuItem(GenerateVisualScriptingUnitsPath)]
        static void GenerateVisualScriptingUnits()
        {
            InvokeOriginal(GenerateVisualScriptingUnitsCommand);
        }

        [MenuItem(GettingStartedPath, true)]
        static bool ValidateGettingStarted()
        {
            return CanInvokeOriginal(GettingStartedCommand);
        }

        [MenuItem(GettingStartedPath, priority = 2)]
        static void GettingStarted()
        {
            InvokeOriginal(GettingStartedCommand);
        }

        [MenuItem(IntegrationManualPath, true)]
        static bool ValidateIntegrationManual()
        {
            return CanInvokeOriginal(IntegrationManualCommand);
        }

        [MenuItem(IntegrationManualPath, priority = 3)]
        static void IntegrationManual()
        {
            InvokeOriginal(IntegrationManualCommand);
        }

        [MenuItem(ApiManualPath, true)]
        static bool ValidateApiManual()
        {
            return CanInvokeOriginal(ApiManualCommand);
        }

        [MenuItem(ApiManualPath, priority = 4)]
        static void ApiManual()
        {
            InvokeOriginal(ApiManualCommand);
        }

        [MenuItem(RevisionHistoryPath, true)]
        static bool ValidateRevisionHistory()
        {
            return CanInvokeOriginal(RevisionHistoryCommand);
        }

        [MenuItem(RevisionHistoryPath, priority = 5)]
        static void RevisionHistory()
        {
            InvokeOriginal(RevisionHistoryCommand);
        }

        [MenuItem(SupportForumPath, true)]
        static bool ValidateSupportForum()
        {
            return CanInvokeOriginal(SupportForumCommand);
        }

        [MenuItem(SupportForumPath, priority = 16)]
        static void SupportForum()
        {
            InvokeOriginal(SupportForumCommand);
        }

        [MenuItem(AboutIntegrationPath, true)]
        static bool ValidateAboutIntegration()
        {
            return CanInvokeOriginal(AboutIntegrationCommand);
        }

        [MenuItem(AboutIntegrationPath, priority = 7)]
        static void AboutIntegration()
        {
            InvokeOriginal(AboutIntegrationCommand);
        }

        static void QueueRootMenuRemoval()
        {
            EditorApplication.delayCall += RemoveRootMenuItems;
        }

        static void RemoveRootMenuItems()
        {
            s_removalAttemptCount++;

            for (var i = 0; i < Commands.Length; i++)
            {
                // Keep the original available when this FMOD version has no callable replacement.
                if (CanInvokeOriginal(Commands[i]))
                    TryRemoveRootMenuItem(Commands[i].RootPath);
            }

            if (s_removalAttemptCount < MaxRemovalAttempts)
            {
                QueueRootMenuRemoval();
            }
        }

        static void TryRemoveRootMenuItem(string path)
        {
            if (RemoveMenuItemMethod == null)
            {
                if (!s_loggedMissingRemoveMenuItemMethod)
                {
                    Debug.LogWarning("[Minimal Editor FMOD] UnityEditor.Menu.RemoveMenuItem was not found. The root FMOD menu could not be removed.");
                    s_loggedMissingRemoveMenuItemMethod = true;
                }

                return;
            }

            try
            {
                RemoveMenuItemMethod.Invoke(null, new object[] { path });
            }
            catch
            {
                // A missing menu item is harmless; later attempts catch items registered after this assembly loads.
            }
        }

        static bool ValidateOriginal(FmodMenuCommand command)
        {
            if (string.IsNullOrEmpty(command.ValidationMethodName))
            {
                return CanInvokeOriginal(command);
            }

            var validationMethod = GetOriginalMethod(command.TypeName, command.ValidationMethodName);
            if (validationMethod == null)
            {
                return CanInvokeOriginal(command);
            }

            try
            {
                return validationMethod.Invoke(null, null) is bool enabled && enabled;
            }
            catch
            {
                return false;
            }
        }

        static bool CanInvokeOriginal(FmodMenuCommand command)
        {
            return GetOriginalMethod(command.TypeName, command.ExecuteMethodName) != null;
        }

        static void InvokeOriginal(FmodMenuCommand command)
        {
            var method = GetOriginalMethod(command.TypeName, command.ExecuteMethodName);
            if (method == null)
            {
                if (MissingCommandWarnings.Add(command.RootPath))
                {
                    Debug.LogWarning("[Minimal Editor FMOD] Could not find original FMOD menu command: " + command.RootPath);
                }

                return;
            }

            try
            {
                method.Invoke(null, null);
            }
            catch (TargetInvocationException e)
            {
                Debug.LogException(e.InnerException ?? e);
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        static MethodInfo GetOriginalMethod(string typeName, string methodName)
        {
            try
            {
                var type = ResolveFmodEditorType(typeName);
                var method = type?.GetMethod(methodName, StaticMemberFlags, null, Type.EmptyTypes, null);
                return method != null && !method.ContainsGenericParameters ? method : null;
            }
            catch
            {
                // Leave the original command available when FMOD's reflected API is incompatible.
                return null;
            }
        }

        static Type ResolveFmodEditorType(string typeName)
        {
            if (TypeCache.TryGetValue(typeName, out var cachedType))
            {
                return cachedType;
            }

            var type = Type.GetType(typeName + ", FMODUnityEditor");
            if (type == null)
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (var i = 0; i < assemblies.Length; i++)
                {
                    if (assemblies[i].GetName().Name != "FMODUnityEditor")
                    {
                        continue;
                    }

                    type = assemblies[i].GetType(typeName);
                    break;
                }
            }

            if (type != null)
            {
                TypeCache[typeName] = type;
            }

            return type;
        }

        sealed class FmodMenuCommand
        {
            internal FmodMenuCommand(
                string rootPath,
                string typeName,
                string executeMethodName,
                string validationMethodName = null)
            {
                RootPath = rootPath;
                TypeName = typeName;
                ExecuteMethodName = executeMethodName;
                ValidationMethodName = validationMethodName;
            }

            internal string RootPath { get; }
            internal string TypeName { get; }
            internal string ExecuteMethodName { get; }
            internal string ValidationMethodName { get; }
        }
    }
}
