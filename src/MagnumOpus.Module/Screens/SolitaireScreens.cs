using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// Sigmar's Garden (SolitaireScreen: the campaign's solitaire, its Quintessence variant from the
    /// journal, the House Colvan variant), on the editor's hex model:
    ///  0. Story (the panel left of the board; none in the Colvan variant): the lines reached so far,
    ///     with the game's divider after each finished section ("Win 1 game to continue
    ///     (Optional)"), the last one where the wins stop it. Enter on any of them shows the next
    ///     line, as Space does (the game's own key, which reaches it everywhere); each new line is
    ///     spoken as it appears. The game deals the first hand only once the first section is read.
    ///  1. Board: a hex cursor over the game's 91 hexes (Q E A D Z C, numpad 7 9 4 6 1 3; Alt+Shift
    ///     with them skips to the next hex that reads differently; Ctrl+Home the centre; a key that
    ///     would leave the board does nothing, user rule 2026-11-07). Coordinates are the editor's half-step x, y, centred on the
    ///     board's middle hex (the game's (5, 0)). A hex reads "selected" first when the game has it
    ///     selected, then the coordinates, then the marble and "locked" where the game dims it (not
    ///     free: fewer than three contiguous empty neighbours, or a metal with a lower metal left).
    ///     Enter picks the marble the game's way, a left click on it (<see cref="MouseClick"/>), and
    ///     says what came of it: "selected", "unselected", "cleared Fire, Salt". , and . go to the
    ///     previous and next free marble in reading order (the game shows which are free by dimming
    ///     the rest; no pairs are suggested, user rule 2026-11-07). X reads the neighbours.
    ///  2. Marbles: the counter strip under the board, in the game's order for the variant, each
    ///     "Salt, 4", with "warning" where the game draws the count in its warning colour.
    ///  3. New Game (the game's button), Rules (its icon button, the game's click code: it opens
    ///     SolitaireHelpScreen, <see cref="SolitaireHelpGameScreen"/>), the wins, Close.
    /// Opening with a game in play lands on the board and speaks the story so far; with none (before
    /// the first deal, after a win, the only times the story waits for a press) it lands on the
    /// story's latest item instead (user decision 2026-11-07: the story its own stop, so Enter on
    /// the board only ever picks). "dealt" when a deal's reveal finishes, "board cleared" and the
    /// wins on a win. Escape passes to the game (its close button).
    /// </summary>
    public sealed class SolitaireGameScreen : GameScreen<SolitaireScreen>
    {
        private const string StopBoard = "solitaire.board";
        private const string StopStory = "solitaire.story";
        private static readonly HexIndex Centre = new HexIndex(5, 0);
        private const int SkipLimit = 10;

        private HexIndex _cursor = Centre;
        private int _opened;      // the game screen this model last opened on (its identity hash)
        private int _storySpoken; // story lines already spoken
        private string _phase;
        private string _lastError;

        // A pick waiting for the game frame that runs it: the board and the selection before it.
        private Dictionary<HexIndex, AtomType> _before;
        private List<HexIndex> _selectedBefore;
        private HexIndex _picked;
        private int _pickTicks;

        public override string Key => "game.solitaire";

        public override string ScreenName
        {
            get
            {
                var g = Game;
                if (g == null) return null;
                return GameText.Translate(g.solitaireType == SolitaireType.Quintessence ? "Sigmar's Garden II" : "Sigmar's Garden");
            }
        }

        public override bool AllowsTypeahead => false;

        public override object InitialFocusStop => LandsOnStory() ? StopStory : StopBoard;

        // No game in play and a story to read: the story waits for the player then, nothing else does.
        private bool LandsOnStory()
        {
            var g = Game;
            var s = State();
            return g != null && s != null && !s.IsGameplay() && StoryItems(g).Count > 0;
        }

        // Space advances the story panel in the game (Escape closes the screen).
        public override bool PassKeyToGame(int sdlKeycode) => sdlKeycode == GameKeys.Escape || sdlKeycode == GameKeys.Space;

        // ---- the game's state, read live ----

        private SolitaireState State() => Game?.GetState();

        // The marbles on the board: during play, and while a deal is being revealed (the game draws
        // and dims them then too).
        private SolitaireGameState Marbles()
        {
            var s = State();
            if (s == null) return null;
            if (s.IsGameplay()) return s.ToGameplay().gameState;
            if (s.IsRevealNewGame()) return s.ToRevealNewGame().currentState;
            return null;
        }

        private string Phase()
        {
            var s = State();
            if (s == null) return null;
            if (s.IsGameplay()) return "play";
            if (s.IsRevealNewGame()) return "reveal";
            if (s.IsWonLastGame()) return "won";
            return "wait";
        }

        private static bool OnBoard(HexIndex h) => SolitaireScreen.boardHexes.Contains(h);

        private static string Coords(HexIndex h) => Hex.Coords(h.Q - Centre.Q, h.R - Centre.R);

        // ---- building ----

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;

            var story = StoryItems(g);
            if (story.Count > 0)
            {
                b.BeginStop(StopStory);
                b.PushContext(Strings.SolitaireStory, null, positions: false);
                for (int i = 0; i < story.Count; i++)
                {
                    int index = i;
                    var id = ControlId.Structural("solitaire:story:" + i);
                    b.AddItem(id, new NodeVtable
                    {
                        ControlType = ControlTypes.Text,
                        Announcements = new[] { Nodes.LabelPart(() => { var items = Game == null ? null : StoryItems(Game); return items != null && index < items.Count ? items[index] : ""; }) },
                        OnActivate = () => GameKeys.Forced = GameKeys.Space,
                    });
                    if (i == story.Count - 1) b.SetStopStart(id);
                }
                b.PopContext();
            }

            b.BeginStop(StopBoard);
            b.PushContext(Strings.SolitaireBoard, null, positions: false);
            b.AddItem(ControlId.Structural("solitaire:board"), new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new[] { Nodes.LabelPart(() => Readout(_cursor)) },
                OnActivate = Pick,
            });
            b.PopContext();

            b.BeginStop("solitaire.counts");
            b.PushContext(Strings.SolitaireCounts, null); // no role word (user rule 2026-11-07)
            foreach (var type in CounterOrder(g.solitaireType))
            {
                var t = type;
                b.AddItem(ControlId.Structural("solitaire:count:" + t.byteId), new NodeVtable
                {
                    ControlType = ControlTypes.Text, // no role word (user rule 2026-11-07)
                    Announcements = new[] { Nodes.LabelPart(() => Counter(t)) },
                    SearchText = () => Board.AtomName(t),
                });
            }
            b.PopContext();

            b.BeginStop("solitaire.buttons");
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("solitaire:new"), Nodes.Button(() => GameText.Translate("New Game"), NewGame,
                enabled: () => Game != null && Game.CanGenerateNewGame()));
            b.AddItem(ControlId.Structural("solitaire:rules"), Nodes.Button(() => Strings.SolitaireRules, OpenRules,
                enabled: () => Game != null && Game.CanOpenDiagram()));
            b.AddItem(ControlId.Structural("solitaire:wins"), Nodes.Text(() => Game == null ? "" : Strings.SolitaireWins(GameText.Translate("Wins"), Game.solitaireStat)));
            b.AddItem(ControlId.Structural("solitaire:close"), Nodes.Button(() => Strings.ModClose, () => GameKeys.Forced = GameKeys.Escape));
            b.PopContext();
        }

        private bool BoardFocused => Equals(Navigation.FocusedStopKey, StopBoard);

        protected override IEnumerable<ElementAction> OwnActions()
        {
            if (!BoardFocused || Game == null) yield break;
            yield return new ElementAction(EditorActions.HexNW, Strings.Get("bind.ed.hex.nw"), _ => Move(HexDir.NorthWest));
            yield return new ElementAction(EditorActions.HexNE, Strings.Get("bind.ed.hex.ne"), _ => Move(HexDir.NorthEast));
            yield return new ElementAction(EditorActions.HexW, Strings.Get("bind.ed.hex.w"), _ => Move(HexDir.West));
            yield return new ElementAction(EditorActions.HexE, Strings.Get("bind.ed.hex.e"), _ => Move(HexDir.East));
            yield return new ElementAction(EditorActions.HexSW, Strings.Get("bind.ed.hex.sw"), _ => Move(HexDir.SouthWest));
            yield return new ElementAction(EditorActions.HexSE, Strings.Get("bind.ed.hex.se"), _ => Move(HexDir.SouthEast));
            yield return new ElementAction(EditorActions.SkipNW, Strings.Get("bind.ed.skip.nw"), _ => Skip(HexDir.NorthWest));
            yield return new ElementAction(EditorActions.SkipNE, Strings.Get("bind.ed.skip.ne"), _ => Skip(HexDir.NorthEast));
            yield return new ElementAction(EditorActions.SkipW, Strings.Get("bind.ed.skip.w"), _ => Skip(HexDir.West));
            yield return new ElementAction(EditorActions.SkipE, Strings.Get("bind.ed.skip.e"), _ => Skip(HexDir.East));
            yield return new ElementAction(EditorActions.SkipSW, Strings.Get("bind.ed.skip.sw"), _ => Skip(HexDir.SouthWest));
            yield return new ElementAction(EditorActions.SkipSE, Strings.Get("bind.ed.skip.se"), _ => Skip(HexDir.SouthEast));
            yield return new ElementAction(EditorActions.Origin, Strings.Get("bind.sol.center"), _ => { _cursor = Centre; SayHex(); });
            yield return new ElementAction(EditorActions.ItemPrev, Strings.Get("bind.sol.free_prev"), _ => NextFree(-1));
            yield return new ElementAction(EditorActions.ItemNext, Strings.Get("bind.sol.free_next"), _ => NextFree(1));
            yield return new ElementAction(EditorActions.Neighbours, Strings.Get("bind.ed.neighbours"), _ => Speech.Say(Neighbours(), true));
        }

        // ---- reading ----

        // What is on a hex, without its coordinates: the marble, "locked" where the game dims it.
        private string Contents(SolitaireGameState marbles, HexIndex h)
        {
            if (marbles == null || !marbles.atoms.TryGetValue(h, out var type)) return "";
            string name = Board.AtomName(type);
            return marbles.CanRemove(h) ? name : name + ", " + Strings.StateLocked;
        }

        private string Readout(HexIndex h)
        {
            var marbles = Marbles();
            var parts = new List<string>();
            if (marbles != null && marbles.selectedAtoms.Contains(h)) parts.Add(Strings.SolitaireSelected);
            parts.Add(Coords(h));
            string contents = Contents(marbles, h);
            if (contents.Length > 0) parts.Add(contents);
            return string.Join(", ", parts);
        }

        private void SayHex() => Speech.Say(Readout(_cursor), true);

        private string Neighbours()
        {
            var marbles = Marbles();
            var lines = new List<string>();
            foreach (var dir in Hex.All)
            {
                var h = Hex.Step(_cursor, dir);
                string c = Contents(marbles, h);
                if (c.Length > 0) lines.Add(Strings.EditorNeighbour(Hex.Word(dir), c));
            }
            return string.Join("; ", lines);
        }

        // The game's counter strip (SolitaireScreen.RenderCounters), per variant.
        private static IEnumerable<AtomType> CounterOrder(SolitaireType type)
        {
            yield return AtomTypes.salt;
            yield return AtomTypes.air;
            yield return AtomTypes.fire;
            yield return AtomTypes.water;
            yield return AtomTypes.earth;
            if (type == SolitaireType.Normal) yield return AtomTypes.quicksilver;
            else if (type == SolitaireType.Quintessence) yield return AtomTypes.quintessence;
            yield return AtomTypes.lead;
            yield return AtomTypes.tin;
            yield return AtomTypes.iron;
            yield return AtomTypes.copper;
            yield return AtomTypes.silver;
            yield return AtomTypes.gold;
        }

        // "Salt, 4", with the game's warning colour said: an odd count of salt, or of a cardinal
        // element counted with the quintessence (SolitaireScreen.RenderAtomCounter).
        private string Counter(AtomType type)
        {
            var marbles = Marbles();
            int n = marbles == null ? 0 : marbles.atoms.Values.Count(a => a == type);
            int quintessence = marbles == null ? 0 : marbles.atoms.Values.Count(a => a == AtomTypes.quintessence);
            string text = Strings.SolitaireCount(Board.AtomName(type), n);
            bool warning = (type.isGlassy && (n + quintessence) % 2 == 1) || (type == AtomTypes.salt && n % 2 == 1);
            return warning ? text + ", " + Strings.SolitaireWarning : text;
        }

        // ---- moving ----

        private void Move(HexDir dir)
        {
            var next = Hex.Step(_cursor, dir);
            if (!OnBoard(next)) return; // the edge: nothing (user rule 2026-11-07)
            _cursor = next;
            SayHex();
        }

        // To the next hex that way that reads differently; none before the edge: the last hex
        // there; already at the edge: nothing.
        private void Skip(HexDir dir)
        {
            var marbles = Marbles();
            string here = Contents(marbles, _cursor);
            var last = _cursor;
            for (int i = 1; i <= SkipLimit; i++)
            {
                var h = Hex.Step(_cursor, dir, i);
                if (!OnBoard(h)) break;
                last = h;
                if (Contents(marbles, h) == here) continue;
                _cursor = h;
                SayHex();
                return;
            }
            if (last == _cursor) return;
            _cursor = last;
            SayHex();
        }

        // , and .: the previous or next free marble in reading order (rows top to bottom, each left
        // to right); wraps; the only one, already on it: nothing (the Tab rule).
        private void NextFree(int step)
        {
            var marbles = Marbles();
            if (marbles == null) return;
            var free = marbles.atoms.Keys.Where(marbles.CanRemove)
                .OrderByDescending(h => h.R).ThenBy(h => 2 * h.Q + h.R).ToList();
            if (free.Count == 0) return;
            int at = free.IndexOf(_cursor);
            if (at >= 0 && free.Count < 2) return;
            int next;
            if (at >= 0) next = ((at + step) % free.Count + free.Count) % free.Count;
            else
            {
                // From an empty or locked hex: the first one after (or before) it in reading order.
                int Key(HexIndex h) => -h.R * 100 + (2 * h.Q + h.R);
                int here = Key(_cursor);
                next = step > 0 ? free.FindIndex(h => Key(h) > here) : free.FindLastIndex(h => Key(h) < here);
                if (next < 0) next = step > 0 ? 0 : free.Count - 1;
            }
            _cursor = free[next];
            SayHex();
        }

        // ---- acting ----

        // Enter: the game's left click on the marble under the cursor, during play. A marble the
        // game dims does nothing there: "locked" is said instead.
        private void Pick()
        {
            var s = State();
            if (s == null || !s.IsGameplay()) return;
            var marbles = s.ToGameplay().gameState;
            if (!marbles.atoms.ContainsKey(_cursor)) return;
            if (!marbles.CanRemove(_cursor)) { Speech.Say(Strings.StateLocked, true); return; }
            _before = new Dictionary<HexIndex, AtomType>(marbles.atoms);
            _selectedBefore = new List<HexIndex>(marbles.selectedAtoms);
            _picked = _cursor;
            _pickTicks = 1;
            MouseClick.At(ScreenPosition(_cursor));
        }

        // Where the game draws a hex's marble (SolitaireScreen.RenderFrame and its renderer's
        // GetScreenPosition): the window centred on the screen, the board's origin inside it.
        private static Vector2 ScreenPosition(HexIndex h)
        {
            var size = new Vector2(1516f, 922f);
            var window = (global::InputManager.screenSize / 2f - size / 2f + new Vector2(-2f, -11f)).Rounded();
            return HexGrid.solitaireGrid.ToPixelCoords(h, window + new Vector2(687f, 506f));
        }

        // What the click did, read once the game frame that ran it is over: marbles gone, or the
        // selection changed.
        private void ResolvePick()
        {
            var before = _before;
            var selectedBefore = _selectedBefore;
            _before = null;
            _selectedBefore = null;
            var s = State();
            if (s == null || before == null) return;
            var after = s.IsGameplay() ? s.ToGameplay().gameState.atoms : new Dictionary<HexIndex, AtomType>();
            var selectedAfter = s.IsGameplay() ? s.ToGameplay().gameState.selectedAtoms : new List<HexIndex>();

            // The order the game took them in: the selection first, the clicked marble last.
            var removed = selectedBefore.Where(h => before.ContainsKey(h) && !after.ContainsKey(h)).ToList();
            if (!after.ContainsKey(_picked) && before.ContainsKey(_picked) && !removed.Contains(_picked)) removed.Add(_picked);
            if (removed.Count > 0)
            {
                Speech.Say(Strings.SolitaireCleared(string.Join(", ", removed.Select(h => Board.AtomName(before[h])))), true);
                return;
            }
            var parts = new List<string>();
            bool nowSelected = selectedAfter.Contains(_picked), wasSelected = selectedBefore.Contains(_picked);
            if (nowSelected && !wasSelected) parts.Add(Strings.SolitaireSelected);
            if (!nowSelected && wasSelected) parts.Add(Strings.SolitaireUnselected);
            var dropped = selectedBefore.Where(h => h != _picked && !selectedAfter.Contains(h)).ToList();
            if (dropped.Count > 0) parts.Add(Strings.SolitaireDropped(string.Join(", ", dropped.Select(h => Board.AtomName(before[h])))));
            if (parts.Count > 0) Speech.Say(string.Join(", ", parts), true);
            else CoreLog.Warning("[solitaire] a pick at " + _picked.Q + ", " + _picked.R + " changed nothing");
        }

        private void NewGame()
        {
            if (!ButtonPress.Press(Translations.Translate("New Game").GetTranslated()))
                CoreLog.Warning("[solitaire] the game did not draw a 'New Game' button last frame.");
        }

        // The rules page: the game's icon button has its own builder, out of ButtonPress's reach,
        // so its click code runs here as the game has it.
        private void OpenRules()
        {
            var g = Game;
            if (g == null || !g.CanOpenDiagram()) return;
            GameLogic.instance.PushScreen(new SolitaireHelpScreen(g.solitaireType));
            Assets.sounds.click_button.PlaySound(1f);
        }

        // ---- the story ----

        // The story panel as the game draws it here (StoryPanel.RenderVignette with the solitaire's
        // sections): each reached line "Speaker: line", and after a finished section its divider,
        // "{0} to continue (Optional)", where the panel stops until the wins reach the section's
        // count. None without a vignette (the Colvan variant).
        private static List<string> StoryItems(SolitaireScreen g)
        {
            var panel = g?.storyPanel;
            if (panel == null || !panel.vignette.HasValue()) return new List<string>();
            return StoryPanelNodes.Read(panel, SolitaireScreen.storyPanelSections, g.solitaireStat).Items;
        }

        // ---- lifecycle ----

        public override void OnPush()
        {
            base.OnPush();
            _phase = Phase();
            _before = null;
        }

        // Opening (not coming back from the rules page over it): the story lines, after the landing.
        public override void OnFocus()
        {
            base.OnFocus();
            var g = Game;
            if (g == null) return;
            int id = RuntimeHelpers.GetHashCode(g);
            var lines = StoryItems(g);
            _storySpoken = lines.Count;
            if (id == _opened) return;
            _opened = id;
            _cursor = Centre;
            // Landing on the story reads its latest item; on the board, the story so far is spoken.
            if (LandsOnStory()) return;
            foreach (var line in lines)
                if (!string.IsNullOrWhiteSpace(line)) Speech.Say(line, interrupt: false);
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            try
            {
                var g = Game;
                if (g == null) return;
                if (_before != null && _pickTicks-- <= 0) ResolvePick();

                // A story line the player advanced to (Space), or one the game shows by itself.
                var lines = StoryItems(g);
                if (lines.Count < _storySpoken) _storySpoken = lines.Count; // the save's progress went back
                for (int i = _storySpoken; i < lines.Count; i++)
                    if (!string.IsNullOrWhiteSpace(lines[i])) Speech.Say(lines[i], interrupt: false);
                _storySpoken = Math.Max(_storySpoken, lines.Count);

                string phase = Phase();
                if (phase == _phase) return;
                string was = _phase;
                _phase = phase;
                if (was == "reveal" && phase == "play")
                {
                    _cursor = Centre;
                    Speech.Say(Strings.SolitaireDealt, interrupt: false);
                }
                else if (phase == "won")
                {
                    Speech.Say(Strings.SolitaireWon, interrupt: false);
                    Speech.Say(Strings.SolitaireWins(GameText.Translate("Wins"), g.solitaireStat), interrupt: false);
                }
            }
            catch (Exception e)
            {
                // Once per distinct failure: this runs every frame.
                if (e.Message != _lastError) CoreLog.Warning("[solitaire] update: " + e);
                _lastError = e.Message;
            }
        }
    }

    /// <summary>
    /// Sigmar's Garden's rules page (SolitaireHelpScreen, from the solitaire's Rules button): its
    /// three sections as the game lays them out, top row left to right, then the bottom: Clearing
    /// Marbles, Unlocking Marbles (with the note that off-board spaces count as empty), Matching
    /// Combinations (the cardinals, salt, vitae and mors, the metals; the House Colvan variant's
    /// own metals rule and no vitae and mors). The pictures between the texts are not described.
    /// Then Close (the game's close button, which answers Escape).
    /// </summary>
    public sealed class SolitaireHelpGameScreen : GameScreen<SolitaireHelpScreen>
    {
        public override string Key => "game.solitaire_help";

        public override string ScreenName => Strings.SolitaireRules;

        public override void Build(GraphBuilder b)
        {
            var g = Game;
            if (g == null) return;
            b.BeginStop("solitaire.rules");
            Section(b, "clearing", "Clearing Marbles", SolitaireHelpScreen.hintClearing);
            Section(b, "unlocking", "Unlocking Marbles", SolitaireHelpScreen.hintUnlocking, SolitaireHelpScreen.field_4500);
            if (g.type == SolitaireType.Colvan)
                Section(b, "matching", "Matching Combinations", SolitaireHelpScreen.hintCardinals, SolitaireHelpScreen.hintSalt, SolitaireHelpScreen.hintMetalsColvan);
            else
                Section(b, "matching", "Matching Combinations", SolitaireHelpScreen.hintCardinals, SolitaireHelpScreen.hintSalt,
                    SolitaireHelpScreen.hintVitaeMors, SolitaireHelpScreen.hintMetals);
            b.BeginStop("solitaire.rules.close");
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("solitaire-rules:close"), Nodes.Button(() => Strings.ModClose, () => GameKeys.Forced = GameKeys.Escape));
            b.PopContext();
        }

        // A heading the game draws (in capitals) over its texts: the heading is the context, each
        // text a line.
        private static void Section(GraphBuilder b, string key, string englishHeading, params LocString[] texts)
        {
            b.PushContext(GameText.Translate(englishHeading), null, positions: false);
            for (int i = 0; i < texts.Length; i++)
            {
                var text = texts[i];
                b.AddItem(ControlId.Structural("solitaire-rules:" + key + ":" + i), Nodes.Text(() => GameText.Clean(text)));
            }
            b.PopContext();
        }
    }
}
