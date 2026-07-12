using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Essentials.Editor
{
    [InitializeOnLoad]
    internal static class HierarchySceneAutoSave
    {
        const string EnabledKey = "Essentials.Editor.HierarchySceneAutoSave.Enabled";

        const string EnableActionId = "Essentials.Editor.HierarchySceneAutoSave.Enable";
        const string DisableActionId = "Essentials.Editor.HierarchySceneAutoSave.Disable";
        const string SaveNowActionId = "Essentials.Editor.HierarchySceneAutoSave.SaveNow";

        const int EnableActionPriority = 500;
        const int DisableActionPriority = 501;
        const int SaveNowActionPriority = 502;

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        static readonly Assembly UnityEditorAssembly = typeof(UnityEditor.Editor).Assembly;
        static readonly Type HostViewType = UnityEditorAssembly.GetType("UnityEditor.HostView");
        static readonly Type WindowActionType = UnityEditorAssembly.GetType("UnityEditor.WindowAction");
        static readonly Type ExecuteHandlerType = WindowActionType?.GetNestedType("ExecuteHandler", AnyStatic);
        static readonly Type ValidateHandlerType = WindowActionType?.GetNestedType("ValidateHandler", AnyStatic);
        static readonly MethodInfo WindowActionsGetter = HostViewType?.GetProperty("windowActions", AnyStatic)?.GetGetMethod(nonPublic: true);
        static readonly FieldInfo WindowActionsField = HostViewType?.GetField("s_windowActions", AnyStatic);
        static readonly MethodInfo CreateWindowMenuItemMethod = WindowActionType?.GetMethod(
            "CreateWindowMenuItem",
            AnyStatic,
            binder: null,
            types: new[] { typeof(string), ExecuteHandlerType, typeof(string) },
            modifiers: null);
        static readonly FieldInfo ActionIdField = WindowActionType?.GetField("id", AnyInstance);
        static readonly FieldInfo MenuPathField = WindowActionType?.GetField("menuPath", AnyInstance);
        static readonly FieldInfo PriorityField = WindowActionType?.GetField("priority", AnyInstance);
        static readonly FieldInfo ValidateHandlerField = WindowActionType?.GetField("validateHandler", AnyInstance);

        static readonly MethodInfo EnableActionExecuteMethod = typeof(HierarchySceneAutoSave).GetMethod(nameof(EnableActionExecute), AnyStatic);
        static readonly MethodInfo DisableActionExecuteMethod = typeof(HierarchySceneAutoSave).GetMethod(nameof(DisableActionExecute), AnyStatic);
        static readonly MethodInfo SaveNowActionExecuteMethod = typeof(HierarchySceneAutoSave).GetMethod(nameof(SaveNowActionExecute), AnyStatic);
        static readonly MethodInfo EnableActionValidateMethod = typeof(HierarchySceneAutoSave).GetMethod(nameof(EnableActionValidate), AnyStatic);
        static readonly MethodInfo DisableActionValidateMethod = typeof(HierarchySceneAutoSave).GetMethod(nameof(DisableActionValidate), AnyStatic);
        static readonly MethodInfo SaveNowActionValidateMethod = typeof(HierarchySceneAutoSave).GetMethod(nameof(SaveNowActionValidate), AnyStatic);

        static object s_enableAction;
        static object s_disableAction;
        static object s_saveNowAction;
        static bool s_saveQueued;
        static bool s_isSaving;

        static HierarchySceneAutoSave()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            EditorSceneManager.sceneSaved += OnSceneSaved;
            EditorApplication.delayCall += EnsureWindowActionsRegistered;
        }

        static bool Enabled
        {
            get => EditorPrefs.GetBool(EnabledKey, false);
            set
            {
                if (Enabled == value)
                    return;

                EditorPrefs.SetBool(EnabledKey, value);
                s_saveQueued = false;
                UpdateActionMenuPaths();
            }
        }

        static void OnEditorUpdate()
        {
            if (!Enabled || !s_saveQueued)
                return;

            if (!CanAutoSaveNow())
                return;

            s_saveQueued = false;
            SaveDirtyScenes(logResult: false);
        }

        static bool CanAutoSaveNow()
        {
            return !s_isSaving
                && !EditorApplication.isPlayingOrWillChangePlaymode
                && !EditorApplication.isCompiling
                && !EditorApplication.isUpdating
                && !BuildPipeline.isBuildingPlayer;
        }

        static void OnSceneDirtied(Scene scene)
        {
            if (!Enabled || s_isSaving || !ShouldAutoSave(scene))
                return;

            s_saveQueued = true;
        }

        static void OnSceneSaved(Scene scene)
        {
            if (!scene.IsValid())
                return;

            s_saveQueued = false;
        }

        static int SaveDirtyScenes(bool logResult)
        {
            s_isSaving = true;
            int savedSceneCount = 0;

            try
            {
                for (int i = 0; i < SceneManager.sceneCount; i++)
                {
                    Scene scene = SceneManager.GetSceneAt(i);
                    if (!ShouldAutoSave(scene))
                        continue;

                    if (EditorSceneManager.SaveScene(scene, scene.path, saveAsCopy: false))
                        savedSceneCount++;
                }
            }
            finally
            {
                s_isSaving = false;
            }

            if (logResult && savedSceneCount > 0)
            {
                Debug.Log($"[Hierarchy Auto Save] Saved {savedSceneCount} dirty scene(s).");
            }
            else if (logResult)
            {
                Debug.Log("[Hierarchy Auto Save] No dirty saved scenes to save.");
            }

            return savedSceneCount;
        }

        static bool ShouldAutoSave(Scene scene)
        {
            return scene.IsValid()
                && scene.isLoaded
                && scene.isDirty
                && !EditorSceneManager.IsPreviewScene(scene)
                && !string.IsNullOrEmpty(scene.path)
                && scene.path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);
        }

        static void EnsureWindowActionsRegistered()
        {
            if (!CanRegisterWindowActions())
                return;

            object[] existingActions = GetWindowActions();
            List<object> mergedActions = new List<object>(existingActions.Length + 3);
            mergedActions.AddRange(existingActions);

            foreach (object existingAction in existingActions)
            {
                string existingId = GetActionId(existingAction);
                switch (existingId)
                {
                    case EnableActionId:
                        s_enableAction = existingAction;
                        break;
                    case DisableActionId:
                        s_disableAction = existingAction;
                        break;
                    case SaveNowActionId:
                        s_saveNowAction = existingAction;
                        break;
                }
            }

            if (s_enableAction == null)
            {
                s_enableAction = CreateWindowAction(
                    EnableActionId,
                    CreateExecuteHandler(EnableActionExecuteMethod),
                    CreateValidateHandler(EnableActionValidateMethod),
                    BuildEnableMenuPath(),
                    EnableActionPriority);
                if (s_enableAction != null)
                    mergedActions.Add(s_enableAction);
            }

            if (s_disableAction == null)
            {
                s_disableAction = CreateWindowAction(
                    DisableActionId,
                    CreateExecuteHandler(DisableActionExecuteMethod),
                    CreateValidateHandler(DisableActionValidateMethod),
                    BuildDisableMenuPath(),
                    DisableActionPriority);
                if (s_disableAction != null)
                    mergedActions.Add(s_disableAction);
            }

            if (s_saveNowAction == null)
            {
                s_saveNowAction = CreateWindowAction(
                    SaveNowActionId,
                    CreateExecuteHandler(SaveNowActionExecuteMethod),
                    CreateValidateHandler(SaveNowActionValidateMethod),
                    "Auto Save/Save Dirty Scenes Now",
                    SaveNowActionPriority);
                if (s_saveNowAction != null)
                    mergedActions.Add(s_saveNowAction);
            }

            Array mergedArray = Array.CreateInstance(WindowActionType, mergedActions.Count);
            for (int i = 0; i < mergedActions.Count; i++)
            {
                mergedArray.SetValue(mergedActions[i], i);
            }

            WindowActionsField.SetValue(null, mergedArray);
            UpdateActionMenuPaths();
        }

        static bool CanRegisterWindowActions()
        {
            return HostViewType != null
                && WindowActionType != null
                && ExecuteHandlerType != null
                && ValidateHandlerType != null
                && WindowActionsGetter != null
                && WindowActionsField != null
                && CreateWindowMenuItemMethod != null
                && ActionIdField != null
                && MenuPathField != null
                && PriorityField != null
                && ValidateHandlerField != null;
        }

        static object[] GetWindowActions()
        {
            Array actions = WindowActionsGetter?.Invoke(null, null) as Array;
            if (actions == null || actions.Length == 0)
                return Array.Empty<object>();

            object[] result = new object[actions.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                result[i] = actions.GetValue(i);
            }

            return result;
        }

        static string GetActionId(object action)
        {
            return action != null ? ActionIdField.GetValue(action) as string : null;
        }

        static object CreateWindowAction(string id, Delegate executeHandler, Delegate validateHandler, string menuPath, int priority)
        {
            if (executeHandler == null || validateHandler == null)
                return null;

            object action = CreateWindowMenuItemMethod.Invoke(null, new object[] { id, executeHandler, menuPath });
            if (action == null)
                return null;

            ValidateHandlerField.SetValue(action, validateHandler);
            PriorityField.SetValue(action, priority);
            return action;
        }

        static Delegate CreateExecuteHandler(MethodInfo method)
        {
            return CreateActionDelegate(ExecuteHandlerType, method);
        }

        static Delegate CreateValidateHandler(MethodInfo method)
        {
            return CreateActionDelegate(ValidateHandlerType, method);
        }

        static Delegate CreateActionDelegate(Type delegateType, MethodInfo method)
        {
            if (delegateType == null || method == null)
                return null;

            try
            {
                return Delegate.CreateDelegate(delegateType, method, throwOnBindFailure: false);
            }
            catch
            {
                return null;
            }
        }

        static void UpdateActionMenuPaths()
        {
            if (s_enableAction != null)
                MenuPathField.SetValue(s_enableAction, BuildEnableMenuPath());

            if (s_disableAction != null)
                MenuPathField.SetValue(s_disableAction, BuildDisableMenuPath());
        }

        static string BuildEnableMenuPath()
        {
            return "Auto Save/Enable On Scene Edit";
        }

        static string BuildDisableMenuPath()
        {
            return "Auto Save/Disable On Scene Edit";
        }

        static bool IsHierarchyWindow(EditorWindow window)
        {
            return window != null
                && window.GetType().Name.IndexOf("SceneHierarchyWindow", StringComparison.Ordinal) >= 0;
        }

        static void EnableActionExecute(EditorWindow window, object action)
        {
            Enabled = true;
        }

        static void DisableActionExecute(EditorWindow window, object action)
        {
            Enabled = false;
        }

        static void SaveNowActionExecute(EditorWindow window, object action)
        {
            SaveDirtyScenes(logResult: true);
        }

        static bool EnableActionValidate(EditorWindow window, object action)
        {
            UpdateActionMenuPaths();
            return IsHierarchyWindow(window) && !Enabled;
        }

        static bool DisableActionValidate(EditorWindow window, object action)
        {
            UpdateActionMenuPaths();
            return IsHierarchyWindow(window) && Enabled;
        }

        static bool SaveNowActionValidate(EditorWindow window, object action)
        {
            return IsHierarchyWindow(window);
        }
    }
}
