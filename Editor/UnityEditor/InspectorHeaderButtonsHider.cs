using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

namespace MinimalEditor.Editor
{
    // Removes Unity's help and preset buttons from Inspector headers by editing the shared list of
    // header item callbacks that Unity fills the first time it draws an Inspector header.
    // Editor configuration persists across Play Mode sessions.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class InspectorHeaderButtonsHider
    {
        const BindingFlags AnyStatic = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
        const BindingFlags AnyInstance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        const double FallbackPollIntervalSeconds = 15d;

        internal enum Button
        {
            Help,
            Preset
        }

        static readonly Assembly EditorAssembly = typeof(UnityEditor.Editor).Assembly;
        static readonly FieldInfo HeaderItemsField =
            typeof(EditorGUIUtility).GetField("s_EditorHeaderItemsMethods", AnyStatic);
        static readonly Type HeaderItemAttributeType = EditorAssembly.GetType("UnityEditor.EditorHeaderItemAttribute");
        // The preset button lives in a separate Editor module, so both are found through Unity's attribute scan.
        static readonly MethodInfo HelpMethod = FindHeaderItem("UnityEditor.EditorGUI", "HelpIconButton");
        static readonly MethodInfo PresetMethod = FindHeaderItem("UnityEditor.Presets.PresetSelector", "DrawPresetButton");
        static readonly MethodInfo RepaintAllInspectorsMethod =
            EditorAssembly.GetType("UnityEditor.InspectorWindow")?.GetMethod("RepaintAllInspectors", AnyStatic);

        // Callbacks removed by this hider, so they can be put back in Unity's order.
        static readonly Dictionary<Button, Delegate> s_Removed = new Dictionary<Button, Delegate>();
        static bool s_WaitingForList;
        static double s_LastPollTimestamp;

        internal static bool IsSupported => HeaderItemsField != null &&
                                            typeof(IList).IsAssignableFrom(HeaderItemsField.FieldType);

        internal static bool IsButtonSupported(Button button) => IsSupported && GetMethod(button) != null;

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            EditorApplication.update -= PollFallback;
            EditorApplication.update += PollFallback;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
            Apply();
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void Shutdown()
        {
            EditorApplication.update -= PollFallback;
            EditorApplication.update -= WaitForList;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            // Unity's list belongs to the Editor. Put back what this code removed before it unloads.
            Restore(Button.Help);
            Restore(Button.Preset);
            s_Removed.Clear();
            s_WaitingForList = false;
            s_LastPollTimestamp = 0d;
        }

        internal static bool HideHelp
        {
            get => UnityEditorPreferences.HideInspectorHelpButton;
            set
            {
                if (HideHelp == value) return;
                UnityEditorPreferences.HideInspectorHelpButton = value;
                Apply();
            }
        }

        internal static bool HidePreset
        {
            get => UnityEditorPreferences.HideInspectorPresetButton;
            set
            {
                if (HidePreset == value) return;
                UnityEditorPreferences.HideInspectorPresetButton = value;
                Apply();
            }
        }

        internal static void Apply()
        {
            if (!IsSupported) return;

            var changed = ApplyButton(Button.Help, HideHelp);
            changed |= ApplyButton(Button.Preset, HidePreset);
            if (changed) RepaintInspectors();
        }

        static void PollFallback()
        {
            if (EditorApplication.timeSinceStartup - s_LastPollTimestamp < FallbackPollIntervalSeconds) return;
            s_LastPollTimestamp = EditorApplication.timeSinceStartup;
            if (HideHelp || HidePreset)
                Apply();
        }

        // Unity fills the list lazily while drawing the first Inspector header after a reload.
        static void WaitForList()
        {
            if (GetList() == null) return;
            EditorApplication.update -= WaitForList;
            s_WaitingForList = false;
            Apply();
        }

        static bool ApplyButton(Button button, bool hide)
        {
            return hide ? Remove(button) : Restore(button);
        }

        static bool Remove(Button button)
        {
            var method = GetMethod(button);
            if (method == null) return false;

            IList list;
            try
            {
                list = GetList();
            }
            catch
            {
                return false;
            }

            if (list == null)
            {
                if (!s_WaitingForList)
                {
                    s_WaitingForList = true;
                    EditorApplication.update += WaitForList;
                }
                return false;
            }

            var changed = false;
            try
            {
                for (var i = list.Count - 1; i >= 0; i--)
                {
                    if (!(list[i] is Delegate callback) || callback.Method != method) continue;
                    list.RemoveAt(i);
                    s_Removed[button] = callback;
                    changed = true;
                }
            }
            catch
            {
                // Fail silently for editor-internal layout/runtime changes.
            }
            return changed;
        }

        static bool Restore(Button button)
        {
            if (!s_Removed.TryGetValue(button, out var callback)) return false;
            s_Removed.Remove(button);

            try
            {
                // A null list is rebuilt by Unity with every callback, so nothing needs restoring.
                var list = GetList();
                if (list == null) return false;

                foreach (var item in list)
                {
                    if (item is Delegate existing && existing.Method == callback.Method)
                        return false;
                }

                var order = GetCallbackOrder(callback.Method);
                var index = list.Count;
                for (var i = 0; i < list.Count; i++)
                {
                    if (list[i] is Delegate existing && GetCallbackOrder(existing.Method) > order)
                    {
                        index = i;
                        break;
                    }
                }
                list.Insert(index, callback);
                return true;
            }
            catch
            {
                return false;
            }
        }

        static MethodInfo FindHeaderItem(string typeName, string methodName)
        {
            if (HeaderItemAttributeType == null) return null;
            try
            {
                foreach (var method in TypeCache.GetMethodsWithAttribute(HeaderItemAttributeType))
                {
                    if (method.IsStatic && method.Name == methodName && method.DeclaringType?.FullName == typeName)
                        return method;
                }
            }
            catch
            {
                // Fail closed when Unity changes how header items are registered.
            }
            return null;
        }

        static IList GetList()
        {
            return HeaderItemsField?.GetValue(null) as IList;
        }

        static MethodInfo GetMethod(Button button)
        {
            return button == Button.Help ? HelpMethod : PresetMethod;
        }

        static int GetCallbackOrder(MethodInfo method)
        {
            if (HeaderItemAttributeType == null) return 0;
            var attribute = method.GetCustomAttribute(HeaderItemAttributeType);
            if (attribute == null) return 0;
            var property = HeaderItemAttributeType.GetProperty("callbackOrder", AnyInstance);
            if (property?.GetValue(attribute) is int propertyOrder) return propertyOrder;
            var field = HeaderItemAttributeType.GetField("m_CallbackOrder", AnyInstance);
            return field?.GetValue(attribute) is int fieldOrder ? fieldOrder : 0;
        }

        static void RepaintInspectors()
        {
            try
            {
                RepaintAllInspectorsMethod?.Invoke(null, null);
            }
            catch
            {
                // Repainting is cosmetic; the next Inspector repaint shows the change.
            }
        }
    }
}
