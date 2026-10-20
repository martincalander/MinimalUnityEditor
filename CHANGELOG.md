# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Fixed

- Keep menu hiding active when a quit request is canceled, and tolerate missing
  or null bundle entries in saved menu settings.
- Keep ordinary Console logs and errors when Warnings Only is enabled, bound
  regex matching time, and preserve Console rows when a rescan cannot read them.
- Avoid logger wrapper cycles when another extension replaces the log handler.
- Restore hidden status-bar extras after Unity resets or recreates the bar.
- Keep Package Manager entries visible when their package location is unknown.
- Keep original FMOD commands when their replacement API cannot be called.

### Added

- On macOS, whole-root menu rules such as `Jobs/*` hide the menu-bar heading
  without hovering and keep it hidden when Unity recreates it. Native visibility
  is restored when the rule is disabled, without forcing Unity to rebuild menus.

- Integrated Project Folder Visibility into Minimal Editor on Unity 7 CoreCLR,
  preserving its asset GUIDs.
- Added built-in visibility toggles for `Assets/Plugins`, `Assets/Gizmos`, and
  `Assets/Settings`, alongside Addressables data, StreamingAssets, and custom
  folders. The three new built-ins start visible.
- Added **Preferences > Minimal Editor > Project Folders** and moved the menu
  toggle under **Tools > Minimal Editor > Project Folders**.

### Changed

- Standardized assembly names, namespaces, preference keys, settings pages,
  patch identifiers, and asset filenames on Minimal Editor. Existing asset
  GUIDs are preserved. Consumers must update assembly references and configure
  earlier local preferences again.
- Use `MINIMAL_EDITOR_DISABLE_ALL` to disable the package, with
  `MINIMAL_EDITOR_DISABLE_*` symbols for individual modules.
- Project Folders uses the existing Minimal Editor Harmony binary and follows
  the package-wide disable symbol, with its own
  `MINIMAL_EDITOR_DISABLE_PROJECT_FOLDER_VISIBILITY` opt-out.

- Adopted guarded code lifecycle callbacks with explicit event and Harmony
  cleanup, idempotent initialization, and logger lifecycle checks when
  entering Play or Edit Mode without a domain reload.
- Added regression coverage for repeated Play sessions with domain reload
  disabled and scene reload either enabled or disabled.

- Added a Unity 7 CoreCLR build of Harmony for the Package Manager integration,
  while retaining the Mono build for earlier Editors.
- Preserved Editor services across Play Mode transitions with explicit static
  lifecycle policies and updated loaded-assembly discovery for newer Editors.
- Updated Package Manager compatibility checks for Unity 7's imported-assets
  collection and retained patch failure details for troubleshooting.
- Restored immediate Package Manager list refresh on older Unity 6 Editors by
  supporting their legacy rebuild entry point.
- Restored short window titles on Unity 2021.3 and 2022.3 by binding their
  internal title callback without referencing its inaccessible descriptor type.
- Guarded the Console call-stack hiding attribute for Unity 2022.2 and newer,
  retaining the existing stack-trace cleanup on Unity 2021.3.
- Saved both Harmony importers in the older plugin format so Unity 2021.3 and
  2022.3 can read the Editor-only platform settings.
- Moved the repository to Martin Calander's personal GitHub account and adopted
  the package ID `com.martincalander.minimaleditor`.
- Renamed the GitHub repository from `com.martincalander.minimaleditor` to
  `MinimalUnityEditor` without changing the package ID or installation path.
- Vendored a uniquely named, package-owned Harmony binary for the Package Manager patches with Editor-only, explicit-reference importer settings.
- Updated the Package Manager assembly to reference `MinimalEditor.Harmony.dll`
  and removed the shared Harmony package dependency.
- Added focused user, compatibility, and troubleshooting guides, including the
  default menu behavior, menu-restoration caveat, and exact Unity 6 Package
  Manager duplicate rules.

## [0.1.0] - 2026-08-22

### Added

- Extracted the Console, Menu Item, Package Manager, optional FMOD menu, and
  Editor chrome modules into a standalone package.
- Included public editor APIs, settings, and stable Unity asset GUIDs.
- Added package-wide and module-specific compile opt-outs.
- Retained the Harmony integration used by the Package Manager module.

### Changed

- Limited Package Manager cleanup to Unity 6 or newer, where its reflected Package Manager API exists, and no longer reports success when no Harmony target was patched.
