using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MinimalEditor.FolderIcons.Editor
{
    // Draws folder icons through the public Project window item callback. The callback
    // runs for every visible row on every GUI event, so rows without an icon return after
    // one event check and one dictionary lookup.
#if UNITY_6000_5_OR_NEWER
    [Unity.Scripting.LifecycleManagement.NoAutoStaticsCleanup]
#endif
    internal static partial class FolderIconDrawer
    {
        // List and tree rows are at most this tall; grid tiles are taller.
        internal const float MaxListRowHeight = 22f;

        // The right-hand pane starts this far or more from the window's left edge.
        internal const float ContentPaneMinOffset = 8f;

        // A two-column folder tree ends at least this far before the window's right edge.
        internal const float TwoColumnTreeMinGap = 40f;

        internal const float WindowVerticalTolerance = 48f;

        const double WindowScanInterval = 1.0;
        const string ProjectWindowTypeName = "UnityEditor.ProjectBrowser";

        static readonly HashSet<string> s_SelectedGuids = new HashSet<string>(StringComparer.Ordinal);
        static readonly HashSet<string> s_SelectedParentGuids = new HashSet<string>(StringComparer.Ordinal);
        static readonly List<EditorWindow> s_ProjectWindows = new List<EditorWindow>();
        static bool s_SelectionDirty = true;
        static bool s_SelectionInContent;
        static bool s_SelectionHasFile;
        static double s_NextWindowScan;

        internal static bool Registered { get; private set; }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeInitializing]
#else
        [InitializeOnLoadMethod]
#endif
        static void Initialize()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectWindowItemOnGUI += OnProjectWindowItemGUI;
            EditorApplication.projectChanged -= OnProjectChanged;
            EditorApplication.projectChanged += OnProjectChanged;
            Selection.selectionChanged -= OnSelectionChanged;
            Selection.selectionChanged += OnSelectionChanged;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
#endif
            Registered = true;
        }

#if UNITY_6000_5_OR_NEWER
        [Unity.Scripting.LifecycleManagement.OnCodeUnloading]
