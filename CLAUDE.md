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
then `POST /reload` or F6. Tests: `dotnet test src/MagnumOpus.Tests` (Names, Core, Contracts; the two
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
  `isMouseButtonInputEnabled`). Prefer them to replicating game code: the game then runs its exact path
  (layout-dependent message boxes, sounds, transitions).
- **Keys**: arrows, Tab / Shift+Tab, Enter, Backspace (secondary), Escape, Home / End, Alt+Up / Alt+Down;
  F1 keys here (Enter runs one); Ctrl+Shift+M mod menu; Ctrl+Shift+N focus mode (moved from Ctrl+Shift+A 2026-11-04: the game selects all instructions there); F6 reload (DEBUG).
- **Tab** wraps round the ends on every screen; with a single stop it does nothing and says nothing (user
  rule 2026-11-04; `GraphNavigator.Tab`, `TabWrapTests`).
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
- **Cutscenes** (`CinematicGameScreen`): each line spoken as it appears ("Speaker: line"); Enter advances
  (as Space). NOT yet verified live (no cutscene unlocked on the test profile).
- **Puzzle info** (`PuzzleInfoGameScreen`): solutions (focus selects, Enter opens), New / Open / Rename /
  Copy / Delete via ButtonPress, the leaderboard notice, the story panel (`StoryPanelNodes`, shared with
  Options). Histograms of solved puzzles are not read yet.
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
  arrows, user rule 2026-11-04), Alt+Shift+them skip (Alt+numpad), ; and ' move the program cursor on the arm under the board cursor, [ ] categories,
  , . items, S or / details, W molecule (size, composition, bonds, holder; the layout is read hex by hex), X neighbours. A hex reads coordinates, then the atom in play, arm
  bases ("arm 1, Piston Arm, northeast, length 1"), grippers, other parts (`Game/Board`); empty =
  coordinates only. Arms are numbered by program index (the game's own order; its cached list lags a
  pickup or drop until it saves). Program: rows = arms, columns = cycles (1-based); Reset / Repeat cells
  read "clockwise, from reset" (`CompiledProgramGrid`, `isExpansion`). Everywhere: digits 1-9 jump to an
  arm, Space play / pause, Ctrl+Space step, Alt+Space fast forward (the game's buttons via
  `GameKeys.Forced`; its refusal text spoken), Ctrl+Shift+S score ('$' in a cost is the game's gold mark), Ctrl+Shift+T
  tips (the puzzle's sticky notes, also spoken on entry), Alt+P each product's count ("Product (X) at 4, 0:
  6 of 6"; Alt+P is never an instruction).
  Editing (user decisions 2026-11-04, "the most fluid, quick ways to program"; every edit through the
  game's own operations, so its snapshot and save follow at frame end): on the tape the game's
  instruction keys (the key's character on the player's layout matched to the game's hotkeys, which
  follow its Keyboard Layout option: `ProgramEdits.PressedInstruction`) type at the cursor and step on,
  overwriting; Shift inserts (`InsertInstruction`, the push stops at the first gap); Delete clears the
  cell or selection, Backspace the previous cell; Shift+arrows select a rectangle, Ctrl+Shift+A all,
  Escape clears; Ctrl+C / X / V. Refusals spoken: engine running, a fixed arm, an instruction the puzzle
  does not permit. Alt+instruction key on the board programs the arm under the cursor at the program
  cursor's cycle ("arm 1, 4, grab"; user decision 2026-11-04: Ctrl is selection and clipboard
  everywhere, so skips moved to Alt+Shift and score / tips to Ctrl+Shift+S / T); Alt+letters on the
  tape type as plain ones. Alt+Backspace jumps from the board to the tape on the arm under the cursor
  and back to the same hex. On the board: Enter picks up / puts down (`CarryMode`: out of the solution while carried, the hex it
  was taken by stays under the cursor, `Solution.IsAllowedPlacement` decides and its reason is spoken,
  Escape restores it and the cursor, an editor pop restores it too); Ctrl+Enter carries a copy (the
  game's rules: no reagent, product, unique, conduit, or limited-count puzzle); Shift+A / D rotate,
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
  Instructions stop (`InstructionTraySection`): the game's tray as five key columns (Q/A, W/S/X, E/D/C,
  R/F/V, T/G/B; its Delete tile left out), each tile "grab, F" (no role word, user rule), "not available"
  where the puzzle forbids it, "limit reached" for a second period override; Enter places at the program
  cursor ("arm 1, 4, grab"), S or / the game's description.
  Undo / redo (Ctrl+Z, Ctrl+Y / Ctrl+Shift+Z): the game's buffer stepped directly (an injected chord
  cannot give the game Ctrl), the change spoken (new states, three at most, else a count).
  Narration (`Patches/SimNarration`): arm moves from the sim states' deltas after `Sim.BeginCycle`, glyph
  work as the atom / bond difference across each `RunCycleGlyphs` (content-mod glyphs included; one
  sentence per part, not per name), products from `outputDone`; ONE MESSAGE PER EVENT (user rule
  2026-11-04, SpeechChem's way): a step says "cycle N" then each actor's sentence queued; at play each
  cycle's first message interrupts the last cycle's; fast forward says nothing (user rule 2026-11-04;
  a product is announced only up to its required count: the game keeps counting past it); errors when the
  game shows them (message + hexes, the board cursor moved there).
  NOT yet verified live: error narration, track drawing, reagents / products from the tray, placing a
  copy, Alt+Space (the game reads Alt itself), the numpad.
- **Completion** (`CompletionGameScreen`, 2026-11-04): the puzzle's name, the game's message, then its
  notice (tutorial, custom, hermit mode) or cost / cycles / area-or-instructions with the figures the game
  prints under each histogram (Now, Best, Prev; Top Percentile with global records), then Continue
  Editing / Record GIF (disabled in tutorials, as the game has them) / Return to Menu via ButtonPress;
  Escape is the game's (continues editing outside tutorials). Verified live on a tutorial; the scores
  are NOT yet verified (need a non-tutorial completion); the histogram curves are not read yet.
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
- Phase 5: Sigmar's Garden (`SolitaireScreen`) on the editor's hex model (section 12).
- Phase 6: the solution editor. DONE: reading, narration and editing (section 12). Left: the items
  section 12 lists as not verified; reading the game's instruction errors (the red cells behind
  "Instruction errors must be fixed."); dropping a new arm from the tray onto an arm to swap it (the
  game's drag does); a "step to next event" key; the completion screen's histogram curves. Read part, atom and puzzle names from the game's registries, never hardcoded
  (content mods).
- Phase 7: the journal (operable), the puzzle editor, the solution browser, the GIF recorder, Workshop and
  leaderboards; histograms on puzzle info.
- Phase 9: the Quintessential copy: a net10.0 shim mod (CoreCLR has no AppDomainManager), the module
  multi-targeted, our human names -> intermediary -> their version's human names, Harmony alongside their
  merged MonoMod code (untested).
- Not done yet: the GitHub repo `amerikrainian/magnum-opus` (the update check 404s and logs until the
  first release), a first `create-release.ps1` run, the GUI installer build (`build-installer.ps1`; the
  CLI example and tests are verified).
