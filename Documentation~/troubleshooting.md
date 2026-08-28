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

Root-only paths cannot be removed safely. `Services/**` paths are ignored as
unreliable, and known error-prone entries produce a warning in Project
Settings. If an unexpected apply failure occurs, the menu module enters a
fail-safe state until the next domain reload.

## Menu Hiding Is Disabled on Unity 6.0

Unity 6.0 Editor releases before `6000.0.43f1` have an unsafe internal menu
contract for this module. Saved settings remain on disk, but menu removal is
blocked until the project opens in a newer Editor.

## Package Manager Changes Are Missing

The Package Manager assembly exists only on Unity 6 or newer. Its patches also
require exact internal types and methods; an affected feature remains inactive
when Unity changes that contract.

The duplicate filter hides an Asset Store-backed row only when the matching
registered package is read-only and resolves outside the project `Packages/`
folder. Local packages remain visible and should receive **Go To Local
Package**.

## Re-resolve All Packages Did Not Run

The command acts only when `Packages/packages-lock.json` exists. It removes that
file and calls Unity Package Manager resolve. Review the regenerated lockfile
before committing it.

## FMOD Still Has Root Menu Items

Confirm the installed FMOD package ID is
`com.fmod.fmod-for-unity-2-03`. Minimal Editor retries removal of the original
FMOD entries a limited number of times; API or menu changes in another FMOD
release can leave some originals in place.

## A Whole Module Is Missing

Check the project's scripting define symbols. The package-wide
`ESSENTIALS_DISABLE_MINIMAL_EDITOR` and `ESSENTIALS_DISABLE_ALL` symbols remove
every module. Module-specific symbols are listed in
[Compatibility and scripting](compatibility.md).

Remember that `ESSENTIALS_DISABLE_MENU_ITEM` also removes the dependent Editor
chrome assembly.

## A Preference Appeared in Another Project

Console filters, the short title, and status-bar choices use user-global
`EditorPrefs`. They carry across Unity projects opened by the same
operating-system user. Menu Item choices are different: they live in the
project-owned `ProjectSettings/MenuItemSettings.json` file.
