using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.Screens.Editor;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The journal's Puzzle Editor and Steam Workshop tabs (PuzzleEditorScreen: immediate-mode, mouse
    /// only; the custom puzzles, or the subscribed Workshop ones). Stops:
    ///  1. the puzzles, as the game lists them ("completed" where it draws its check: a workshop
    ///     puzzle solved, a custom one solved since its last edit, which Upload needs); focusing one
    ///     selects it, as the game's Up / Down do; Enter is the game's Test / Open; with none
    ///     selected, the game's hint text;
    ///  2. the game's buttons (New, Rename, Copy, Delete, Test, Upload; Browse, Open), pressed
    ///     through ButtonPress with the game's enabled rules;
    ///  3. Products, 4. Reagents: each molecule in brief (the editor tray's words); on a custom
    ///     puzzle Enter edits it (the game's click on the tile, opening its molecule editor), Delete
    ///     removes it (the click on the tile's close mark: the game's confirmation), and "Create New
    ///     Product" while there is room; W reads it hex by hex (<see cref="MoleculeViewerScreen"/>);
    ///  5. Mechanisms and Glyphs: the game's permission checkboxes, toggled by the game's click
    ///     (read only on a Workshop puzzle; a DLC glyph without the DLC opens the store, as the game's
    ///     click does);
    ///  6. the journal's tabs and Close (the game's Escape).
    /// Stops 3 to 5 exist while a puzzle is selected (the game draws them then). The game's own keys
    /// keep working where they are not ours: Delete on the list asks to delete the puzzle, F2 renames.
    /// Pictures (the molecules drawn on the tiles) are read as molecules, never described.
    /// </summary>
    public sealed class PuzzleEditorGameScreen : GameScreen<PuzzleEditorScreen>
    {
        public override string Key => "game.puzzle_editor";

        public override string ScreenName => Game == null ? null : GameText.Translate(Personal(Game) ? "Puzzle Editor" : "Steam Workshop");

        public override object InitialFocusStop => "puzzles";

        public override bool KeepStateOnPop => true;

        // The puzzle the game had selected the last time we looked: a change we did not make (New,
        // Copy) is spoken.
        private string _selectedId;

        // The game screen we were built on: the Puzzle Editor and Steam Workshop tabs are two game
        // screens of one type, so switching between them is seen here.
        private PuzzleEditorScreen _game;

        private static bool Personal(PuzzleEditorScreen g) => g.type == PuzzleEditorScreenType.PersonalPuzzles;

        private static Puzzle Selected(PuzzleEditorScreen g) => g != null && g.selectedPuzzle.HasValue() ? g.selectedPuzzle.GetValue() : null;

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            bool personal = Personal(g);
            var puzzles = g.GetPuzzles();
            var selected = Selected(g);

            b.BeginStop("puzzles");
            foreach (var puzzle in puzzles)
            {
                var p = puzzle;
                b.AddItem(ControlId.Structural("pe:puzzle:" + p.puzzleId), new NodeVtable
                {
                    ControlType = ControlTypes.Item,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => GameText.Of(p.puzzleName)),
                        new NodeAnnouncement(() => Completed(p) ? Strings.StateCompleted : null, live: true, kind: AnnouncementKinds.Selected),
                    },
                    SearchText = () => GameText.Of(p.puzzleName),
                    OnFocus = () => Select(p),
                    OnActivate = () => { Select(p); OpenInfo(); },
                });
            }
            if (selected == null)
                b.AddItem(ControlId.Structural("pe:hint"), Nodes.Text(Hint));
            else
                b.SetStopStart(ControlId.Structural("pe:puzzle:" + selected.puzzleId));

            b.BeginStop("actions");
            if (personal)
            {
                Func<bool> has = () => Selected(Game) != null;
                AddPress(b, "New", null);
                AddPress(b, "Rename", has);
                AddPress(b, "Copy", has);
                AddPress(b, "Delete", has);
                AddPress(b, "Test", has);
                AddPress(b, "Upload", () => { var s = Selected(Game); return s != null && GameLogic.instance.workshopManager.HadEdits(s); });
            }
            else
            {
                AddPress(b, "Browse", null);
                AddPress(b, "Open", () => Selected(Game) != null);
            }

            if (selected != null)
            {
                BuildMolecules(b, personal, 0);
                BuildMolecules(b, personal, 1);
                BuildPermissions(b, personal);
            }

            b.BeginStop("tabs");
            JournalTabs.Build(b, personal ? 1 : 2);
        }

        public override void OnFocus()
        {
            base.OnFocus();
            _game = Game;
            _selectedId = Selected(Game)?.puzzleId;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            var g = Game;
            if (g != null && !ReferenceEquals(g, _game))
            {
                // The other tab: announced and landed on as if opened.
                bool switched = _game != null;
                _game = g;
                _selectedId = Selected(g)?.puzzleId;
                if (switched)
                {
                    Speech.Say(ScreenName, true);
                    Navigation.FocusStop("puzzles");
                }
                return;
            }
            var s = Selected(g);
            string id = s?.puzzleId;
            if (id == _selectedId) return;
            _selectedId = id;
            if (s != null) Speech.Say(GameText.Of(s.puzzleName));
        }

        // The game's Delete reaches it on the list (it asks to delete the puzzle); on a product or
        // reagent Delete removes that one instead.
        public override bool ClaimsKey(int sdlKeycode) => sdlKeycode == GameKeys.Delete && FocusedSlot(out _, out _);

        public override IEnumerable<ElementAction> GetActions()
        {
            foreach (var a in base.GetActions()) yield return a;
            var g = Game;
            if (g == null || !FocusedSlot(out int kind, out int slot)) yield break;
            if (Personal(g))
                yield return new ElementAction(EditorActions.Delete, Strings.Get("bind.pe.remove"), _ => RemoveMolecule(kind, slot));
            yield return new ElementAction(EditorActions.Molecule, Strings.Get("bind.ed.molecule"), _ => ViewMolecule(kind, slot));
        }

        // ---- the puzzles ----

        private static bool Completed(Puzzle p)
        {
            var g = GameScreens.TopAs<PuzzleEditorScreen>();
            if (g == null) return false;
            return Personal(g) ? GameLogic.instance.workshopManager.HadEdits(p) : GameLogic.instance.saveData.IsComplete(p);
        }

        private void Select(Puzzle p)
        {
            var g = Game;
            if (g == null || Selected(g) == p) return;
            g.SetSelected(p);
            _selectedId = p.puzzleId;
        }

        // The game's double click, Enter, Test and Open.
        private void OpenInfo()
        {
            var g = Game;
            if (g == null || Selected(g) == null) return;
            g.ToPuzzleInfoScreen();
            Assets.sounds.click_button.PlaySound(1f);
        }

        private string Hint()
        {
            var g = Game;
            if (g == null) return "";
            if (!Personal(g)) return GameText.Translate("Click the *browse* button to browse available puzzles on Steam Workshop.");
            return GameText.Translate(GameLogic.instance.workshopManager.customPuzzles.Count == 0
                ? "Click the *new* button to create a custom puzzle."
                : "Select a custom puzzle to edit its products, reagents, mechanisms, and glyphs.");
        }

        private static void AddPress(GraphBuilder b, string english, Func<bool> enabled)
        {
            b.AddItem(ControlId.Structural("pe:button:" + english), Nodes.Button(() => GameText.Translate(english), () => Press(english), enabled));
        }

        private static void Press(string english)
        {
            if (!ButtonPress.Press(Translations.Translate(english).GetTranslated()))
                CoreLog.Warning("[puzzle editor] the game did not draw a '" + english + "' button last frame.");
        }

        // ---- products and reagents ----

        // kind 0 = products (the game's outputs), 1 = reagents (inputs): the game's loop order.
        private static PuzzleInputOutput[] Molecules(PuzzleEditorScreen g, int kind)
        {
            var p = Selected(g);
            if (p == null) return new PuzzleInputOutput[0];
            return (kind == 0 ? p.outputs : p.inputs) ?? new PuzzleInputOutput[0];
        }

        private void BuildMolecules(GraphBuilder b, bool personal, int kind)
        {
            var g = Game;
            b.BeginStop(kind == 0 ? "products" : "reagents");
            b.PushContext(GameText.Translate(kind == 0 ? "Products" : "Reagents"));
            int count = Molecules(g, kind).Length;
            for (int j = 0; j < count && j < 4; j++)
            {
                int slot = j;
                b.AddItem(ControlId.Structural("pe:io:" + kind + ":" + slot), new NodeVtable
                {
                    ControlType = personal ? ControlTypes.Button : ControlTypes.Item,
                    Announcements = new List<NodeAnnouncement> { Nodes.LabelPart(() => Brief(kind, slot)) },
                    SearchText = () => Brief(kind, slot),
                    OnActivate = personal ? (Action)(() => MouseClick.At(TileCentre(kind, slot))) : () => ViewMolecule(kind, slot),
                });
            }
            if (personal && count < 4)
            {
                string english = kind == 0 ? "Create New Product" : "Create New Reagent";
                // Any empty tile does it; the first one is clicked.
                b.AddItem(ControlId.Structural("pe:io:new:" + kind), Nodes.Button(() => GameText.Translate(english),
                    () => MouseClick.At(TileCentre(kind, Molecules(Game, kind).Length))));
            }
            b.PopContext();
        }

        private string Brief(int kind, int slot)
        {
            var list = Molecules(Game, kind);
            return slot < list.Length && list[slot].molecule != null ? Board.MoleculeSummaryText(list[slot].molecule) : "";
        }

        // The product or reagent the focus is on (not the Create New tile).
        private bool FocusedSlot(out int kind, out int slot)
        {
            kind = slot = 0;
            if (Game == null) return false;
            var key = Navigation.FocusedNode?.Id?.StructuralKey as string;
            if (key == null || !key.StartsWith("pe:io:", StringComparison.Ordinal)) return false;
            var parts = key.Split(':');
            return parts.Length == 4 && int.TryParse(parts[2], out kind) && int.TryParse(parts[3], out slot);
        }

        private void RemoveMolecule(int kind, int slot)
        {
            var g = Game;
            if (g == null || slot >= Molecules(g, kind).Length) return;
            var close = Assets.textures.puzzle_editor.product_close.size.ToVector2();
            MouseClick.At(TileMin(kind, slot) + new Vector2(176f, 165f) + close / 2f);
        }

        private void ViewMolecule(int kind, int slot)
        {
            var g = Game;
            if (g == null || slot >= Molecules(g, kind).Length) return;
            string id = Selected(g)?.puzzleId;
            MoleculeViewerScreen.Open(Brief(kind, slot), () =>
            {
                var gg = GameScreens.TopAs<PuzzleEditorScreen>();
                var p = Selected(gg);
                if (p == null || p.puzzleId != id) return null;
                var list = Molecules(gg, kind);
                return slot < list.Length ? list[slot].molecule : null;
            });
        }

        // Where RenderEditor draws a tile: the window centred on the screen.
        private static Vector2 Window() =>
            (global::InputManager.screenSize / 2f - new Vector2(1516f, 922f) / 2f + new Vector2(-2f, -11f)).Rounded();

        private static Vector2 TileMin(int kind, int slot) =>
            Window() + new Vector2(495f, 588f) + new Vector2(slot * 236, kind == 0 ? -28 : -281);

        // The tile's middle (the tile is 226 by 201; its close mark sits at the top right).
        private static Vector2 TileCentre(int kind, int slot) => TileMin(kind, slot) + new Vector2(113f, 100f);

        // ---- mechanisms and glyphs ----

        private struct Permission
        {
            public Func<string> Name;
            public PuzzlePermissions Flag;
            public bool Dlc;
            public int Column, Row;
        }

        // RenderEditor's checkboxes, in its order (four columns, row by row).
        private static Permission[] Permissions()
        {
            var list = new List<Permission>();
            void Add(Func<string> name, PuzzlePermissions flag, bool dlc = false)
            {
                int i = list.Count;
                list.Add(new Permission { Name = name, Flag = flag, Dlc = dlc, Column = i % 4, Row = i / 4 });
            }
            Add(() => Short("Glyph of Bonding###Shorter"), PuzzlePermissions.Bonder);
            Add(() => Short("Glyph of Multi-bonding###Shorter"), PuzzlePermissions.SpeedBonder);
            Add(() => Short("Glyph of Triplex-bonding###Shorter"), PuzzlePermissions.PrismaBonder);
            Add(() => Short("Glyph of Unbonding###Shorter"), PuzzlePermissions.Unbonder);
            Add(() => Short("Glyph of Calcification###Shorter"), PuzzlePermissions.Calcification);
            Add(() => Short("Glyph of Duplication###Shorter"), PuzzlePermissions.Duplication);
            Add(() => GameText.Of(PartTypes.berlosWheel.name), PuzzlePermissions.BaronWheel);
            Add(() => Short("Glyph of Animismus###Shorter"), PuzzlePermissions.LifeAndDeath);
            Add(() => Short("Glyph of Projection###Shorter"), PuzzlePermissions.Projection);
            Add(() => Short("Glyph of Purification###Shorter"), PuzzlePermissions.Purification);
            Add(() => Short("Glyph of Disposal###Shorter"), PuzzlePermissions.Disposal);
            Add(() => Short("Glyphs of Quintessence"), PuzzlePermissions.Quintessence);
            Add(() => Short("Glyph of Rejection###Shorter"), PuzzlePermissions.Rejection, dlc: true);
            Add(() => Short("Glyph of Division###Shorter"), PuzzlePermissions.Division, dlc: true);
            Add(() => Short("Glyph of Proliferation###Shorter"), PuzzlePermissions.Proliferation, dlc: true);
            Add(() => GameText.Of(PartTypes.ravarisWheel.name), PuzzlePermissions.RavariWheel, dlc: true);
            return list.ToArray();
        }

        // The game's "###Shorter" rows hold a short name; a missing row would read the key.
        private static string Short(string english)
        {
            string s = GameText.Translate(english);
            int cut = s.IndexOf("###", StringComparison.Ordinal);
            return cut >= 0 ? s.Substring(0, cut) : s;
        }

        private void BuildPermissions(GraphBuilder b, bool personal)
        {
            b.BeginStop("permissions");
            b.PushContext(GameText.Translate("Mechanisms and Glyphs"));
            foreach (var permission in Permissions())
            {
                var p = permission;
                // The game's isEnabled: a DLC glyph without the DLC, dimmed; its click opens the store.
                Func<bool> locked = () => personal && p.Dlc && !PuzzleSelectScreen.HasDRM();
                Func<bool> on = () => { var s = Selected(Game); return s != null && (s.permissionFlags & p.Flag) == p.Flag; };
                b.AddItem(ControlId.Structural("pe:perm:" + (int)p.Flag), new NodeVtable
                {
                    ControlType = ControlTypes.Toggle,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(p.Name),
                        Nodes.ValuePart(() => on() ? Strings.StateOn : Strings.StateOff),
                        new NodeAnnouncement(() => locked() ? Strings.DlcNotOwned : null, kind: AnnouncementKinds.Enabled),
                    },
                    SearchText = p.Name,
                    // The value is live: the game flips it on the clicked frame, after this.
                    OnActivate = personal
                        ? (Action)(() => MouseClick.At(Window() + new Vector2(494f, 219f) + PuzzleEditorScreen.GetPermissionTogglePos(p.Column, p.Row) + new Vector2(18f, 18f)))
                        : null,
                });
            }
            b.PopContext();
        }
    }

    /// <summary>
    /// A custom puzzle's product or reagent being edited (MoleculeEditorScreen, titled Product or
    /// Reagent). Stops:
    ///  1. the grid: a hex cursor over the game's builder board (61 hexes round the centre, which
    ///     must hold an atom), walked with the editor's hex keys, stopping silently at its edge. A
    ///     hex reads its coordinates (the centre is 0, 0), then its atom and bonds, as the molecule
    ///     viewer does; a hex the game fills faintly with a copy of a repeating pattern reads
    ///     "Salt, repeated". Enter places the selected atom (replacing one there), Delete removes
    ///     the atom, Shift with a hex key bonds toward that neighbour with the selected bond type
    ///     (again removes it): each the game's own click there (left places, right removes), so its
    ///     rules run, and what changed is spoken, or "no change". [ ] pick the atom, Shift+[ ] the
    ///     bond type; W the molecule in brief, X the neighbours, Ctrl+Home the centre.
    ///  2. Atoms, 3. Other (the game's two panels): the atom tools, then the two bond tools and the
    ///     repetition placeholder (products only); Enter selects one.
    ///  4. the game's error line while it shows one, Cancel, Save (disabled while there is an error,
    ///     as the game's). Ctrl+S saves from anywhere; Escape is the game's (closes, unsaved).
    /// An edit that changes the game's error says the new one (or that none is left) after its own
    /// feedback.
    /// </summary>
    public sealed class MoleculeEditorGameScreen : GameScreen<MoleculeEditorScreen>
    {
        public override string Key => "game.molecule_editor";

        public override string ScreenName => Game == null ? null : GameText.Translate(Game.isProduct ? "Product" : "Reagent");

        public override object InitialFocusStop => "grid";

        public override bool AllowsTypeahead => !GridFocused;

        private HexIndex _cursor = new HexIndex(0, 0);

        // An edit made by a click, read back once the game frame that ran it is over.
        private enum EditKind { None, Place, Remove, Bond }
        private EditKind _edit;
        private int _editTicks;
        private HexIndex _editHex, _editOther;
        private HexDir _editDir;
        private AtomType _atomBefore;
        private BondTypeEnum _bondBefore;
        private string _errorBefore;

        private bool GridFocused => Equals(Navigation.FocusedStopKey, "grid");

        public override void OnPush()
        {
            base.OnPush();
            _cursor = new HexIndex(0, 0);
            _edit = EditKind.None;
        }

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;

            b.BeginStop("grid");
            b.AddItem(ControlId.Structural("mol:grid"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[] { Nodes.LabelPart(() => Readout(_cursor)) },
                OnActivate = Place,
            });

            b.BeginStop("atoms");
            b.PushContext(GameText.Translate("Atoms"));
            foreach (var type in AtomTools(includeRepeat: false))
                AddAtomTool(b, type);
            b.PopContext();

            b.BeginStop("other");
            b.PushContext(GameText.Translate("Other"));
            foreach (var bond in BondTools)
            {
                var t = bond;
                b.AddItem(ControlId.Structural("mol:bondtool:" + (int)t), new NodeVtable
                {
                    ControlType = ControlTypes.RadioButton,
                    Announcements = new List<NodeAnnouncement>
                    {
                        Nodes.LabelPart(() => BondName(t)),
                        Nodes.SelectedPart(() => Game != null && Game.selectedBondTypeEnum == t),
                    },
                    SearchText = () => BondName(t),
                    OnActivate = () => SelectBond(t, speak: false),
                });
            }
            if (g.isProduct) AddAtomTool(b, AtomTypes.repeat);
            b.PopContext();

            b.BeginStop("buttons");
            if (g.errorMessage.HasValue())
                b.AddItem(ControlId.Structural("mol:error"), Nodes.Text(() => ErrorText() ?? ""));
            b.AddItem(ControlId.Structural("mol:cancel"), Nodes.Button(() => GameText.Translate("Cancel"), () => Press("Cancel")));
            b.AddItem(ControlId.Structural("mol:save"), Nodes.Button(() => GameText.Translate("Save"), Save,
                enabled: () => Game != null && !Game.errorMessage.HasValue()));
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            foreach (var a in base.GetActions()) yield return a;
            if (Game == null) yield break;
            yield return new ElementAction(EditorActions.MoleculeSave, Strings.Get("bind.mol.save"), _ => Save());
            if (!GridFocused) yield break;
            yield return new ElementAction(EditorActions.HexNW, Strings.Get("bind.ed.hex.nw"), _ => Move(HexDir.NorthWest, false));
            yield return new ElementAction(EditorActions.HexNE, Strings.Get("bind.ed.hex.ne"), _ => Move(HexDir.NorthEast, false));
            yield return new ElementAction(EditorActions.HexW, Strings.Get("bind.ed.hex.w"), _ => Move(HexDir.West, false));
            yield return new ElementAction(EditorActions.HexE, Strings.Get("bind.ed.hex.e"), _ => Move(HexDir.East, false));
            yield return new ElementAction(EditorActions.HexSW, Strings.Get("bind.ed.hex.sw"), _ => Move(HexDir.SouthWest, false));
            yield return new ElementAction(EditorActions.HexSE, Strings.Get("bind.ed.hex.se"), _ => Move(HexDir.SouthEast, false));
            yield return new ElementAction(EditorActions.SkipNW, Strings.Get("bind.ed.skip.nw"), _ => Move(HexDir.NorthWest, true));
            yield return new ElementAction(EditorActions.SkipNE, Strings.Get("bind.ed.skip.ne"), _ => Move(HexDir.NorthEast, true));
            yield return new ElementAction(EditorActions.SkipW, Strings.Get("bind.ed.skip.w"), _ => Move(HexDir.West, true));
            yield return new ElementAction(EditorActions.SkipE, Strings.Get("bind.ed.skip.e"), _ => Move(HexDir.East, true));
            yield return new ElementAction(EditorActions.SkipSW, Strings.Get("bind.ed.skip.sw"), _ => Move(HexDir.SouthWest, true));
            yield return new ElementAction(EditorActions.SkipSE, Strings.Get("bind.ed.skip.se"), _ => Move(HexDir.SouthEast, true));
            yield return new ElementAction(EditorActions.Origin, Strings.Get("bind.ed.origin"), _ => { _cursor = new HexIndex(0, 0); Speech.Say(Readout(_cursor), true); });
            yield return new ElementAction(EditorActions.BondNW, Strings.Get("bind.mol.bond.nw"), _ => Bond(HexDir.NorthWest));
            yield return new ElementAction(EditorActions.BondNE, Strings.Get("bind.mol.bond.ne"), _ => Bond(HexDir.NorthEast));
            yield return new ElementAction(EditorActions.BondW, Strings.Get("bind.mol.bond.w"), _ => Bond(HexDir.West));
            yield return new ElementAction(EditorActions.BondE, Strings.Get("bind.mol.bond.e"), _ => Bond(HexDir.East));
            yield return new ElementAction(EditorActions.BondSW, Strings.Get("bind.mol.bond.sw"), _ => Bond(HexDir.SouthWest));
            yield return new ElementAction(EditorActions.BondSE, Strings.Get("bind.mol.bond.se"), _ => Bond(HexDir.SouthEast));
            yield return new ElementAction(EditorActions.Delete, Strings.Get("bind.ed.delete"), _ => Remove());
            yield return new ElementAction(EditorActions.AtomPrev, Strings.Get("bind.mol.atom.prev"), _ => CycleAtom(-1));
            yield return new ElementAction(EditorActions.AtomNext, Strings.Get("bind.mol.atom.next"), _ => CycleAtom(1));
            yield return new ElementAction(EditorActions.BondTypePrev, Strings.Get("bind.mol.bondtype.prev"), _ => CycleBond());
            yield return new ElementAction(EditorActions.BondTypeNext, Strings.Get("bind.mol.bondtype.next"), _ => CycleBond());
            yield return new ElementAction(EditorActions.Molecule, Strings.Get("bind.ed.molecule"), _ => { var g = Game; if (g != null) Speech.Say(Summary(g), true); });
            yield return new ElementAction(EditorActions.Neighbours, Strings.Get("bind.ed.neighbours"), _ => Speech.Say(Neighbours(), true));
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            if (_edit != EditKind.None && _editTicks-- <= 0) Resolve();
        }

        // ---- the tools ----

        private static AtomType[] AtomTools(bool includeRepeat)
        {
            var list = new List<AtomType>
            {
                AtomTypes.salt, AtomTypes.air, AtomTypes.fire, AtomTypes.quicksilver, AtomTypes.water, AtomTypes.earth,
                AtomTypes.lead, AtomTypes.tin, AtomTypes.iron, AtomTypes.copper, AtomTypes.silver, AtomTypes.gold,
                AtomTypes.vitae, AtomTypes.mors, AtomTypes.quintessence,
            };
            if (includeRepeat) list.Add(AtomTypes.repeat);
            return list.ToArray();
        }

        private static readonly BondTypeEnum[] BondTools =
        {
            BondTypeEnum.Standard,
            BondTypeEnum.Prisma0 | BondTypeEnum.Prisma1 | BondTypeEnum.Prisma2,
        };

        private static string ToolName(AtomType type) => ReferenceEquals(type, AtomTypes.repeat) ? Strings.MolRepeat : Board.AtomName(type);

        private static string BondName(BondTypeEnum type) => type == BondTypeEnum.Standard ? Strings.MolBondStandard : Strings.MolBondTriplex;

        private void AddAtomTool(GraphBuilder b, AtomType type)
        {
            var t = type;
            b.AddItem(ControlId.Structural("mol:atomtool:" + ToolName(t)), new NodeVtable
            {
                ControlType = ControlTypes.RadioButton,
                Announcements = new List<NodeAnnouncement>
                {
                    Nodes.LabelPart(() => ToolName(t)),
                    Nodes.SelectedPart(() => Game != null && ReferenceEquals(Game.selectedAtomType, t)),
                },
                SearchText = () => ToolName(t),
                OnActivate = () => SelectAtom(t, speak: false),
            });
        }

        // AtomTypeSelector's click.
        private void SelectAtom(AtomType type, bool speak)
        {
            var g = Game;
            if (g == null) return;
            if (!ReferenceEquals(g.selectedAtomType, type))
            {
                g.selectedAtomType = type;
                Assets.sounds.click_button.PlaySound(1f);
            }
            if (speak) Speech.Say(ToolName(type), true);
        }

        // BondTypeSelector's click.
        private void SelectBond(BondTypeEnum type, bool speak)
        {
            var g = Game;
            if (g == null) return;
            if (g.selectedBondTypeEnum != type)
            {
                g.selectedBondTypeEnum = type;
                Assets.sounds.click_button.PlaySound(1f);
            }
            if (speak) Speech.Say(BondName(type), true);
        }

        private void CycleAtom(int by)
        {
            var g = Game;
            if (g == null) return;
            var tools = AtomTools(g.isProduct);
            int i = Array.FindIndex(tools, t => ReferenceEquals(t, g.selectedAtomType));
            int next = i < 0 ? 0 : ((i + by) % tools.Length + tools.Length) % tools.Length;
            SelectAtom(tools[next], speak: true);
        }

        // Two bond tools: either direction is the other one.
        private void CycleBond()
        {
            var g = Game;
            if (g == null) return;
            SelectBond(g.selectedBondTypeEnum == BondTools[0] ? BondTools[1] : BondTools[0], speak: true);
        }

        // ---- the grid ----

        private static bool OnBoard(HexIndex h) => MoleculeEditorScreen.builderBoard.Contains(h);

        private void Move(HexDir dir, bool skip)
        {
            var next = Hex.Step(_cursor, dir);
            if (!OnBoard(next)) return; // the edge: silent
            if (skip) while (OnBoard(Hex.Step(next, dir))) next = Hex.Step(next, dir);
            _cursor = next;
            Speech.Say(Readout(_cursor), true);
        }

        private string Readout(HexIndex hex)
        {
            string what = Describe(hex);
            return what == null ? Hex.Coords(hex) : Hex.Coords(hex) + ", " + what;
        }

        // The atom and its bonds, or the faint copy of a repeating pattern; null for an empty hex.
        private string Describe(HexIndex hex)
        {
            var g = Game;
            if (g == null) return null;
            var m = g.molecule;
            if (m.GetAtoms().TryGetValue(hex, out var atom))
            {
                string bonds = Board.Bonds(new Board.AtomAt { Atom = atom, Molecule = m, Hex = hex });
                return bonds.Length > 0 ? Board.AtomName(atom.atomType) + ", " + bonds : Board.AtomName(atom.atomType);
            }
            var repeat = m.RepeatAtomPositions().FirstOrNone();
            if (!repeat.HasValue()) return null;
            var step = repeat.GetValue();
            foreach (var kv in m.GetAtoms())
            {
                if (ReferenceEquals(kv.Value.atomType, AtomTypes.repeat)) continue;
                for (int k = 1; k < UIConsts.outputCount; k++)
                    if (kv.Key + k * step == hex) return Strings.MolRepeated(Board.AtomName(kv.Value.atomType));
            }
            return null;
        }

        // In brief, as the editor's tray will read it: a valid repeating pattern as the game saves it
        // (the unit and its offset), anything else as it stands.
        private static string Summary(MoleculeEditorScreen g)
        {
            var m = g.molecule;
            if (m.RepeatAtomPositions().Any() && !g.errorMessage.HasValue()) m = Molecule.RepeatMonomer(m);
            return Board.MoleculeSummaryText(m);
        }

        private string Neighbours()
        {
            var parts = new List<string>();
            foreach (var dir in Hex.All)
            {
                string what = Describe(Hex.Step(_cursor, dir));
                if (what != null) parts.Add(Strings.EditorNeighbour(Hex.Word(dir), what));
            }
            return string.Join("; ", parts);
        }

        // ---- editing: the game's clicks ----

        // Where the game draws a hex of the builder board.
        private static Vector2 HexPosition(HexIndex h)
        {
            var window = (global::InputManager.screenSize / 2f - new Vector2(1516f, 922f) / 2f + new Vector2(-2f, -11f)).Rounded();
            return HexGrid.standardGrid.ToPixelCoords(h, window + new Vector2(946f, 472f));
        }

        private AtomType AtomAt(HexIndex h)
        {
            var g = Game;
            return g != null && g.molecule.GetAtoms().TryGetValue(h, out var atom) ? atom.atomType : null;
        }

        private void Begin(EditKind kind, HexIndex hex)
        {
            _edit = kind;
            _editTicks = 1;
            _editHex = hex;
            _atomBefore = AtomAt(hex);
            _errorBefore = ErrorText();
        }

        private void Place()
        {
            if (Game == null || _edit != EditKind.None) return;
            Begin(EditKind.Place, _cursor);
            MouseClick.At(HexPosition(_cursor));
        }

        private void Remove()
        {
            if (Game == null || _edit != EditKind.None || ReferenceEquals(AtomAt(_cursor), null)) return;
            Begin(EditKind.Remove, _cursor);
            MouseClick.At(HexPosition(_cursor), right: true);
        }

        // A bond of the selected type toward a neighbour; the same bond again removes it.
        private void Bond(HexDir dir)
        {
            var g = Game;
            if (g == null || _edit != EditKind.None) return;
            var other = Hex.Step(_cursor, dir);
            if (!OnBoard(other)) return;
            var existing = g.molecule.GetBondTypeAt(_cursor, other);
            Begin(EditKind.Bond, _cursor);
            _editOther = other;
            _editDir = dir;
            _bondBefore = existing;
            var mid = (HexPosition(_cursor) + HexPosition(other)) / 2f;
            MouseClick.At(mid, right: existing != BondTypeEnum.None && existing == g.selectedBondTypeEnum);
        }

        private void Resolve()
        {
            var kind = _edit;
            _edit = EditKind.None;
            var g = Game;
            if (g == null) return;
            string feedback = null;
            switch (kind)
            {
                case EditKind.Place:
                    feedback = Describe(_editHex) ?? Strings.EditorNoChange;
                    break;
                case EditKind.Remove:
                    feedback = ReferenceEquals(AtomAt(_editHex), null) && !ReferenceEquals(_atomBefore, null) ? Strings.EditorRemoved(ToolName(_atomBefore)) : Strings.EditorNoChange;
                    break;
                case EditKind.Bond:
                    var after = g.molecule.GetBondTypeAt(_editHex, _editOther);
                    string dir = Hex.Word(_editDir);
                    if (after == _bondBefore) feedback = Strings.EditorNoChange;
                    else if (after == BondTypeEnum.None) feedback = Strings.EditorRemoved(BondName(_bondBefore) + " " + dir);
                    else feedback = BondName(after) + " " + dir;
                    break;
            }
            if (feedback != null) Speech.Say(feedback, true);
            string error = ErrorText();
            if (error != _errorBefore) Speech.Say(error ?? Strings.MolNoErrors);
        }

        // The game's error line, as it draws it ("Error: ..."), or null.
        private string ErrorText()
        {
            var g = Game;
            if (g == null || !g.errorMessage.HasValue()) return null;
            return GameText.Translate("Error") + ": " + GameText.Of(g.errorMessage.GetValue());
        }

        private void Save()
        {
            var g = Game;
            if (g == null) return;
            string error = ErrorText();
            if (error != null) { Speech.Say(error, true); return; }
            Press("Save");
        }

        private static void Press(string english)
        {
            if (!ButtonPress.Press(Translations.Translate(english).GetTranslated()))
                CoreLog.Warning("[molecule editor] the game did not draw a '" + english + "' button last frame.");
        }
    }
}
