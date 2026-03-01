using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Toolbars;
using UnityEngine;

internal static class DefaultPlayerIconToolbarElement
{
    const string kElementPath = "Editor/Default Icon";
    const double kFallbackPollIntervalSeconds = 15d;

    static int s_LastIconInstanceId;
    static double s_LastFallbackPollTime;

    [MainToolbarElement(kElementPath, defaultDockPosition = MainToolbarDockPosition.Right)]
    public static MainToolbarElement CreateDefaultPlayerIconElement()
    {
        Texture2D defaultIcon = GetCurrentDefaultIconSafe();
        string tooltip = defaultIcon != null
            ? "Project Settings > Player default icon for the active build target."
            : "No default Player icon is assigned for the active build target.";

        var content = new MainToolbarContent(defaultIcon, tooltip);
        return new MainToolbarLabel(content);
    }

    static Texture2D GetCurrentDefaultIconSafe()
    {
        try
        {
            return GetCurrentDefaultIcon();
        }
        catch
        {
            // Do not surface toolbar callback failures from Unity internals.
            return null;
        }
    }

    static Texture2D GetCurrentDefaultIcon()
    {
        BuildTargetGroup buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
        Texture2D icon = GetIconForBuildTargetGroup(buildTargetGroup);
        if (icon != null)
            return icon;

        icon = GetIconForBuildTargetGroup(BuildTargetGroup.Unknown);
        if (icon != null)
            return icon;

        foreach (BuildTargetGroup targetGroup in (BuildTargetGroup[])Enum.GetValues(typeof(BuildTargetGroup)))
        {
            if (targetGroup == BuildTargetGroup.Unknown || targetGroup == buildTargetGroup)
                continue;

            icon = GetIconForBuildTargetGroup(targetGroup);
            if (icon != null)
                return icon;
        }

        return null;
    }

    static Texture2D GetIconForBuildTargetGroup(BuildTargetGroup buildTargetGroup)
    {
#if UNITY_2022_1_OR_NEWER
        try
        {
            var namedBuildTarget = UnityEditor.Build.NamedBuildTarget.FromBuildTargetGroup(buildTargetGroup);
            Texture2D icon = FirstAssignedIcon(PlayerSettings.GetIcons(namedBuildTarget, IconKind.Any));
            if (icon != null)
                return icon;

            icon = FirstAssignedIcon(PlayerSettings.GetIcons(namedBuildTarget, IconKind.Application));
            if (icon != null)
                return icon;
        }
        catch
        {
            // Some build target groups are not valid for icon queries.
        }
#endif

#pragma warning disable 618
        try
        {
            Texture2D legacyAnyIcon = FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup, IconKind.Any));
            if (legacyAnyIcon != null)
                return legacyAnyIcon;

            Texture2D legacyApplicationIcon = FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup, IconKind.Application));
            if (legacyApplicationIcon != null)
                return legacyApplicationIcon;

            return FirstAssignedIcon(PlayerSettings.GetIconsForTargetGroup(buildTargetGroup));
        }
        catch
        {
            return null;
        }
#pragma warning restore 618
    }

    static Texture2D FirstAssignedIcon(Texture2D[] icons)
    {
        if (icons == null)
            return null;

        foreach (Texture2D icon in icons)
        {
            if (icon != null)
                return icon;
        }

        return null;
    }

    internal static void RequestRefresh()
    {
        MainToolbar.Refresh(kElementPath);
    }

    internal static void NotifyBuildTargetChanged()
    {
        RefreshIfIconChanged();
        RequestRefresh();
    }

    static void OnProjectChanged()
    {
        RefreshIfIconChanged();
    }

    static void PollFallback()
    {
        if (EditorApplication.timeSinceStartup - s_LastFallbackPollTime < kFallbackPollIntervalSeconds)
            return;

        s_LastFallbackPollTime = EditorApplication.timeSinceStartup;
        RefreshIfIconChanged();
    }

    static void RefreshIfIconChanged()
    {
        int iconInstanceId = GetCurrentIconInstanceId();
        if (iconInstanceId == s_LastIconInstanceId) return;

        s_LastIconInstanceId = iconInstanceId;
        RequestRefresh();
    }

    static int GetCurrentIconInstanceId()
    {
        Texture2D icon = GetCurrentDefaultIconSafe();
        return icon != null ? icon.GetInstanceID() : 0;
    }

    static DefaultPlayerIconToolbarElement()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        EditorApplication.update += PollFallback;
        s_LastIconInstanceId = GetCurrentIconInstanceId();
    }
}

internal sealed class DefaultPlayerIconBuildTargetChanged : IActiveBuildTargetChanged
{
    public int callbackOrder => 0;

    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        DefaultPlayerIconToolbarElement.NotifyBuildTargetChanged();
    }
}
