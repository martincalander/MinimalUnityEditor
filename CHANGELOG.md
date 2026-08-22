# Changelog

All notable changes to this package are documented in this file.

## [Unreleased]

## [0.1.0] - 2026-08-22

### Added

- Extracted the Console, Menu Item, Package Manager, optional FMOD menu, and Editor chrome modules from `com.essentials.extensions`.
- Preserved the existing assembly names, public APIs, settings locations, persisted keys, and Unity asset GUIDs.
- Added the package-wide `ESSENTIALS_DISABLE_MINIMAL_EDITOR` compatibility symbol alongside the existing module-specific symbols.
- Retained the single package-owned Harmony binary used by Package Manager and the Extensions Preset Manager module.

### Changed

- Limited Package Manager cleanup to Unity 6 or newer, where its reflected Package Manager API exists, and no longer reports success when no Harmony target was patched.
