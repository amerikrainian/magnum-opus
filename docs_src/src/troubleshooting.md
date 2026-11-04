# Troubleshooting

## Nothing is spoken

- Make sure the game is on the **community-edition** branch (see Before starting).
- Check that `Lightning.exe.config` is in the game folder; the installer's Repair puts it back.
- The mod writes a log at `%LOCALAPPDATA%\MagnumOpus\magnumopus.log` (a fresh file every launch).
  Send it along with any report.

## Speech goes to the wrong place

The mod speaks through your running screen reader, falling back to SAPI, then the clipboard. To
choose, set `"speech.output"` in `%LOCALAPPDATA%\MagnumOpus\settings.json` to `"prism"`,
`"sapi"` or `"clipboard"` (or `"auto"`, the default).

## The keyboard does nothing in the game

Ctrl+Shift+N toggles the mod's keyboard navigation; with it off, the game gets every key. The mod
menu (Ctrl+Shift+M) has a setting for whether navigation is on at launch.

## Removing the mod

Use the installer's Uninstall, or delete `Lightning.exe.config` from the game folder (the game
then runs without the mod; the other files are inert).
