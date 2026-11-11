using System;
using System.Reflection;
#if UNITY_7000_0_OR_NEWER
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
#endif

namespace MinimalEditor.Editor
{
    // Lets a person quit an Editor that was launched with -automated. In that mode Unity answers
    // its own "save changes?" prompt with Cancel, so a quit is silently dropped while a scene has
    // unsaved changes. Unity never reaches EditorApplication.wantsToQuit in that case, so this
    // patches the first managed method Unity calls on every quit request and runs the quit itself.
    // Unity 7 and newer only. Older Editors compile the preference but never handle a quit.
    // The patch must survive Play Mode sessions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class AutomatedEditorQuit
    {
        internal const string HarmonyId = "com.martincalander.minimaleditor.editor.automated-editor-quit";
        internal const string QuitRequestTypeName = "UnityEditor.SceneManagement.StageNavigationManager";
        internal const string QuitRequestMethodName = "Internal_AskUserToSaveModifiedStageBeforeQuitting";

        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

        internal static bool Active
        {
            get => UnityEditorPreferences.QuitAutomatedEditor;
            set
            {
                UnityEditorPreferences.QuitAutomatedEditor = value;
                Refresh();
            }
        }

        // True while Unity's quit request is patched.
        internal static bool IsInstalled { get; private set; }

        internal static string LastPatchError { get; private set; }

        internal static bool IsAutomated(string[] commandLineArgs)
        {
            if (commandLineArgs == null)
                return false;

            foreach (var arg in commandLineArgs)
            {
                if (arg == "-automated")
                    return true;
            }

            return false;
        }

        // Batch mode has no person to quit it, and a normal Editor shows the prompt itself.
        internal static bool ShouldHandleQuit(bool supported, bool enabled, bool batchMode, string[] commandLineArgs)
        {
            return supported && enabled && !batchMode && IsAutomated(commandLineArgs);
        }

#if UNITY_7000_0_OR_NEWER
        static object s_harmony;
        static bool s_patchFailed;
        static bool s_exiting;

        // Tests replace it so a scheduled exit is recorded, not performed.
        internal static Action<int> ExitAction = EditorApplication.Exit;

        internal static bool IsSupported =>
            !s_patchFailed && GetQuitRequestMethod() != null && GetHarmonyType() != null;

        internal static bool IsQuitPending => s_exiting;

        internal static MethodInfo GetQuitRequestMethod()
        {
            var method = typeof(EditorApplication).Assembly.GetType(QuitRequestTypeName)
                ?.GetMethod(QuitRequestMethodName, AnyStatic, null, Type.EmptyTypes, null);
            return method != null && method.ReturnType == typeof(bool) ? method : null;
        }

        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
        static void Initialize()
        {
            s_exiting = false;
            Refresh();
        }

        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
        static void Shutdown()
        {
            EditorApplication.delayCall -= SaveAndExit;
            try
            {
                RemovePatch();
            }
            catch
            {
                // Leave quitting to Unity if patch cleanup fails during code reload.
            }
            finally
            {
                s_harmony = null;
                s_patchFailed = false;
                s_exiting = false;
                IsInstalled = false;
                LastPatchError = null;
            }
        }

        static void Refresh()
        {
            bool wanted = ShouldHandleQuit(
                IsSupported, Active, Application.isBatchMode, Environment.GetCommandLineArgs());
            if (wanted == IsInstalled)
                return;

            if (wanted)
            {
                TryPatch();
                return;
            }

            try
            {
                RemovePatch();
            }
            catch (Exception exception)
            {
                LastPatchError = exception.ToString();
            }

            s_harmony = null;
            IsInstalled = false;
        }

        static void TryPatch()
        {
            var target = GetQuitRequestMethod();
            var harmonyType = GetHarmonyType();
            if (target == null || harmonyType == null)
                return;

            try
            {
                LastPatchError = null;
                s_harmony = Activator.CreateInstance(harmonyType, new object[] { HarmonyId });
                var harmonyMethodType = harmonyType.Assembly.GetType("HarmonyLib.HarmonyMethod", throwOnError: true);
                var constructor = harmonyMethodType.GetConstructor(new[] { typeof(MethodInfo) })
                    ?? throw new MissingMethodException(harmonyMethodType.FullName, ".ctor(MethodInfo)");
                var patchMethod = harmonyType.GetMethod("Patch", new[]
                {
                    typeof(MethodBase), harmonyMethodType, harmonyMethodType, harmonyMethodType, harmonyMethodType
                }) ?? throw new MissingMethodException(harmonyType.FullName, "Patch");
                var prefix = typeof(AutomatedEditorQuit).GetMethod(nameof(QuitRequestPrefix), AnyStatic);
                patchMethod.Invoke(s_harmony, new[]
                {
                    target, constructor.Invoke(new object[] { prefix }), null, null, null
                });
                IsInstalled = true;
            }
            catch (Exception exception)
            {
                LastPatchError = exception.ToString();
                s_patchFailed = true;
                try
                {
                    RemovePatch();
                }
                catch
                {
                    // Keep the original failure while cleaning up best-effort.
                }

                s_harmony = null;
                IsInstalled = false;
            }
        }

        static Type GetHarmonyType()
        {
            // Reflection keeps the selected Harmony runtime build out of the script API surface.
            return Type.GetType("HarmonyLib.Harmony, 0Harmony", throwOnError: false);
        }

        static void RemovePatch()
        {
            if (s_harmony == null)
                return;

            var harmonyType = s_harmony.GetType();
            var method = harmonyType.GetMethod("UnpatchAll", new[] { typeof(string) })
                ?? throw new MissingMethodException(harmonyType.FullName, "UnpatchAll");
            method.Invoke(s_harmony, new object[] { HarmonyId });
        }

        // Command+Q and closing the main window arrive here before Unity checks for unsaved
        // scenes. Returning false with a false result cancels Unity's own quit; the exit below
        // replaces it. Once that exit is under way, Unity's method runs unchanged.
        internal static bool QuitRequestPrefix(ref bool __result)
        {
            if (s_exiting)
                return true;

            s_exiting = true;
            EditorApplication.delayCall -= SaveAndExit;
            EditorApplication.delayCall += SaveAndExit;
            __result = false;
            return false;
        }

        internal static void CancelPendingQuit()
        {
            EditorApplication.delayCall -= SaveAndExit;
            s_exiting = false;
        }

        static void SaveAndExit()
        {
            EditorApplication.delayCall -= SaveAndExit;

            // Scene changes made in Play Mode are not kept, so there is nothing to save.
            if (!EditorApplication.isPlaying && !TrySaveOpenWork())
            {
                s_exiting = false;
                return;
            }

            ExitAction(0);
        }

        static bool TrySaveOpenWork()
        {
            var failed = new List<string>();

            var stage = PrefabStageUtility.GetCurrentPrefabStage();
            if (stage != null && stage.scene.isDirty)
            {
                PrefabUtility.SaveAsPrefabAsset(stage.prefabContentsRoot, stage.assetPath, out bool saved);
                if (saved)
                    stage.ClearDirtiness();
                else
                    failed.Add(stage.assetPath);
            }

            for (var i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                if (!scene.isDirty)
                    continue;

                if (string.IsNullOrEmpty(scene.path))
                {
                    Debug.LogWarning("Minimal Editor: quitting the automated Editor dropped changes in an untitled scene.");
                    continue;
                }

                if (!EditorSceneManager.SaveScene(scene))
                    failed.Add(scene.path);
            }

            if (failed.Count > 0)
            {
                Debug.LogError("Minimal Editor: the quit was cancelled because these could not be saved: " +
                    string.Join(", ", failed));
                return false;
            }

            AssetDatabase.SaveAssets();
            return true;
        }
#else
        internal static bool IsSupported => false;

        static void Refresh()
        {
        }
#endif
    }
}
