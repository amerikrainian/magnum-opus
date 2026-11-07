# Magnum Opus — Accessibility Mod for Opus Magnum

Screen-reader accessibility mod for blind players of Opus Magnum (Zachtronics, 2017), community-edition
Steam branch. Speaks screens and makes them keyboard-operable through Prism. Built on the GuildrunAccess
architecture (Contracts / Core / host / module, the key-graph UI) with the Zachtronics machinery of
Echopunks (`../exaaccess`) and SpeechChem (`../speechchem`): AppDomainManager injection into the stock
exe, a byte-loaded hot-reloadable module, a load-time name remap. Speech is the sole interface: a silent
failure is invisible to the player, so every catch logs, and nothing caches game state. The investigation
behind the decisions is `docs/investigation.md`.

## 1. Game facts (verified live 2026-11-03)
- **Engine**: Zachtronics' own SDL2 + D3D11/OpenGL engine, NOT Unity. `Lightning.exe` is one
  **.NET Framework 4.x, x64**, IL-only assembly (assembly name `Lightning`, 1.0.0.0); references only
  mscorlib/System/System.Core/System.IO.Compression.FileSystem and Steamworks.NET.
- **Install**: `C:\Program Files (x86)\Steam\steamapps\common\Opus Magnum`, Steam appid **558990**.
  `GameLogic.GameInit` calls `SteamAPI.RestartAppIfNecessary`, so `steam_appid.txt` (558990) sits in the
  game folder (the host deploy writes it; Bootstrap rewrites it as a fallback).
- **Branch**: the mod targets **community-edition** (user decision 2026-11-03). The public branch is the
  same codebase compiled separately (CE has a few more editor methods) and has no community mapping.
  Steam keeps the branch in `steamapps\appmanifest_558990.acf` (`"BetaKey" "community-edition"`); the
  host logs it at boot and speaks a notice on any other branch (`HostNotice.WrongBranch`); the installer
  warns too (`installer/src/core/detect.rs` `steam_branch`).
- **Obfuscation**: Eazfuscator.NET 3.3.149-3.4. 168 of ~1270 types keep real names (`GameLogic`, `Sim`,
  `Part`, the screens); every member is `#=q...`, and **every build reshuffles every obfuscated name**
  (consecutive CE builds share essentially none). Parameter names are obfuscated too: Harmony patch
  methods bind game parameters **by position** (`__0`, `__1`), never by name (`isEnabled` failed to bind:
  "Parameter not found"). 217 member-less enums carry no mapping at all (e.g. the SDL scancode enum,
  `GEnum138` in the decompile): never reference one from module code (no stable name across builds;
  `Input/SdlKeyboard` declares its own P/Invoke for that reason). Strings are encrypted (de4dot decrypts
  them for the decompile).
