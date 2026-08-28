# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

### Changed

- Moved the repository to Martin Calander's personal GitHub account and renamed the package from `com.essentials.minimaleditor` to `com.martincalander.minimaleditor`, while preserving its assembly, namespace, settings, and Unity asset compatibility identities.
- Vendored a uniquely named, package-owned Harmony binary for the Package Manager patches with Editor-only, explicit-reference importer settings.
- Updated the Package Manager assembly to reference `Essentials.MinimalEditor.Harmony.dll` and removed the shared `com.essentials.harmony` dependency.

## [0.1.0] - 2026-08-22

### Added

- Extracted the Console, Menu Item, Package Manager, optional FMOD menu, and Editor chrome modules from `com.essentials.extensions`.
- Preserved the existing assembly names, public APIs, settings locations, persisted keys, and Unity asset GUIDs.
- Added the package-wide `ESSENTIALS_DISABLE_MINIMAL_EDITOR` compatibility symbol alongside the existing module-specific symbols.
- Retained the Harmony integration used by the Package Manager module.

### Changed

- Limited Package Manager cleanup to Unity 6 or newer, where its reflected Package Manager API exists, and no longer reports success when no Harmony target was patched.
