using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Essentials.Editor
{
    [InitializeOnLoad]
    internal static class HierarchySceneAutoSave
    {
        static bool s_saveQueued;
        static bool s_isSaving;

        static HierarchySceneAutoSave()
        {
            EditorApplication.update += OnEditorUpdate;
            EditorSceneManager.sceneDirtied += OnSceneDirtied;
            EditorSceneManager.sceneSaved += OnSceneSaved;
        }

        internal static bool Enabled
        {
            get => UnityEditorPreferences.HierarchySceneAutoSave;
            set
            {
                if (Enabled == value)
                    return;

                UnityEditorPreferences.HierarchySceneAutoSave = value;
                s_saveQueued = false;
            }
        }

        static void OnEditorUpdate()
        {
            if (!Enabled || !s_saveQueued)
                return;

            if (!CanAutoSaveNow())
                return;

            s_saveQueued = false;
            SaveDirtyScenes();
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

        static int SaveDirtyScenes()
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

    }
}