- **Main loop**: `AppInit.Main` = `new GameLogic(); GameInit(); while (true) GameLoop();`. `GameLoop` is
  the per-frame tick (SDL event pump, screen updates, end-of-frame input reset). Ticks start once loading
  finishes (about 10 s); there is NO hidden boot gate. The visible gate is `TitleScreen` ("Click to
  Continue": a left click or Escape only).
- **Screens**: `GameLogic.screenStack` (`global::Stack<IScreen>`, its `stack` list bottom to top);
  `GetCurrentScreen()` = top, `PushScreen(screen[, out, in])`, `PopScreen(...)`, `GetScreenTransition()`
  (`IsEmpty()` when no fade runs). **The game updates the screens below the top one every frame too,
  with input switched off** (`InputManager.isKeyInputEnabled` / `isMouseButtonInputEnabled` false): any
  synthesized input must respect those flags, or a lower screen acts on it (a forced Escape once opened
  the options menu under a letter, which the letter's close then popped instead of itself).
- **Input**: everything reads keys through the static `InputManager` (global namespace; `IsKeyPressed`,
  `IsKeyReleased`, `IsKeyHeld`, `WhileKeyHeld`, `IsEnterPressed`, `IsDeletePressed`), which reads the
  window's key sets (`Window` fields: down now, down last frame, held with repeat), SDL keycodes. Nothing
  else reads those sets, so prefixing the four queries blanks a key for the whole game. Typed characters:
  one per frame in a window field, **cleared at the end of `GameLoop`**, before our prefix could read it.
  Mouse: `IsClickPressed` etc.
- **Immediate-mode UI**: no widget tree. Every button is `ButtonDrawingLogic.RenderAndCheckIfPressed`
  (hover + left click); labeled ones come from `UIUtils.TextButton` / `FancyTextButton` /
  `SolutionButton` / `PuzzleButton`; `UIUtils.CloseButton` also answers Escape. All text is
  `TextureRenderer.RenderText` -> `RenderTextUnsafe`. **`RenderText` is a three-line wrapper the JIT
  inlines into its callers: a patch on it never runs** (patch `RenderTextUnsafe`). The same caution holds
  for any tiny game method: patch the non-trivial callee.
- **Text markup**: `*bold*`, `_italic_`, backslash escapes (`Utils.EscapeFormatting`), and `●` break
  markers in Japanese: `Game/GameText.Clean`. Localization: `LocString` (`GetTranslated()`, implicit to
  string), `Translations.Translate(english)` for UI strings (`Content/strings.csv` has only a few hundred
  rows: "Options" yes, "Journal" no), `Translations.currentLanguage`, `Translations.countryCodes`.
- **Settings**: `GameLogic.instance.settingsData` (type `global::Settings`), each a `SettingsItem<T>`
  (`Get()`, `Set(value)`, persisted to the save folder's config.cfg on Set).

## 2. Analysis workspace: `game/` (gitignored — NEVER commit or ship any of it)
`tools\prepare-game.ps1 [-Decompile] [-Force]` rebuilds it (warns when the branch is not community-edition):
- `game\Lightning.orig.exe` — byte copy of the installed exe; `game\builds\<mvid>\` parks the previous build
  on an update (the matcher check and body comparisons need it).
- `game\Lightning.named.ref.exe` — **the module's compile reference**: the game renamed into human names
  and made public (`NameTool refgen`; the module build regenerates it when stale).
- `game\Lightning.named.exe` / `Lightning.named-deob.exe` — the same names unpublicized, then de4dot over
  it (strings decrypted, human names kept: de4dot only renames names failing its identifier pattern).
- `game\decompiled\` — ilspycmd project of the named-deob exe: read this. CAUTION: de4dot still renames
  short names it dislikes (`Index2.X` reads `int_0` there); the compile reference is the authority.
- `game\fingerprint.tsv` — the shipped fingerprint of the current build (the module build writes it).
- `game\builds\public-26115378\` — the public build of 2026-10-21 (MVID 8d4aa6bd), kept for matcher tests.

## 3. Names: from obfuscated to human and back (`src/MagnumOpus.Names`, `tools/NameTool`)
The community's **om_mappings** (QuintessentialOM; use approved by the user) has three layers:
obfuscated -> intermediary per build (`intermediary/mappings_ce_<date>_<mvid>.json`, made by their
`matcher` from the previous build), intermediary names (`class_N`, `method_N`, `field_N`, `param_N`,
stable across builds), and intermediary -> human (`named/*.txt`, one flat table; `enum_N.V,Name` re-adds
stripped enum constants). Only CE files are imported (`previous`-branch files share the intermediary space
and would override CE names). `mappings/` holds what the build and the release use: `named/` (pinned:
the module's source names), `obf/<mvid>.tsv` (the CURRENT CE build only, our compact TSV), `SOURCE.txt`
(the om_mappings commit). `NameTool import <om_mappings>` refreshes it.

ONE renamer serves every source of names (`Renamer`): candidates per type and member, uniqueness kept
per scope (types per namespace / declaring type, fields by name, methods by name + signature),
collisions resolved symmetrically (every member of a colliding group falls back: human -> intermediary
-> shipping), the module's own member references to renamed definitions updated too.
`MappingNameSource` feeds it from the community layers, `Matcher` from a fingerprint (section 4).
- **Compile time**: `refgen` renames the orig exe, re-adds enum constants, publicizes -> the reference.
- **Run time**: the host renames a Cecil copy of the running exe IN MEMORY with the same code
  (`GameNames.TableFromMappings`, ~650 ms), inverts it (`NameTable`: human -> shipping, and back for the
  dev tools), and `Remapper` rewrites the module's game references before `Assembly.Load` (members found
  through method bodies too: Cecil makes a fresh reference per call site on generic instances). Assembly
  identity needs no touch ("Lightning" in both). What the build lacks is logged and listed in
  `ModHostImpl.Unresolved`; the code that uses it throws when reached, the rest runs.
- Module source therefore reads like plain C#: `GameLogic.instance.GetCurrentScreen()`,
  `g.selectedSolution.HasValue()`, private members included (the module declares
  `[IgnoresAccessChecksTo("Lightning")]`, honoured by CLR 4; `AccessChecks.cs`).
- **Rules**: target game methods for Harmony with `Game/Expr.MethodOf(() => ...)` (the ldtoken is
  remapped); NEVER by a name string (`typeof(X).GetMethod("Human")` fails on the shipping build) and
  never `typeof(GameType)` inside attributes. At runtime `GetType().Name` is the SHIPPING name: use
  `GameBinding.HumanName` / `GameScreens.Name`.

## 4. Surviving game updates (`Host/NameSources`, `Host/MappingDownloader`, `Matcher`)
Names for the running build, best first: **bundled** (`MagnumOpus\mappings\obf\<mvid>.tsv`), **downloaded**
(`%LOCALAPPDATA%\MagnumOpus\mappings\<mvid>.tsv`), **matched** (the running exe bound to the shipped
`MagnumOpus\mappings\fingerprint.tsv`). For a build neither bundled nor downloaded the host starts a
background fetch from om_mappings (GitHub contents API, the file carrying the MVID, parsed with the
framework's own `JavaScriptSerializer`: no JSON library ships; the tool uses System.Text.Json), caches it,
and on arrival rebinds on the main thread and hot-reloads the module onto the mapped names. Every step is
SPOKEN, localized, by the module (user rule: important enough for speech, not just the log):
`HostNotice` NamesMatched / MappingDownloading / MappingNotPublished / MappingDownloadFailed /
MappingUpdated, each followed by the unresolved count when there is one. `"mappings.download": "false"`
in settings.json turns the fetch off. Verified live by hiding the bundled map: matched in 815 ms, fetched
and cached in about a second, rebound on the first tick.
- **Fingerprint** = the known build in human names: types (kind, base, arity, nesting), fields (type),
  methods (signature, the hashed set of EXTERNAL methods the body calls, IL size). Derived metadata, no code.
- **Matcher**: real names first; rounds of structural signatures written with the current bindings
  (unbound game types "?"), unique on both sides binds; equal groups pair in metadata order; looser
  signatures; nested types only within their bound parents. Members: elementwise, real names, unique keys,
  order within equal groups, then an LCS alignment (survives inserted members). On the public build
  against the CE fingerprint: types 1268/1270, fields 98.7%, methods 98.5%, 93% of matched pairs with
  identical bodies, and the two members verified live there (`GameLoop`, `GetCurrentScreen`) bound right;
  against the CE mapping on its own build it agrees on every item. Member positions barely move between
  CE builds (99.6-100% per update, measured from the community files).
- **Developer side**: `tools\update-game.ps1` after Steam updates the game: pulls and imports om_mappings,
  refreshes the workspace (parks the old build), runs `NameTool check` with the previous fingerprint
  (agreement with a new community mapping, bodies against the previous build, the module's unresolved
  references), rebuilds and tests. Then fix what moved, commit `mappings/`, release.

## 5. Tools
- `tools\prepare-game.ps1` (section 2), `tools\update-game.ps1` (section 4).
- `tools/NameTool` (`dotnet run --project tools/NameTool -- <verb>`): `import`, `refgen`, `fingerprint`,
  `check --exe <new> --fingerprint <fp> [--old <known exe>] [--module <dll>]`, `mvid <exe>`.
- `tools/python/dev.py` (`uv run python tools/python/dev.py <cmd>`): `launch` (shows the window WITHOUT
  activating it), `kill`, `key <chord>...` (injected through the dev server: never OS input), `get
  <route>` (routes may omit the leading slash; Git Bash rewrites "/nav"), `post <route> [body]`,
  `eval "<C#>"`, `speech [--since N]`, `screenshot`, `click [x y]` (posted, no focus change).

## 6. Build & deploy
```
dotnet build MagnumOpus.slnx      # or .\build.ps1 [-ModuleOnly]
```
The game is a BUILD PREREQUISITE for the module (it compiles against the reference generated from the
installed exe; a fresh clone needs the game installed on the community-edition branch). A Debug build
deploys: the host (`MagnumOpus.dll`, `MagnumOpus.Contracts.dll`, `MagnumOpus.Names.dll`, `Mono.Cecil.dll`,
`0Harmony.dll`, `Mono.CSharp.dll`, `prism.dll`, `Lightning.exe.config`, `steam_appid.txt`,
`MagnumOpus\mappings\named|obf`) into the game folder (locked while the game runs: close it for host
changes; the copy warns and continues), and the module (`MagnumOpus\MagnumOpus.Core.dll`,
`MagnumOpus.Module.dll`, `lang\`, `mappings\fingerprint.tsv`), which deploys with the game running:
then `POST /reload` (no key: F6 dropped 2026-11-07, user request). Tests: `dotnet test src/MagnumOpus.Tests` (Names, Core, Contracts; the two
real-exe tests skip without the workspace). `MAGNUMOPUS_WRITE_LANG_TEMPLATE=1` regenerates `lang/en.txt`
(the test fails when the template lags the strings table). Override the install with `MAGNUMOPUS_DIR`,
`-p:GameDir=`, or a gitignored `Directory.Build.local.props`. The host's AssemblyVersion is pinned at
1.0.0.0 (the config binds it by full name); releases move FileVersion (`Directory.Build.props` Version).

## 7. Releases (the Non-Visual Calculus / Harkest Dungeon / Echopunks tooling, MIT attribution kept)
Bump `Version` in `Directory.Build.props`, add a `## Vx.y.z` section to `CHANGELOG.md`, commit, tag, then:
- `build_release.ps1` -> `releases\MagnumOpus-vX.Y.Z.zip`, root = game folder: the host files above minus
  Mono.CSharp, `MagnumOpus\` (Core, Module, lang, mappings named + current obf + fingerprint),
  `MagnumOpusDocs\` (the mdbook manual from `docs_src`). Refuses a build with dev tooling.
- `build-installer.ps1` -> `releases\MagnumOpusInstaller.exe` (Rust + wxWidgets, `installer\`: game
  detection, the GitHub releases of `amerikrainian/magnum-opus`, sha256 check, install manifest
  `MagnumOpus\install.json`, backups, repair, uninstall, the branch warning). `test-installer.ps1` = cargo
  test; `tools\installer-e2e.ps1` = install / uninstall against a throwaway folder and a local feed (build
  the CLI first: `cargo build --release --example cli` in `installer\`). The first e2e run of a session
  can fail while the local feed starts; rerun.
- `create-release.ps1 vX.Y.Z` = the gh release with the zip, the installer and the CHANGELOG section.
- `.github/workflows/build-docs.yml` publishes `docs_src` to GitHub Pages on a push touching it.
Every game update with a new community mapping deserves a release (it bundles the new obf map and a new
fingerprint), though the mod survives without one (section 4).

## 8. Logs
`%LOCALAPPDATA%\MagnumOpus\magnumopus.log`, a fresh file per launch (the one path to give testers), also
served by `GET /log?since=N&grep=S`. Settings: `%LOCALAPPDATA%\MagnumOpus\settings.json` (flat
`{"key": "value"}`: `speech.output` auto|prism|sapi|clipboard, `speech.trace`, `mappings.download`,
`focus_mode_on_launch`, `speak_positions`).

## 9. Dev server (DEBUG builds; on by default, `MAGNUMOPUS_NO_DEV=1` disables, `MAGNUMOPUS_DEV_PORT` moves it from 8776)
`GET /health`, `POST /say`, `GET /speech?since=N`, `GET /log?since=N&grep=S`, `GET /screen` (the game's
stack by human name), `GET /gui` (reflection dump of the top screen with human field names), `GET
/screenshot` (PrintWindow: no focus change, works occluded), `POST /eval` (Mono.CSharp REPL on the main
thread; the game's members have SHIPPING names there, so use `N`: `N.Game`, `N.Top`, `N.Screens()`,
`N.Get(obj, "field")`, `N.Set`, `N.Call(obj, "Method", args)`, `N.Static`, `N.Type`, `N.Dump`, `N.Name`;
generic game types work), `POST /input <action>` (`ui.down`, `mod.help`...; also `dev.key:<chord>` and
`dev.type:<text>`), `GET /nav`, `GET /actions`, `POST /reload`, `GET /module` (generation, dll times,
name source, unresolved references). Main-thread routes run in the GameLoop prefix.
- **Driving the game never touches window focus** (user rule 2026-11-03: the user works on the same
  machine). `dev.key:` injects a chord into the module's keyboard snapshot for one frame; where the
  focused screen would pass that key to the game, the module forwards it (`GameKeys.Forced`), since the
  game never sees an injection. Typing into a GAME text field needs real OS input: ask the user.
- **Live tests navigate and read** (user rule): never activate something that changes the player's
  settings or saves (Reset, Reverify, language, Exit, delete) unless the test needs it and the value is
  restored at once; check `/nav`'s focus before every Enter.

## 10. Architecture (the Guildrun split, on .NET Framework)
- **`MagnumOpus.Contracts`** (netstandard2.0, permanent): `IModHost`, `IModModule`, `IDevDriver`,
  `SpeechPipeline`, `ISpeechBackend`, `ModSettings` / `ISettingsStore`, `TextFilter`. From Guildrun.
- **`MagnumOpus.Names`** (netstandard2.0, permanent; Cecil): the name pipeline (sections 3-4), shared by
  the host, NameTool and the tests.
- **`MagnumOpus`** (net48 x64, permanent HOST; a change needs a game restart, keep it minimal):
  `Bootstrap` (AppDomainManager; waits for the CLR's own `AssemblyLoad` of Lightning, since loading the
  exe ourselves with `LoadFrom` (the Echopunks pattern) made the CLR's own load fail with 0x8000FFFF and
  killed the game before its window), `Host/GameBinding` (names, branch, human-name reflection),
  `Host/GameHooks` (GameInit postfix, GameLoop prefix), `Host/Runtime` (tick: posted actions, dev pump,
  module), `Host/ModHostImpl` (IModHost, `Notices`, `Unresolved`), `Modularity/ModuleLoader`,
  `SpeechOutput/` (Prism -> SAPI -> clipboard, from Echopunks; namespace `SpeechOutput` so module code's
  Core `Speech` is unambiguous), `HostConfig`, `Log`, `Dev/`.
- **`MagnumOpus.Core`** (netstandard2.0, reloadable, engine-free): Guildrun's Core verbatim (key graph,
  navigator, screens, input categories, type-ahead, key help, strings) minus its audio, hex grid and
  review buffers (dropped on request). Closing a child screen does not re-announce its parent.
- **`MagnumOpus.Module`** (net48 x64, reloadable; feature work goes here): `ModuleMain` (seams, input,
  screens, tick, dev driver), `Input/` (SDL snapshot, bindings, nav input), `Patches/` (`GameKeys`,
  `ButtonPress`, `TextCapture`), `Game/` (`GameScreens`, `GameText`, `Expr`), `Screens/`, `UI/`
  (`Nodes`, `FocusMode`, `Later`), `LanguageSync`, `UpdateChecker`.
- **Loading**: .NET Framework has no collectible contexts: Core and the module are `Assembly.Load(bytes)`
  per generation (files stay unlocked; old generations stay pinned until exit). Core gets a
  per-generation assembly name (`MagnumOpus.Core.g3`) with the module's reference renamed to match, so a
  generation always binds its own Core. LOAD-THEN-SWAP: a broken build leaves the running module.
- **Module rules**: patches arm on the FIRST TICK, never in Load (patching runs a type's cctor; Load runs
  before the game's init); Harmony ids are per load (`UnpatchAll(id)` in Dispose; Lib.Harmony has no
  `UnpatchSelf`); the game's global `InputManager` shadows Core's, so module files alias Core's INSIDE the
  namespace block (`using InputManager = MagnumOpus.Core.Input.InputManager;`; a file-level alias
  conflicts); the game's `Stack<T>` and `Settings` collide with BCL names: write `global::`.

## 11. Input and keys
The module ticks from the GameLoop prefix: `SdlKeyboard.Update` (our own SDL_GetKeyboardState, edge
detected) -> `KeyboardBinding` -> Core's InputManager. It reflects last frame's events, like the game.
Type-ahead characters come from the same snapshot through `SDL_GetKeyFromScancode` (layout-aware).
- **Suppression** (`Patches/GameKeys`): while focus mode is on and a modeled screen is up, the navigation
  keys (arrows, Enter, numpad Enter, Tab, Backspace, Escape, Home, End, Page Up/Down) are blanked in the
  game's four key queries, unless the screen passes them (`GameScreen.PassKeyToGame`: Escape by default,
  so the game's own back-out keeps working). A mod overlay and a CHILD screen (a dropdown's list) own
  every key: Escape closing a list must not also close the screen beneath. Unclaimed keys (letters,
  Space, Delete, F2...) reach the game, except where a screen claims more (`GameScreen.ClaimsKey`: the
  solution editor takes every printable key, Delete and the numpad, since the game's instruction hotkeys
  act on the mouse position and Z deletes the selection).
- **Synthesized game input**: `GameKeys.Forced` makes the game see one key for one frame (only where
  `isKeyInputEnabled`); `ButtonPress.Press(label)` presses a drawn button (only where
  `isMouseButtonInputEnabled`); `MouseClick.At(pos)` is a left click at a screen position for one frame
  (prefixes on `MousePos` / `IsClickPressed`, only where mouse input is enabled; for click code with no
  button, Sigmar's marbles). Prefer them to replicating game code: the game then runs its exact path
  (layout-dependent message boxes, sounds, transitions).
- **Keys**: arrows, Tab / Shift+Tab, Enter, Backspace (secondary), Escape, Home / End, Alt+Up / Alt+Down;
  F1 keys here (Enter runs one; an `ElementAction.HelpRows` lists one action row by row: the editor's instruction keys, one row per permitted instruction, "pivot counter: Alt+Q" on the board, "Q" on the tape plus an insert row, 2026-11-07); Ctrl+Shift+M mod menu; Ctrl+Shift+N focus mode (moved from Ctrl+Shift+A 2026-11-04: the game selects all instructions there).
- **Tab** wraps round the ends on every screen; with a single stop it does nothing and says nothing (user
  rule 2026-11-04; `GraphNavigator.Tab`, `TabWrapTests`). The same rule holds for EVERY cycling key
  (Ctrl+Up / Ctrl+Down through a hex's parts, categories...): wrap with two or more, silent no-op with one.
- **Announcement text never throws out**: `NodeAnnouncement.Resolve` evaluates every label delegate; one
  that throws reads silent and each distinct failure is logged once (a throwing label once failed the
  tick on every navigator rebuild). The host counts repeats of the same tick failure instead of logging
  them, until a reload (`Runtime.TickFrame`).

## 12. Screens (`src/MagnumOpus.Module/Screens/`; verified live 2026-11-03 unless noted)
`GameScreen<T>` is active while game screen T is on top with no fade running; every `GameScreen<T>`
registers T, and `FallbackScreen` (layer -10) covers every other game screen.
- **Title** (`TitleGateScreen`): "Press any key to continue"; any key reaches the game as Escape.
- **Puzzle select** (`PuzzleSelectGameScreen`): items (name, kind, completed; locked rows "locked"; every
  unlocked chapter when the campaign shares the screen, as De Re Metallica does), chapter gems, Journal /
  campaign switch (store notice without the DLC) / Options. Enter runs the game's click code. The unlock
  sequence is spoken as the game plays it (it only sounds it).
- **Letters** (`DocumentGameScreen`): sections as lines, Close (the game's Escape: marks seen, pops).
- **Cutscenes** (`CinematicGameScreen`): each line spoken as it appears ("Speaker: line"; the first by the
  landing only). ONE PRESS = THE NEXT LINE (Echopunks' CutsceneAdvance rule, 2026-11-04): the game types
  a line out (0.005 s a character) and takes an advance only once it shows in full plus 0.1 s and no
  portrait is sliding; Enter or Space finishes its clocks (timeSinceEvent, cinematicsEventTime) and
  forces Space, so the game's own advance runs. Escape passes (skips a seen cutscene).
- **Puzzle info** (`PuzzleInfoGameScreen`): solutions (focus selects, Enter opens), New / Open / Rename /
  Copy / Delete via ButtonPress, the leaderboard notice; the story panel's lines (`StoryPanelNodes`,
  shared with Options, which does the same) are spoken on opening, no stop (user rule 2026-11-04). Histograms: `ScoreTable` in comparing mode.
- **Message boxes** (`MessageBoxGameScreen`): notice / confirm / text box; the game keeps typed
  characters and Backspace in a text box (type-ahead stands down), each change echoed. The typing echo is
  NOT verified live (needs OS input).
- **Pause menu** (`EscapeGameScreen`), **Options** (`OptionsGameScreen`), **Controls**
  (`ControlsGameScreen`), **Workshop upload wait** (`UploadGameScreen`).
- **Dropdowns are real dropdowns** (user rule): a combo-box node (`Nodes.Dropdown`) whose Enter opens
  Core's `ChoiceSubmenuScreen` on the current value; never a slider. Sliders only for numbers (volumes,
  Left/Right in tenths). Options writes the settings exactly as the game's widgets do.
- **Solution editor** (`SolutionEditorGameScreen`, verified live 2026-11-04 on the first two lessons): a
  composite (`GameScreen` derives from `CompositeScreen`) of sections sharing one `Screens/Editor/
  EditorContext` (cursors, tape selection, clipboard, `CarryMode`, `TrackDrawMode`; Guildrun's pattern,
  no partial classes): stops Board / Program / Instructions / Parts / Run log, plus `RunSection` and `UndoSection`
  answering from anywhere. Board: hex cursor (`Game/Hex`: axial Q east, R up the screen, pointy-top, turn
  k = direction k counter-clockwise from east; spoken as Civ's half-step x, y, x = Q + R/2, origin the
  game's (0, 0)). Keys Q NW, E NE, A W, D E, Z SW, C SE (scancodes; numpad 7 9 4 6 1 3; never the
  arrows, user rule 2026-11-04), Alt+Shift+them skip (Alt+numpad), Ctrl+Home the origin, Ctrl+Up / Ctrl+Down the part on the hex edits
  take (default order arm base, other part, track, gripper; reset when the cursor moves), then always
  "instructions", an empty hex's only category (Ctrl+Up / Ctrl+Down silent there; user decisions 2026-11-07): the board's tape keys then act on
  the program cursor's cell or the tape's selection (Ctrl+C / X, Delete, Backspace, Shift+arrows,
  Ctrl+Shift+A, Escape clears, Enter runs to the cell) and the part edits find no part. ONE CLIPBOARD for the player: Ctrl+V pastes what was
  copied or cut last (`EditorContext.PartsOnClipboard`), parts at the board cursor, instructions at the program cursor; parts on the tape stop paste nothing, silently, Up / Down choose the program cursor's arm (wrapping, silent with one; the cursor lands after the arm's last instruction, a Reset's filled cells included, "arm 2, 5"; user decision 2026-11-05: the board cursor's position never chooses it), Left / Right move the program cursor a cycle on that arm and Home / End to its first / last instruction (the tape's keys), [ ] categories,
  , . items, S details, W molecule (size, composition, bonds, holder; the layout is read hex by hex), X neighbours. A hex reads coordinates, then the atom in play, arm
  bases ("arm 1, Piston Arm, northeast, length 1"), grippers, other parts (`Game/Board`; a track hex
  reads its joins marked with the move that goes there, "Track, +northeast, -southeast": the game
  draws a minus sign on the end drawn first and a plus on the last (both at a loop's seam); no "end" /
  "loop" words, user rules 2026-11-04; a glyph hex adds its role, "Glyph of Projection, metal in",
  "Glyph of Rejection, Quicksilver out": `Game/GlyphRoles`, a table transcribed from Sim's glyph
  pass, since neither the game nor Quintessential's recipes store what a hex takes (checked
  2026-11-05); a content mod's glyph reads no role); empty =
  coordinates only. Arms are numbered by program index (the game's own order; its cached list lags a
  pickup or drop until it saves). Program: rows = arms, columns = cycles (1-based); Enter runs to the cell (the game's Alt+click on the
  program, its code run from `RunSection.RunTo`: stopped, the cell must hold an instruction and the run
  starts; running, the cell's next turn; it fast forwards silently and says only "cycle N" where it
  pauses, user decision 2026-11-05; NOT yet verified live); Reset / Repeat cells
  read "clockwise, from reset" (each arm's own expansion, `EditableProgram.GetReferenceForPart`, so they
  read while an error stops the compile). Instruction errors (`Editor/ProgramErrors`, 2026-11-05): a
  Reset / Repeat whose filled cells hold an instruction (the only compile failure; the game draws its
  extent line red): "3, reset, overlaps 4" and "4, repeat, overlaps reset at 3" on the tape, said at
  once when a placement makes one, and on Play / Step as a second message after the game's
  "Instruction errors must be fixed." ("arm 2, reset at 3 overlaps 4", the program cursor moved onto
  it; separate announcements, user rule 2026-11-05). Everywhere: digits 1-9 jump to an
  arm, Space play / pause, Ctrl+Space step, Alt+Space fast forward (the game's buttons via
  `GameKeys.Forced`; its refusal text spoken), Ctrl+Shift+S score ('$' in a cost is the game's gold mark), Ctrl+Shift+T
  tips (the puzzle's sticky notes, also spoken on entry), Ctrl+Shift+D the game's Show Diagram, Alt+P each product's count ("Product (X) at 4, 0:
  6 of 6"; Alt+P is never an instruction).
  Editing (user decisions 2026-11-04, "the most fluid, quick ways to program"; every edit through the
  game's own operations, so its snapshot and save follow at frame end): on the tape the game's
  instruction keys (the key's character on the player's layout matched to the game's hotkeys, which
  follow its Keyboard Layout option: `ProgramEdits.PressedInstruction`) type at the cursor and step on,
  overwriting; Shift inserts (`InsertInstruction`, the push stops at the first gap); Delete clears the
  cell or selection, Backspace the previous cell; Shift+arrows select a rectangle, Ctrl+Shift+A all,
  Escape clears; Ctrl+C / X / V. Refusals spoken: engine running, a fixed arm, an instruction the puzzle
  does not permit. Alt+instruction key on the board programs the program cursor's arm at its cycle,
  wherever the board cursor is ("4, grab"; user decision 2026-11-04: Ctrl is selection and clipboard
  everywhere, so skips moved to Alt+Shift and score / tips to Ctrl+Shift+S / T); Alt+letters on the
  tape type as plain ones. Alt+Backspace jumps from the board to the tape on the arm under the cursor
  (else the program cursor's arm) and back to the same hex. On the board: Enter picks up / puts down (`CarryMode`: out of the solution while carried, the hex it
  was taken by stays under the cursor, `Solution.IsAllowedPlacement` decides and its reason is spoken,
  Escape restores it and the cursor, an editor pop restores it too); Ctrl+Enter carries a copy (the
  game's rules: no reagent, product, unique, conduit, or limited-count puzzle); Shift+A / D rotate (always round the cursor hex, the game's A / D while dragging: an arm by its gripper swings its base; a non-arm part then reads the hex again as a separate announcement; user decisions 2026-11-07, verified live on a product),
  Shift+W / S arm length; Delete removes; Shift+Enter extends a track (`TrackDrawMode`: hex keys extend
  the end or back up along it; a new track from the tray starts drawing when put down). Selection (user
  design 2026-11-04; the game's own `partSelection`, so it draws it): Shift+Space marks a corner, the next
  the opposite one and selects every part touching that x, y rectangle (the game's box-select rule:
  footprint, gripper or attached atom), a third starts over; Ctrl+Shift+Space adds / removes the part
  under the cursor; Ctrl+A all parts; Escape clears (board focused only); fixed parts never join (so the
  tutorial lessons, all fixed, select nothing). A hex with a selected part reads "selected" first. With
  the cursor on a selected part, Enter / Ctrl+Enter (the game's drag-copy: copyable parts copied, the
  rest move along; copies become the selection) / Delete / Shift+A / D (in place round the cursor) /
  Ctrl+C / X act on the group; Ctrl+V places the clipboard at the cursor at once, all or nothing, the
  game's reason spoken when it does not fit (user decision 2026-11-04; several parts land selected;
  after a cut the first paste moves the cut parts, later pastes copy). Copied arms are numbered right after the highest-numbered
  carried part (the game's rule). NOT yet verified live: anything on non-fixed parts. Parts stop: the
  game's toolbar rules (`SolutionEditorPartsPanel`), Enter carries a new part from the board cursor.
  Above the products, the program panel's Show Information and Show Diagram icon buttons
  (`Editor/EditorTools`: the game's enabled rules and click code, its private button builder escaping
  ButtonPress; user decision 2026-11-05); entering the stop still lands on the first part
  (`GraphBuilder.SetStopStart`, a per-stop landing).
  Parts tray: a reagent or product reads its molecule in brief ("Salt, Water; 1 bond"; the
  game draws the whole molecule on the tile), W opens `MoleculeViewerScreen` (a child screen: hex keys
  walk the molecule in its own frame, each hex its atom and bond directions; W summary; Escape). S
  reads a tray part's tooltip description; multi-armed assemblies read "(N grippers)".
  Instructions stop (`InstructionTraySection`): the game's tray as five key columns (Q/A, W/S/X, E/D/C,
  R/F/V, T/G/B; its Delete tile left out), each tile "grab, F" (no role word, user rule), "not available"
  where the puzzle forbids it, "limit reached" for a second period override; Enter places at the program
  cursor ("4, grab"), S the game's description (the tape has no details key: S types retract there; `/` dropped 2026-11-07, user request).
  Undo / redo (Ctrl+Z, Ctrl+Y / Ctrl+Shift+Z): the game's buffer stepped directly (an injected chord
  cannot give the game Ctrl), the change spoken (new states, three at most, else a count).
  Narration (`Patches/SimNarration`): arm moves from the sim states' deltas after `Sim.BeginCycle`, glyph
  work as the atom / bond difference across each `RunCycleGlyphs` (content-mod glyphs included; one
  sentence per part, not per name), products from `outputDone`; ONE MESSAGE PER EVENT (user rule
  2026-11-04, SpeechChem's way): a step says "cycle N" then each actor's sentence queued; at play each
  cycle's first message interrupts the last cycle's; fast forward says nothing (user rule 2026-11-04;
  a product is announced only up to its required count: the game keeps counting past it); errors when the
  game shows them (message + hexes, the board cursor moved there).
  Error panel (`ErrorSection`, a stop after Board while the game shows it): "Error, <message> at
  <hexes>", Replay (replays the failing cycle up to the error; disabled when it is at the cycle's
  start) and Okay (stops the engine), the game's click code run directly (its private button
  builder escapes ButtonPress). Verified live 2026-11-04.
  NOT yet verified live: track drawing, reagents / products from the tray, placing a
  copy, Alt+Space (the game reads Alt itself), the numpad.
- **Completion** (`CompletionGameScreen`, 2026-11-04): one stop: the game's message, then its notice
  (tutorial, custom, hermit mode) or a TABLE (graph columns, SpeechChem's layout): a column per metric
  (cost / cycles / area or instructions; its name the column's context, spoken on crossing), first the
  figures the game prints under the histogram (Now, Best, Prev; Top Percentile with global records),
  then one row per drawn bar, "51g to 63g: 55%, now": the bar's SHARE OF PLAYERS (the game stores
  counts divided by the largest, equal-width bars from zero, so share = height / total height; never
  "% of the tallest bar", SpeechChem's rule), "under 1%" for a drawn bar rounding to 0, the bar under
  the game's marker tagged (bucket = clamp((score - 1) * n / max), `Core/Text/HistogramBins`,
  unit-tested). Then Continue Editing / Record GIF (disabled in tutorials) / Return to Menu via
  ButtonPress. Verified live on Stabilized Water. The table is `Screens/ScoreTable`, shared with puzzle
  info (comparing mode: "This: x" = the selected solution or N/A, "Best: y", bars tagged this / best).
- **Puzzle tips** (`PuzzleTipGameScreen`, 2026-11-04): the tip's name, its text as the game draws it
  (the dragging tip filled with the layout's drag letters), Close (the game's Escape). The animated
  demonstration is not described. Verified live.
- **Diagram** (`PeriodicTableGameScreen`, 2026-11-05; the editor's Show Diagram, "Common Alchemical
  Primes & Their Transmutations"): one picture with the names drawn over it, read as its links (checked
  against the game's screen): "Salt, joined to Air above, Fire left, Water right, Earth below";
  "Vitae upper right and Mors lower left, on a line through Salt"; "Metals, bottom to top: Lead, ...
  Gold; Quicksilver between each"; then the quote, Close. What comes from where (content mods, user
  request 2026-11-05): names live; DIRECTIONS from where the game draws each label (`TextCapture`
  positions through `Core/Text/Compass`, unit-tested; a label not drawn leaves its direction out;
  capture stays on through the opening fade, `GameScreen.ReadsText`); metal chains from
  `AtomTypes.atoms` successor links; the links themselves (Salt to the cardinals, Salt to Vitae and
  Mors) are the stock picture's, fixed (the game has them only as drawn lines and glyph rules); any
  other text drawn is read after them, placed by the nearest element label ("Zinc, below Lead").
  Opening it marks the diagram learned (the button blinks on Refined Gold until then): a live test
  restores `SetDiagramLearned(false)`. NOT yet verified live: the label-derived directions (the
  fixed-wording version was), extra labels (no mod to test with).
- **GIF recorder** (`SolutionRecorderGameScreen`, 2026-11-04): the game's status line (recording, saved
  to the desktop / the specified folder, or the collision error), spoken again when it changes; Close
  (the game's Escape; cancels a recording in progress). The animation is not described.
- **Sigmar's Garden** (`SolitaireGameScreen`, 2026-11-07; all three variants): Board stop, a hex cursor
  over `SolitaireScreen.boardHexes` (the editor's keys and `Game/Hex`, coordinates centred on the game's
  (5, 0); a key that would leave the board does nothing, user rule): "selected" first, coordinates, the
  marble, "locked" where `CanRemove` is false (the game dims it). Enter clicks the marble through
  `MouseClick` at the position the game draws it (`ScreenPosition`, transcribed from `RenderFrame`), then
  diffs the board and selection a frame later: "selected", "unselected", "selected, Mors unselected",
  "cleared Mors, Vitae"; a dimmed one says "locked" without clicking. With no game in play Enter
  forces Space instead: the story's next line (user decision). , . previous / next free marble
  (reading order; NO pair suggestions, user rule: the game shows none), X neighbours. Marbles stop: the
  game's counter strip per variant, "Salt, 4", "warning" where it uses its warning colour. New Game
  (ButtonPress), Rules (its icon button's click code: `SolitaireHelpScreen`, still the fallback), Wins,
  Close. The story panel's reached lines are spoken on opening and each new one as it appears; Space
  passes to the game (it advances the panel); the first deal waits for the first section (10 lines,
  `StoryPanel.GetCompletion() >= 10`). "dealt" when a reveal ends, "board cleared" + wins on a win.
  Verified live: the cursor against a screenshot, free-marble order, select / replace / clear / unselect,
  locked, Rules and back, the story by Space and Enter. NOT verified: a win, the Quintessence and Colvan
  variants (five-marble quintessence picks), New Game.
- **Fallback** (`FallbackScreen`): the screen's human type name ("Journal Submission"), the text it draws
  top to bottom (`TextCapture`, only while the fallback is up) and its labeled buttons (ButtonPress).
  Covers the journal (readable, not yet operable) and the rest.

## 13. Languages
The mod's words follow the GAME's language (`LanguageSync`: `Translations.currentLanguage` ->
`MagnumOpus\lang\<code>.txt`, the game's codes, English without a file; Options changes it live).
`Core/Strings/Strings.cs` is the one table of mod-authored words; `lang/en.txt` is its generated template.
Game terms are the game's own (`GameText.Translate`, `LocString`s).

## 14. Hard rules
- **All speech through Core `Speech.Say`** -> the host pipeline. Navigation moves interrupt; screen entry
  and feedback queue.
- **Never cache game state.** Read live at speak time; never hold game objects across frames beyond the
  screen that owns them.
- **Reuse game text** where it exists; every mod-authored word lives in `Strings.cs`.
- **No silent failures**: every catch logs (`CoreLog` / host `Log`).
- **Announcements** (mod-authored text only; never reword game text): distinguishing word first, no nav
  hints, no fluff, never less information. Users are expert screen-reader users.
- **No game code in the repo** (`game/` is gitignored); `mappings/` ships names and metadata only.
- **Never focus the game window** in automation; **live tests have no side effects** (section 9).

## 15. Roadmap
- Phase 5: Sigmar's Garden (`SolitaireScreen`) on the editor's hex model: DONE (section 12). Left: its
  rules page (`SolitaireHelpScreen`, the fallback today), the items section 12 lists as not verified.
- Phase 6: the solution editor. DONE: reading, narration and editing (section 12). Left: the items
  section 12 lists as not verified; dropping a new arm from the tray onto an arm to swap it (the
  game's drag does); a "step to next event" key. Read part, atom and puzzle names from the game's registries, never hardcoded
  (content mods).
- Phase 7: the journal (operable), the puzzle editor, the solution browser, the GIF recorder, Workshop and
  leaderboards; histograms on puzzle info.
- Phase 9: the Quintessential copy: a net10.0 shim mod (CoreCLR has no AppDomainManager), the module
  multi-targeted, our human names -> intermediary -> their version's human names, Harmony alongside their
  merged MonoMod code (untested).
- Not done yet: the GitHub repo `amerikrainian/magnum-opus` (the update check 404s and logs until the
  first release), a first `create-release.ps1` run, the GUI installer build (`build-installer.ps1`; the
  CLI example and tests are verified).
