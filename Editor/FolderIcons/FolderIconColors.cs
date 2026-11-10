using UnityEngine;

namespace MinimalEditor.FolderIcons.Editor
{
    // Where a Project window row is drawn. The folder tree, the right-hand list, and the
    // grid use different background colors.
    internal enum FolderIconSurface
    {
        Tree,
        ContentList,
        Grid
    }

    internal enum FolderIconRowState
    {
        Normal,
        SelectedFocused,
        SelectedUnfocused
    }

    // Project window colors used to cover the native folder icon before drawing the image.
    // Unity 7 values were sampled from 7000.0.0a16 captures. Values for earlier Editors
    // follow their documented skin palette and have not been sampled.
    internal static class FolderIconColors
    {
#if UNITY_7000_0_OR_NEWER
        static readonly Color DarkTree = new Color32(0x2D, 0x2D, 0x2D, 0xFF);
        static readonly Color DarkContent = new Color32(0x23, 0x23, 0x23, 0xFF);
        static readonly Color DarkSelectedFocused = new Color32(0x31, 0x51, 0x6C, 0xFF);
        static readonly Color DarkSelectedUnfocused = new Color32(0x3C, 0x3C, 0x3C, 0xFF);
        static readonly Color LightTree = new Color32(0xCB, 0xCB, 0xCB, 0xFF);
        static readonly Color LightContent = new Color32(0xBE, 0xBE, 0xBE, 0xFF);
        static readonly Color LightSelectedFocused = new Color32(0x3A, 0x72, 0xB0, 0xFF);
        static readonly Color LightSelectedUnfocused = new Color32(0xAE, 0xAE, 0xAE, 0xFF);
#else
        static readonly Color DarkTree = new Color32(0x38, 0x38, 0x38, 0xFF);
        static readonly Color DarkContent = new Color32(0x33, 0x33, 0x33, 0xFF);
        static readonly Color DarkSelectedFocused = new Color32(0x2C, 0x5D, 0x87, 0xFF);
        static readonly Color DarkSelectedUnfocused = new Color32(0x4D, 0x4D, 0x4D, 0xFF);
        static readonly Color LightTree = new Color32(0xC8, 0xC8, 0xC8, 0xFF);
        static readonly Color LightContent = new Color32(0xBE, 0xBE, 0xBE, 0xFF);
        static readonly Color LightSelectedFocused = new Color32(0x3A, 0x72, 0xB0, 0xFF);
        static readonly Color LightSelectedUnfocused = new Color32(0xAE, 0xAE, 0xAE, 0xFF);
#endif

        // Unity tints a selected grid icon instead of filling its background.
        static readonly Color GridSelectedTint = new Color(0.85f, 0.9f, 1f, 1f);

        internal static Color Background(FolderIconSurface surface, FolderIconRowState state, bool dark)
        {
            if (surface != FolderIconSurface.Grid)
            {
                if (state == FolderIconRowState.SelectedFocused)
                    return dark ? DarkSelectedFocused : LightSelectedFocused;
                if (state == FolderIconRowState.SelectedUnfocused)
                    return dark ? DarkSelectedUnfocused : LightSelectedUnfocused;
            }

            if (surface == FolderIconSurface.Tree)
                return dark ? DarkTree : LightTree;
            return dark ? DarkContent : LightContent;
        }

        internal static Color IconTint(FolderIconSurface surface, FolderIconRowState state)
        {
            return surface == FolderIconSurface.Grid && state != FolderIconRowState.Normal
                ? GridSelectedTint
                : Color.white;
        }
    }
}
