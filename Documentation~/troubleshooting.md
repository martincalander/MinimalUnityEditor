# Troubleshooting

## A Console Rule Does Nothing

Open **Preferences > Console** and check:

- **Enable Hidden Log Filters** is on;
- the individual rule is enabled;
- **Warnings Only** matches the message type you are testing;
- **Case Sensitive** matches the text;
- a Regex rule has no inline validation error.

The default rule list is empty. Matching rows are revisited while active rules
exist, so a new row can appear briefly before it is removed.

Regex matching stops a rule after a 50 ms timeout to keep the Editor responsive.
Simplify the expression and save the settings to enable it again.

If Console search, severity toggles, or Collapse hide rows, rescans pause to
preserve those logs. Clear the search, show all severities, and turn off
Collapse before updating existing logs.

## The Assistant Toggle Is Disabled

Minimal Editor enables that toggle only when the optional Unity AI Assistant
Console integration is detected. The setting has no effect in a project where
that integration is absent.

## A Hidden Menu Item Did Not Return

Unity's internal menu rebuild proved unsafe to call from this module, so turning
off a bundle stops future removal but does not immediately reconstruct a menu
entry that Unity already removed. Trigger a normal domain reload or reopen the
Editor, then only the currently enabled paths will be removed.

## A Menu Path Is Ignored

Custom definitions accept exact paths or a trailing `*` prefix wildcard. They
do not support arbitrary glob syntax.

Bare root paths cannot be removed safely. On macOS, use a whole-root wildcard
such as `Jobs/*` to hide the native heading as well as its commands. The heading
is kept hidden when Unity recreates it, without forcing a menu rebuild.
Services commands are never removed because Unity regenerates them. On macOS,
`Services/*` or the **Services** bundle hides only the Services heading; other
`Services/` paths are ignored. Known error-prone entries produce a warning in
Preferences. If an unexpected apply failure occurs, the menu module enters
a fail-safe state until the next domain reload.

## Menu Hiding Is Disabled on Unity 6.0

Unity 6.0 Editor releases before `6000.0.43f1` have an unsafe internal menu
contract for this module. Saved settings remain on disk, but menu removal is
blocked until the project opens in a newer Editor.

## Package Manager Changes Are Missing

The Package Manager assembly exists only on Unity 6 or newer. Its patches also
require exact internal types and methods; an affected feature remains inactive
when Unity changes that contract. The patches are applied when a Package Manager
window is open after a code load, or when one first opens or gains focus.

The duplicate filter hides an Asset Store-backed row only when the matching
registered package is read-only and resolves outside the project `Packages/`
folder. Local packages remain visible and should receive **Go To Local
Package**.

## A Project Folder Is Still Visible

Open **Preferences > Minimal Editor > Project Folders** and check both
**Hide selected folders** and the folder's own checkbox. `Assets/Plugins`,
`Assets/Gizmos`, `Assets/Settings`, and `Assets/StreamingAssets` start visible.
Use **Add folder** for other folders inside Assets.

The module requires Unity 7 with the CoreCLR Editor runtime and patches private
Project Browser methods. If a required method changes, it leaves folders visible
and reports the failure in the Console and the Preferences page. Check for
`MINIMAL_EDITOR_DISABLE_PROJECT_FOLDER_VISIBILITY` if the controls are missing.

If upgrading from the standalone Project Folder Visibility package, remove it
so only Minimal Editor provides the module. Configure local folder preferences
again under **Preferences > Minimal Editor > Project Folders**.

## Re-resolve All Packages Did Not Run

The command removes `Packages/packages-lock.json` when present and calls Unity
Package Manager resolve even when lockfiles are disabled. Check the Console for
Package Manager errors if resolution fails. Projects with lockfiles enabled
should review the regenerated file before committing it.

Run it only while the Editor is idle. If it runs while Unity is compiling or
reloading, the newer packages can be downloaded without being applied; choose
the command again once Unity has finished. The command is deprecated in favor
of **Update Git Packages** in Github Package Manager.

## A Moved Menu Still Shows Its Old Heading

Open **Preferences > Minimal Editor > Menu Item** and confirm the module and
the bundle or custom rule are enabled. The settings page lists move rules it
ignores. Spell the source heading exactly as it appears in the menu bar.

A command stays under its old heading when it is not declared with a
`MenuItem` attribute or when another command already uses the new path. The
heading is removed only once every command below it has moved.

## A Whole Module Is Missing

Check the project's scripting define symbols. The package-wide
`MINIMAL_EDITOR_DISABLE_ALL` symbol removes
every module. Module-specific symbols are listed in
[Compatibility and scripting](compatibility.md).

## A Preference Appeared in Another Project

Console filters, the short title, and status-bar choices use user-global
`EditorPrefs`. They carry across Unity projects opened by the same
operating-system user. Menu Item choices are user-global too, stored in
`MinimalEditor/MenuItemSettings.json` in the Unity preferences folder. Project
Folders uses `EditorPrefs` keyed by the project path, so its choices stay local
to this user and project.
