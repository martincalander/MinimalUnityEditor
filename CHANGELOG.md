# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Added

- Integrated Project Folder Visibility into Minimal Editor on Unity 7 CoreCLR,
  preserving its assembly identities, asset GUIDs, and local project preferences.
- Added built-in visibility toggles for `Assets/Plugins`, `Assets/Gizmos`, and
  `Assets/Settings`, alongside Addressables data, StreamingAssets, and custom
  folders. The three new built-ins start visible.
- Added **Preferences > Minimal Editor > Project Folders** and moved the menu
  toggle under **Tools > Minimal Editor > Project Folders**.

### Changed

- Project Folders uses the existing Minimal Editor Harmony binary and follows
  the package-wide disable symbols, with its own
  `ESSENTIALS_DISABLE_PROJECT_FOLDER_VISIBILITY` opt-out.

- Adopted guarded code lifecycle callbacks with explicit event and Harmony
  cleanup, idempotent initialization, and immediate logger rebinding when
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
- Moved the repository to Martin Calander's personal GitHub account and renamed
  the package from `com.essentials.minimaleditor` to
  `com.martincalander.minimaleditor`, while preserving its assembly, namespace,
  settings, and Unity asset compatibility identities.
- Renamed the GitHub repository from `com.martincalander.minimaleditor` to
  `MinimalUnityEditor` without changing the package ID or installation path.
- Vendored a uniquely named, package-owned Harmony binary for the Package Manager patches with Editor-only, explicit-reference importer settings.
- Updated the Package Manager assembly to reference `Essentials.MinimalEditor.Harmony.dll` and removed the shared `com.essentials.harmony` dependency.
- Added focused user, compatibility, and troubleshooting guides, including the
  default menu behavior, menu-restoration caveat, and exact Unity 6 Package
  Manager duplicate rules.

## [0.1.0] - 2026-08-22

### Added

- Extracted the Console, Menu Item, Package Manager, optional FMOD menu, and Editor chrome modules from `com.essentials.extensions`.
- Preserved the existing assembly names, public APIs, settings locations, persisted keys, and Unity asset GUIDs.
- Added the package-wide `ESSENTIALS_DISABLE_MINIMAL_EDITOR` compatibility symbol alongside the existing module-specific symbols.
- Retained the Harmony integration used by the Package Manager module.

### Changed

- Limited Package Manager cleanup to Unity 6 or newer, where its reflected Package Manager API exists, and no longer reports success when no Harmony target was patched.
