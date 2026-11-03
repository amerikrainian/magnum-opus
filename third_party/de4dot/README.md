# de4dot 3.1.41592.3405 (binary distribution)

`de4dot.zip` — the .NET deobfuscator, version 3.1.41592.3405, licensed **GPLv3**.
Source code: https://github.com/de4dot/de4dot (the license text ships inside the zip).

SHA256: `C726CBD18B894CA63B7F6A565C6C86EF512B96E68119C6502CDF64A51F6A1C78`

Used by `tools/prepare-game.ps1` for ANALYSIS only: it runs over `game/Lightning.named.exe`
(the game renamed into human names from the community mappings) to decrypt Eazfuscator's
strings for the decompile. It renames only names that fail its identifier pattern, so the
human names survive. The module never compiles against de4dot output (the compile reference
is `game/Lightning.named.ref.exe`, built by `tools/NameTool refgen`), so the version is not
load-bearing here the way it was for Echopunks.

A standalone developer tool: nothing from it ships with the mod, and it never touches game
files outside the local `game/` analysis workspace.
