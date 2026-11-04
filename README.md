# Magnum Opus

A screen reader accessibility mod for [Opus Magnum](https://store.steampowered.com/app/558990/Opus_Magnum/)
(community-edition Steam branch). It speaks through your screen reader (NVDA, JAWS and others through
Prism, SAPI otherwise) and makes the game's screens keyboard-operable.

## Status

Covered: the title screen, the campaign map, letters, cutscenes, puzzle info and solutions, dialogs, the
pause menu, Options (real dropdowns) and Controls. Screens without a model yet are announced by name,
with their text and text buttons. The solution editor (the puzzles themselves) is next.

The mod keeps working through game updates: it matches a new game build against the one it was made for
and fetches the modding community's names for the new build in the background, announcing each step.

## Installing

1. Switch Opus Magnum to the **community-edition** branch: the game's Steam Properties, Game Versions and
   Betas.
2. Run `MagnumOpusInstaller.exe` from the latest release, or extract the release zip into the game folder.

The player manual is in `docs_src` (published with each release as `MagnumOpusDocs`).

## Building

See `CLAUDE.md`: the game must be installed (the module compiles against a reference generated from it),
then `dotnet build MagnumOpus.slnx`.

## Credits

- Name mappings: [QuintessentialOM/om_mappings](https://github.com/QuintessentialOM/om_mappings), the
  Opus Magnum modding community.
- Architecture from GuildrunAccess, Echopunks and SpeechChem; installer adapted from the Non-Visual
  Calculus installer by Rashad Naqeeb (MIT).
- [Harmony](https://github.com/pardeike/Harmony) (MIT), [Mono.Cecil](https://github.com/jbevain/cecil)
  (MIT), [Prism](https://github.com/ethindp/prism) (MPL-2.0), de4dot (GPLv3, analysis only, not shipped).