#endif
        static void Shutdown()
        {
            EditorApplication.projectWindowItemOnGUI -= OnProjectWindowItemGUI;
            EditorApplication.projectChanged -= OnProjectChanged;
            Selection.selectionChanged -= OnSelectionChanged;
#if !UNITY_6000_5_OR_NEWER
            AssemblyReloadEvents.beforeAssemblyReload -= Shutdown;
#endif
            FolderIconIndex.Clear();
            s_SelectedGuids.Clear();
            s_SelectedParentGuids.Clear();
            s_SelectionDirty = true;
            s_SelectionInContent = false;
            s_SelectionHasFile = false;
            s_ProjectWindows.Clear();
            s_NextWindowScan = 0;
            Registered = false;
        }

        static void OnProjectChanged()
        {
            FolderIconIndex.Invalidate();
        }

        static void OnSelectionChanged()
        {
            s_SelectionDirty = true;
        }

        static void OnProjectWindowItemGUI(string guid, Rect selectionRect)
        {
            Event current = Event.current;
            if (current == null || current.type != EventType.Repaint)
                return;
            if (!FolderIconsPreferences.Enabled)
                return;

            NoteSelectionInContent(guid, selectionRect);
            if (!FolderIconIndex.TryGetIcon(guid, out Texture2D icon))
                return;

            Draw(guid, selectionRect, icon);
        }

        // In the two-column layout the folder tree highlights a clicked folder, but when
        // the selection was made in the right-hand pane it highlights the folder shown
        // there instead. Seeing a selected row in the right-hand pane tells the two apart.
        static void NoteSelectionInContent(string guid, Rect selectionRect)
        {
            RefreshSelection();
            if (s_SelectionInContent || !s_SelectedGuids.Contains(guid))
                return;

            GetRowPlacement(selectionRect, out EditorWindow window, out FolderIconSurface surface, out _);
            if (surface == FolderIconSurface.Tree)
                return;

            s_SelectionInContent = true;
            if (window != null)
                window.Repaint();
        }

        static void Draw(string guid, Rect selectionRect, Texture2D icon)
        {
            GetRowPlacement(selectionRect, out EditorWindow window, out FolderIconSurface surface, out bool twoColumnTree);
            bool focused = window != null && ReferenceEquals(EditorWindow.focusedWindow, window);
            FolderIconRowState state = GetRowState(guid, twoColumnTree, focused);

            Rect iconRect = GetIconRect(selectionRect);
            bool badge = FolderIconsPreferences.Style == FolderIconStyle.Badge;
            if (badge)
            {
                // The native folder icon stays visible, so nothing is covered.
                iconRect = GetBadgeRect(iconRect, surface == FolderIconSurface.Grid);
            }
            else
            {
                EditorGUI.DrawRect(iconRect, FolderIconColors.Background(surface, state, EditorGUIUtility.isProSkin));
            }

            Color previous = GUI.color;
            GUI.color = previous * FolderIconColors.IconTint(surface, state);
            GUI.DrawTexture(iconRect, icon, ScaleMode.ScaleToFit);
            GUI.color = previous;
        }

        // List and tree badges use this share of the row-height icon.
        internal const float ListBadgeScale = 0.625f;

        // Grid badges use this share of the tile-width icon.
        internal const float GridBadgeScale = 0.45f;

        // A badge sits in the bottom-right corner of the folder icon area.
        internal static Rect GetBadgeRect(Rect iconRect, bool grid)
        {
            float size = Mathf.Round(iconRect.width * (grid ? GridBadgeScale : ListBadgeScale));
            return new Rect(iconRect.xMax - size, iconRect.yMax - size, size, size);
        }

        static void GetRowPlacement(
            Rect selectionRect, out EditorWindow window, out FolderIconSurface surface, out bool twoColumnTree)
        {
            float clipOriginX = GUIUtility.GUIToScreenPoint(Vector2.zero).x;
            window = FindProjectWindow(GUIUtility.GUIToScreenPoint(selectionRect.center));
            bool hasWindow = window != null;
            Rect windowRect = hasWindow ? window.position : default;
            surface = Classify(selectionRect, clipOriginX, windowRect, hasWindow);
            twoColumnTree = surface == FolderIconSurface.Tree && hasWindow
                && IsTwoColumnTree(selectionRect, clipOriginX, windowRect);
        }

        internal static bool IsGridTile(Rect selectionRect)
        {
            return selectionRect.height > MaxListRowHeight;
        }

        // A list or tree row gets a square of the row height at its left edge.
        // A grid tile gets a square of the tile width at its top.
        internal static Rect GetIconRect(Rect selectionRect)
        {
            float size = IsGridTile(selectionRect) ? selectionRect.width : selectionRect.height;
            return new Rect(selectionRect.x, selectionRect.y, size, size);
        }

        // The folder tree is drawn from the window's left edge. The right-hand pane of the
        // two-column layout is drawn in a clip that starts further right.
        internal static FolderIconSurface Classify(
            Rect selectionRect, float clipOriginScreenX, Rect windowRect, bool hasWindow)
        {
            if (IsGridTile(selectionRect))
                return FolderIconSurface.Grid;
            if (hasWindow && clipOriginScreenX - windowRect.x >= ContentPaneMinOffset)
                return FolderIconSurface.ContentList;
            return FolderIconSurface.Tree;
        }

        // A one-column tree spans the window, while the two-column folder tree stops well
        // before the window's right edge.
        internal static bool IsTwoColumnTree(Rect selectionRect, float clipOriginScreenX, Rect windowRect)
        {
            return clipOriginScreenX + selectionRect.xMax < windowRect.xMax - TwoColumnTreeMinGap;
        }

        static FolderIconRowState GetRowState(string guid, bool twoColumnTree, bool focused)
        {
            RefreshSelection();
            return GetRowState(
                s_SelectedGuids.Contains(guid),
                s_SelectedParentGuids.Contains(guid),
                twoColumnTree,
                focused,
                s_SelectionInContent || s_SelectionHasFile);
        }

        internal static FolderIconRowState GetRowState(
            bool selected, bool parentOfSelection, bool twoColumnTree, bool focused, bool selectionInContent)
        {
            FolderIconRowState selectedState = focused
                ? FolderIconRowState.SelectedFocused
                : FolderIconRowState.SelectedUnfocused;
            if (!twoColumnTree)
                return selected ? selectedState : FolderIconRowState.Normal;

            // A selection made in the right-hand pane leaves the folder tree highlighting the
            // folder shown there, with the tree's highlight in its unfocused color.
            if (selectionInContent)
                return parentOfSelection ? FolderIconRowState.SelectedUnfocused : FolderIconRowState.Normal;
            return selected ? selectedState : FolderIconRowState.Normal;
        }

        static void RefreshSelection()
        {
            if (!s_SelectionDirty)
                return;

            s_SelectedGuids.Clear();
            s_SelectedParentGuids.Clear();
            s_SelectionInContent = false;
            s_SelectionHasFile = false;
            // Selection.assetGUIDs can omit package root folders, so resolve the selected
            // objects by path instead.
            foreach (UnityEngine.Object selected in Selection.objects)
            {
                string path = selected != null ? AssetDatabase.GetAssetPath(selected) : null;
                if (string.IsNullOrEmpty(path))
                    continue;

                s_SelectedGuids.Add(AssetDatabase.AssetPathToGUID(path));
                // The two-column folder tree lists only folders, so a selected file was
                // chosen in the right-hand pane.
                if (!AssetDatabase.IsValidFolder(path))
                    s_SelectionHasFile = true;
                string parent = FolderIconRules.GetParentFolder(path);
                if (parent != null)
                    s_SelectedParentGuids.Add(AssetDatabase.AssetPathToGUID(parent));
            }

            s_SelectionDirty = false;
        }

        // Finds the Project window that contains a screen point. The window list is
        // rescanned at most once per interval so a miss cannot allocate on every repaint.
        static EditorWindow FindProjectWindow(Vector2 screenPoint)
        {
            EditorWindow window = FindCachedProjectWindow(screenPoint);
            if (window != null)
                return window;

            double now = EditorApplication.timeSinceStartup;
            if (now < s_NextWindowScan)
                return null;

            s_NextWindowScan = now + WindowScanInterval;
            s_ProjectWindows.Clear();
            foreach (EditorWindow candidate in Resources.FindObjectsOfTypeAll<EditorWindow>())
            {
                if (candidate != null && candidate.GetType().FullName == ProjectWindowTypeName)
                    s_ProjectWindows.Add(candidate);
            }

            return FindCachedProjectWindow(screenPoint);
        }

        static EditorWindow FindCachedProjectWindow(Vector2 screenPoint)
        {
            for (int i = 0; i < s_ProjectWindows.Count; i++)
            {
                EditorWindow window = s_ProjectWindows[i];
                if (window != null && ContainsRow(window.position, screenPoint))
                    return window;
            }

            return null;
        }

        // GUI screen points and window positions can disagree vertically by about a
        // toolbar height, so the vertical test has a tolerance.
        internal static bool ContainsRow(Rect windowRect, Vector2 screenPoint)
        {
            return screenPoint.x >= windowRect.xMin && screenPoint.x < windowRect.xMax
                && screenPoint.y >= windowRect.yMin - WindowVerticalTolerance
                && screenPoint.y < windowRect.yMax + WindowVerticalTolerance;
        }
    }
}
