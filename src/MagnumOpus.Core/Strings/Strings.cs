using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace MagnumOpus.Core.Strings
{
    /// <summary>
    /// Central table for text the MOD itself authors and speaks (never game content, which is read live
    /// and already localized). Every authored word lives in <see cref="Defaults"/> as a key and its
    /// English value; typed accessors read through the loaded translation, so a lang/&lt;language&gt;.txt
    /// file overrides any value at speak time. Word order lives in "{0}"-style templates, never in code.
    /// Values are spoken by a screen reader: terse, lowercase unless shown otherwise, no decorative
    /// punctuation. Keys follow the wotr-access locale vocabulary (role.*, state.*, nav.*, screen.*).
    /// </summary>
    public static class Strings
    {
        private static KeyValuePair<string, string> D(string key, string value)
            => new KeyValuePair<string, string>(key, value);

        /// <summary>Every authored string, in template order: the key a translation file addresses and
        /// its English default.</summary>
        internal static readonly KeyValuePair<string, string>[] Defaults =
        {
            // Spoken once when the mod finishes loading; {0} = the mod version.
            D("app.loaded", "Magnum Opus {0} loaded"),
            // Spoken at launch instead when the feature module fails to load (the mod is then dead).
            D("app.module_failed", "Magnum Opus features failed to load"),
            // Follows the loaded line when the newest release outranks the running build; {0} = that
            // newer version. Up to date (or ahead, a dev build) stays silent.
            D("app.update_available", "update {0} available"),
            // Spoken when the focus-mode toggle engages / releases our keyboard navigation.
            D("app.focus_on", "navigation on"),
            D("app.focus_off", "navigation off"),
            // The game build is newer than the mod's name mappings and some game members could not be
            // matched; {0} = how many. Features that use them stay silent until a mapping arrives.
            D("app.unmatched", "{0} game members could not be matched, some features may not work"),
            // The game build is newer than the mod and no community mapping is installed: the mod
            // matched the new build against the one it was made for.
            D("app.names_matched", "the game has updated since this mod version; using automatically matched names"),
            // The community mapping for the new build is being fetched in the background.
            D("app.mapping_downloading", "downloading names for the new game build"),
            // The community has not published a mapping for this build yet; tried again next launch.
            D("app.mapping_not_published", "no names published for this game build yet; matched names stay in use"),
            // The download failed (no network, GitHub unreachable); tried again next launch.
            D("app.mapping_failed", "could not download names for the new game build; trying again next launch"),
            // The game is on a Steam branch other than community-edition (the one the mod follows).
            D("app.wrong_branch", "Opus Magnum is not on the community-edition branch, the one Magnum Opus supports; switch it in the game's Steam properties, under Game Versions and Betas"),
            // A mapping for the new game build arrived (downloaded) and the mod reloaded onto it.
            D("app.mapping_updated", "mod updated for the new game build"),
            // The boot gate (the game waits for a click before its first screen).
            D("app.press_any_key", "Press any key to continue"),

            // Control role words, spoken after the label ("Continue, button").
            D("role.button", "button"),
            D("role.toggle", "toggle"),
            D("role.slider", "slider"),
            D("role.radio button", "radio button"),
            D("role.combo box", "combo box"),
            D("role.tab", "tab"),
            D("role.key binding", "key binding"),
            D("role.item", "item"),
            D("role.heading", "heading"),
            D("role.list", "list"),
            D("role.table", "table"),
            D("role.link", "link"),
            D("role.edit", "edit"),
            D("role.dialog", "dialog"),

            // Control states.
            D("state.disabled", "disabled"),
            D("state.selected", "selected"),
            D("state.on", "on"),
            D("state.off", "off"),
            D("state.expanded", "expanded"),
            D("state.collapsed", "collapsed"),
            D("value.blank", "blank"),
            D("value.space", "space"),
            D("edit.begin", "editing"),
            D("edit.selected", "{0} selected"),
            D("edit.unselected", "{0} unselected"),
            // The mod's own number entry: the decimal point as typed, a character erased with Backspace.
            D("edit.point", "point"),
            D("edit.deleted", "{0} deleted"),

            // Navigation feedback. {0} = index, {1} = count.
            D("nav.position", "{0} of {1}"),
            D("nav.no_tooltip", "no description"),
            D("nav.no_details", "no details"),
            D("drag.no_target", "nothing to drag"),
            D("delete.no_target", "nothing to remove"),
            D("nav.minimum", "minimum"),
            D("nav.maximum", "maximum"),

            // Type-ahead search. {0} = the typed text.
            D("search.no_match", "no match for {0}"),
            D("search.cleared", "search cleared"),

            // Input action labels (for the key help). Short imperative phrases.
            D("bind.ui.up", "Navigate up"),
            D("bind.ui.down", "Navigate down"),
            D("bind.ui.left", "Navigate left"),
            D("bind.ui.right", "Navigate right"),
            D("bind.ui.next", "Next control group"),
            D("bind.ui.prev", "Previous control group"),
            D("bind.ui.activate", "Activate"),
            D("bind.ui.secondary", "Secondary action"),
            D("bind.ui.back", "Back"),
            D("bind.ui.home", "Jump to first"),
            D("bind.ui.end", "Jump to last"),
            D("bind.ui.tooltip", "Read description"),
            D("bind.ui.regionPrev", "Previous section"),
            D("bind.ui.regionNext", "Next section"),
            D("bind.ui.readFocus", "Read current control"),
            D("bind.mod.focus", "Toggle navigation"),
            D("bind.mod.menu", "Mod menu"),
            // The key help (F1): every key that does something right now.
            D("bind.mod.help", "Keys here"),

            // Screen names, spoken on entry when the game gives the screen no title of its own.
            D("screen.confirm", "Confirm"),
            D("screen.credits", "Credits"),
            D("screen.error", "Error"),
            D("screen.help", "Keys here"),
            D("help.none", "no keys here"),
            // A key help row: {0} = what the key does, {1} = the key(s).
            D("help.row", "{0}: {1}"),

            // The mod menu (Magnum Opus's own settings).
            D("screen.mod_menu", "Magnum Opus"),
            D("mod.settings", "Settings"),
            D("mod.key_help", "All keys"),
            D("mod.close", "Close"),
            D("mod.speak_positions", "Speak list positions"),
            D("mod.focus_on_launch", "Keyboard navigation on at launch"),
            D("mod.short_directions", "Abbreviate directions"),
            // The all-keys list's category headings; {0} = the category.
            D("mod.key_category", "{0} keys"),
            D("mod.category.navigation", "Navigation"),
            D("mod.category.board", "Board"),
            D("mod.category.program", "Program"),
            D("mod.category.instructions", "Instruction"),
            D("mod.category.editor", "Editor"),
            // The mod's key rebinding (the mod menu's All keys). {0} = the action.
            D("rebind.prompt", "Press a key for {0}, Escape cancels"),
            // {0} = the key, {1} = another action that has it elsewhere.
            D("rebind.conflict", "{0} is also {1}"),
            D("keys.add", "Add key"),
            // An action's reset; read "Reset to default: Ctrl+M" with its default keys.
            D("keys.reset", "Reset to default"),
            D("keys.reset_all", "Reset all keys"),
            D("keys.reset_all.ask", "Reset all keys to their defaults?"),
            D("keys.reset_all.done", "All keys reset"),
            D("keys.yes", "Yes"),
            D("keys.no", "No"),
            D("keys.none", "No keys"),
            D("keys.replace", "Replace"),
            D("keys.remove", "Remove"),
            // {0} = the key.
            D("keys.removed", "Removed {0}"),
            // Why a protected action's last key cannot be removed (the mod menu, Enter, Escape, Up, Down).
            D("keys.needed", "its last key, cannot be removed"),
            // {0} = the action's keys after the reset.
            D("keys.reset.done", "Reset to {0}"),
            D("keys.cancelled", "Cancelled"),
            // The key being replaced pressed again; {0} = the key.
            D("keys.kept", "Kept {0}"),
            // Refused: {0} = the key, {1} = the action that has it.
            D("keys.taken", "{0} is already {1}, press another key"),
            // {0} = the new key.
            D("keys.bound", "Bound to {0}"),
            // Refused: an instruction's key is one key alone; Shift inserts and Alt programs from the board.
            D("bind.ed.tutorial", "Repeat the tutorial step"),
            D("tutorial.stop", "Tutorial"),
            D("tutorial.restart", "Restart tutorial"),
            // Lesson: Introduction. {0} repeat, {1} key help, {2}-{7} the hex keys NW NE W E SW SE.
            D("tut.intro.move", "Welcome! We figured we'll start you off easy and let you watch this machine to learn the ropes. If you need to repeat this information, hit {0} to do so. If you get confused, {1} is your friend. Now, what you're really here for: the board is a field of hexes. Move with {2} <dir:2>, {3} <dir:1>, {4} <dir:3>, {5} <dir:0>, {6} <dir:4> and {7} <dir:5>."),
            // {0} neighbours.
            D("tut.intro.neighbours", "A hex says its coordinates, then what is on it. {0} reads what is on the hexes around you."),
            // {0} the game's note, {1} {2} category keys, {3} {4} item keys.
            D("tut.intro.reagent", "{0} {1} and {2} choose a kind of thing on the board, and {3} and {4} go through them. Choose Reagents and go to one."),
            // {0} molecule.
            D("tut.intro.molecule", "{0} reads the whole molecule on this hex."),
            // {0} the game's note.
            D("tut.intro.product", "{0} Use the same keys to go to the product."),
            // {0} arm 1's key.
            D("tut.intro.arm", "The digits jump to an arm. {0} goes to arm 1: its hex says its number, type, direction and length."),
            // {0} the game's note, {1} jump.
            D("tut.intro.tape", "{0} Each arm has a row of them, its tape. {1} goes between an arm on the board and its tape."),
            // {0} {1} cycles, {2} {3} arms.
            D("tut.intro.cells", "Each cell of the tape is one cycle. {0} and {1} go through the cycles, {2} and {3} change arms."),
            // {0} step.
            D("tut.intro.step", "Now run it. {0} advances one cycle from anywhere, and says the cycle, then what each arm and glyph did. Press it three times."),
            // {0} back.
            D("tut.intro.stop", "{0} stops the machine and puts everything back."),
            // {0} the game's note, {1} play.
            D("tut.intro.play", "{0} {1} plays and pauses. While it plays, each cycle's speech cuts off the last one's."),
            // {0} fast forward.
            D("tut.intro.fast", "{0} fast forwards without all the spam!"),
            // Lesson: Arms. {0} the game's note, {1} {2} category keys, {3} next item.
            D("tut.arms.goal", "{0} Some hexes are highlighted: find them with {1} and {2}, under Highlighted, then {3}. Those with arms on them are finished. Go to the empty one."),
            // {0} activate.
            D("tut.arms.carry", "In Parts, {0} on the arm picks it up at the board cursor."),
            // {0} {1} rotate, {2} {3} length, {4} put down, {5} cancel.
            D("tut.arms.place", "You are holding the arm. The hex keys move it and say when it cannot go there. {0} and {1} turn it, {2} and {3} change its length, {4} puts it down and {5} cancels. Now place it on that empty highlighted hex."),
            // {0} the reagent's hex, {1} {2} rotate, {3} activate.
            D("tut.arms.reach", "The gripper has to be on the reagent, at {0}. By default, the arm starts out facing <dir:0>, but you can rotate it as we've just described with {1} and {2}. On the board the same keys adjust it, and {3} picks it up again."),
            D("tut.arms.reach.after", "The gripper is over the reagent."),
            // {0} jump, {1} {2} arms.
            D("tut.arms.tape", "Now its program. {0} on the arm goes to its tape, and {1} and {2} change arms there."),
            // {0} the game's note, {1} grab's key.
            D("tut.arms.grab", "{0} Each instruction has a key, and typing one moves on a cycle. {1} is grab: place one."),
            // {0} jump.
            D("tut.arms.board", "You can program from the board too. Back on the board with {0}, Alt with an instruction's key adds it at this tape's cursor, wherever the board cursor is. Try one."),
            // {0} delete, {1} backspace, {2} undo.
            D("tut.arms.edit", "To correct a program: {0} clears a cell, {1} clears the one before the cursor, and {2} undoes anything."),
            // {0} next stop, {1} details.
            D("tut.arms.list", "{0} to Instructions lists every instruction with its key, and {1} describes one."),
            // {0} key help.
            D("tut.arms.finish", "The rest of the program is yours. Remember: if you get lost, {0} is your handy reference for what you can do!"),
            // Lesson: Pivots. {0} arm 1's key.
            D("tut.pivots.meet", "Back for more! This lesson brings in a new move, the pivot, and {0} jumps to arm 1, who already knows it."),
            // {0} jump, {1} {2} cycles.
            D("tut.pivots.tape", "Have a look at its program: {0} goes to its tape, and {1} and {2} walk through it."),
            // {0} next stop, {1} details.
            D("tut.pivots.describe", "Wondering what a pivot actually does? {0} over to Instructions, find one, and {1} spells it out."),
            // {0} step.
            D("tut.pivots.watch", "Enough reading, let's see it. Hit {0} three times."),
            D("tut.pivots.held", "That arm still has its molecule in hand. Go find its gripper with the hex keys, and it'll tell you what it's holding."),
            // {0} back.
            D("tut.pivots.stop", "Good stuff. {0} to stop it, same as ever."),
            // {0} the game's note, {1} {2} category keys.
            D("tut.pivots.goal", "Your turn! {0} You know the drill: {1} and {2} to Highlighted, then go to the empty one."),
            // {0} activate.
            D("tut.pivots.carry", "In Parts, {0} on the arm picks it up, same as last time."),
            // {0} molecule.
            D("tut.pivots.reagent", "Before you program it, size up the job: go to a reagent and hit {0} to hear what it's made of."),
            D("tut.pivots.program", "You're all set! Remember to pivot!"),
            // Lesson: Pistons.
            D("tut.pistons.meet", "Pistons! These arms can stretch and pull back. As per usual, arm 1 has an example for you to look at."),
            // {0} jump, {1} home, {2} end.
            D("tut.pistons.tape", "As per usual, {0} goes to its tape. We're sure you already know, but {1} and {2} jump to the first and last instruction on said tape, respectively."),
            // {0} the game's notes on reset, {1} {2} cycles.
            D("tut.pistons.reset", "{0} A reset takes up cells of its own, and they read as what it's undoing: walk along with {1} and {2} to hear them."),
            // {0} the game's note, {1} {2} category keys.
            D("tut.pistons.goal", "Your turn! {0} You know the drill: {1} and {2} to Highlighted, then go to the empty one."),
            // {0} activate.
            D("tut.pistons.carry", "In Parts, {0} on the piston arm picks it up, same as last time."),
            D("tut.pistons.program", "Get a couple of instructions on its tape."),
            D("tut.pistons.insert", "Forgot something? Shift with an instruction's key slips one in and pushes the rest along."),
            // {0} {1} shift the program.
            D("tut.pistons.shift", "Whole thing off by a cycle? {0} and {1} slide it along."),
            D("tut.pistons.done", "You're all set!"),
            // Lesson: Tracks. {0} arm 1's key.
            D("tut.tracks.meet", "Tracks! An arm on one can slide along these. Check our usual culprit {0}"),
            D("tut.tracks.read", "Walk along the track with the hex keys. Each hex tells you which way a plus move and a minus move go from there."),
            // {0} arm 1's key, {1} {2} part keys.
            D("tut.tracks.parts", "An arm and a track can share a hex. {0} goes back to arm 1's, and {1} and {2} choose which one your edits take."),
            D("tut.tracks.goal", "Your turn!"),
            // {0} activate.
            D("tut.tracks.carry", "In Parts, {0} on the track picks it up, same as last time."),
            // {0} activate.
            D("tut.tracks.draw", "{0} puts it down and you're drawing: the hex keys lay track from there, going back over it takes it up, and {0} ends it."),
            // {0} extend, {1} activate.
            D("tut.tracks.extend", "Need more later? {0} on a track picks the drawing back up, and {1} ends it again."),
            // {0} the game's note.
            D("tut.tracks.arm", "{0}"),
            // {0} {1} the move instructions' keys.
            D("tut.tracks.done", "You're all set! Arms move along tracks with {0} and {1}."),
            // Lesson: Transmutation. {0} {1} category keys, {2} next item.
            D("tut.transmute.meet", "Transmutation! Glyphs change the atoms that land on them. {0} and {1} to Glyphs, then {2} to go to one."),
            // {0} details.
            D("tut.transmute.details", "Remember, {0} tells you what it does."),
            // {0} diagram.
            D("tut.transmute.diagram", "Not sure what turns into what? {0} brings up the game's diagram, though you don't need to look at it for this puzzle."),
            D("tut.transmute.parts", "Your turn!"),
            // {0} undo, {1} redo.
            D("tut.transmute.redo", "Changed your mind about changing your mind? {0} undoes, and {1} puts it back."),
            // {0} products.
            D("tut.transmute.counts", "{0} tells you how many of each product you've made so far."),
            D("tut.transmute.done", "You're all set!"),
            // Lesson: Bonding.
            D("tut.bonding.meet", "Bonding! Drop two atoms on a glyph of bonding and they stick together. As per usual, arm 1 has an example."),
            // {0} activate.
            D("tut.bonding.runto", "If you want to skip to a particular instruction, {0} on a cell with an instruction runs the machine right to it."),
            D("tut.bonding.parts", "Your turn!"),
            D("tut.bonding.program", "Get a couple of instructions on a tape."),
            D("tut.bonding.select", "Want the same thing twice? Shift with the arrows marks cells on a tape."),
            // {0} copy, {1} cut, {2} paste.
            D("tut.bonding.copy", "{0} copies them, {1} cuts them, and {2} pastes them at the cursor."),
            D("tut.bonding.done", "You're all set!"),
            // Lesson: Stabilized Water.
            D("tut.water.parts", "Last one! From now on you're responsible for laying out products, reagents and parts. Head over to the parts list to see what you're producing."),
            // {0} molecule.
            D("tut.water.viewer", "{0} on a reagent or product opens it up before you commit."),
            // {0} neighbours, {1} {2} rotate.
            D("tut.water.turn", "In here the hex keys walk it, {0} reads what's around you, and {1} and {2} turn it to see it from another angle."),
            // {0} back.
            D("tut.water.close", "{0} closes it."),
            // {0} activate.
            D("tut.water.place", "Put them on the board: {0} picks one up, same as any part."),
            // {0} clone.
            D("tut.water.copy", "Need another of something? {0} on a part picks up a copy."),
            // {0} mark a corner, {1} select all, {2} back.
            D("tut.water.select", "Moving a bunch at once? {0} marks a corner and then the opposite one, and {1} takes everything. {2} clears it."),
            // {0} score.
            D("tut.water.score", "{0} tells you how your machine's doing."),
            D("tut.water.done", "You're all set!"),
            D("keys.instruction_bare", "An instruction takes a key without Ctrl, Shift or Alt, press another key"),
            D("mod.category.global", "Global"),
            D("mod.category.ui", "UI"),
            D("mod.category.game", "Game"),

            // Campaign item kinds and states on the puzzle select (the game shows them as icons and
            // check boxes only).
            D("item.puzzle", "puzzle"),
            D("item.cutscene", "cutscene"),
            D("item.letter", "letter"),
            D("item.solitaire", "solitaire"),
            D("state.completed", "completed"),
            D("state.locked", "locked"),
            // The chapter gems, as a list.
            D("select.chapters", "Chapters"),
            // {0} = the chapter's title ("Chapter 1"), {1} = its name.
            D("select.chapter", "{0}, {1}"),
            D("select.journal", "Journal"),
            D("journal.issue", "Issue"),
            D("journal.previous", "Previous issue"),
            D("journal.next", "Next issue"),
            // The campaign switch; {0} = the other campaign's name.
            D("select.switch_campaign", "Switch to {0}"),
            D("select.dlc_not_owned", "not owned, opens the store page"),
            D("campaign.opus_magnum", "Opus Magnum"),
            // The unlock sequence the game plays on return to the map; {0} = the item or chapter.
            D("select.unlocked", "{0} unlocked"),
            D("select.completed", "{0} completed"),
            D("select.chapter_unlocked", "new chapter: {0}"),
            // The puzzle info screen's list of saved solutions.
            D("info.solutions", "Solutions"),

            // ---- the solution editor ----
            // A hex's coordinates, Civ's half-step style: {0} = x (in hex widths, halves allowed),
            // {1} = y (the row, growing up the screen). Origin = the game's own (0, 0).
            D("hex.coords", "{0}, {1}"),
            // The six hex directions, in the game's rotation order (counter-clockwise from east).
            D("hex.dir.0", "east"),
            D("hex.dir.1", "northeast"),
            D("hex.dir.2", "northwest"),
            D("hex.dir.3", "west"),
            D("hex.dir.4", "southwest"),
            D("hex.dir.5", "southeast"),
            // The same six when the mod menu's Abbreviate directions is on. Any value may name a
            // direction with <dir:N> (N = the number above), which follows that setting.
            D("hex.short.0", "e"),
            D("hex.short.1", "ne"),
            D("hex.short.2", "nw"),
            D("hex.short.3", "w"),
            D("hex.short.4", "sw"),
            D("hex.short.5", "se"),
            // An offset of zero.
            D("hex.here", "here"),
            // A run of steps one way: {0} = count, {1} = direction ("3 east").
            D("hex.steps", "{0} {1}"),
            // The editor's Tab stops.
            D("editor.board", "Board"),
            D("editor.program", "Program"),
            D("editor.log", "Run log"),
            // An arm (a programmable part) by its program row number.
            D("editor.arm", "arm {0}"),
            // An arm's base hex: {0} = row number, {1} = the game's part name, {2} = the direction
            // it points, {3} = its length.
            D("editor.arm_base", "arm {0}, {1}, {2}, length {3}"),
            // A wheel's hex: {0} = its tape row, {1} = its name, {2} = its atoms; one atom: {0} = atom, {1} = direction.
            D("editor.wheel_base", "arm {0}, {1}, {2}"),
            D("editor.wheel_atom", "{0} {1}"),
            // An arm's gripper hex; closed = holding on while the engine runs.
            D("editor.grip", "arm {0} grip"),
            D("editor.grip_closed", "arm {0} grip, closed"),
            // An atom's bonds as directions; triplex = the three-colour bonds.
            D("editor.bonds", "bonds {0}"),
            D("editor.triplex", "triplex bonds {0}"),
            // Bonds some of whose three kinds are still missing; one entry: {0} = direction, {1} = kinds it has.
            D("editor.triplex_partial", "partial triplex bonds {0}"),
            D("editor.triplex_partial_dir", "{0}, {1} of 3"),
            // The arm holding an atom.
            D("editor.held_by", "held by arm {0}"),
            // A tape cell: {0} = column, {1} = instruction.
            D("editor.cell", "{0}, {1}"),
            // A cycle of the run.
            D("editor.cycle", "cycle {0}"),
            // A board category and how many things it holds.
            D("editor.category", "{0}, {1}"),
            D("editor.cat.reagents", "Reagents"),
            D("editor.cat.products", "Products"),
            D("editor.cat.arms", "Arms"),
            D("editor.cat.glyphs", "Glyphs"),
            D("editor.cat.tracks", "Tracks"),
            D("editor.cat.atoms", "Atoms"),
            D("editor.cat.highlights", "Highlighted"),
            // Whether , and . move the board cursor to an item (Backslash).
            D("editor.snap_on", "Auto-snap on"),
            D("editor.snap_off", "Auto-snap off"),
            // A hex the game marks in a lesson.
            D("editor.hex_highlighted", "highlighted"),
            // One kind of atom in that summary: {0} = how many, {1} = the atom.
            D("editor.atom_count", "{0} {1}"),
            D("editor.molecule_bond_one", "1 bond"),
            D("editor.molecule_bonds", "{0} bonds"),
            D("editor.molecule_triplex_one", "1 triplex bond"),
            D("editor.molecule_triplex", "{0} triplex bonds"),
            D("editor.molecule_triplex_partial_one", "1 partial triplex bond"),
            D("editor.molecule_triplex_partial", "{0} partial triplex bonds"),
            // A repeating product: {0} = the atoms of one repeating unit; {0} of the second = the offset between units.
            D("editor.molecule_repeating", "repeating, {0}"),
            D("editor.molecule_repeats_every", "every {0}"),
            // The hex of a repeating product where its next unit would join.
            D("editor.repeats_here", "repeats here"),
            // A product's outline on a hex while the engine runs: {0} = the atom it wants, with bonds.
            D("editor.product_wants", "wants {0}"),
            // A neighbour of the cursor: {0} = direction, {1} = what is there.
            D("editor.neighbour", "{0}: {1}"),
            // A score figure: {0} = the game's label, {1} = the value.
            D("editor.score_item", "{0} {1}"),
            // The gold mark after a cost (the game draws it as a small G).
            D("editor.gold_mark", "g"),
            // The editor's close button out after a first Escape (a second one closes the editor).
            D("editor.close_armed", "close?"),
            // A tape cell filled by a Reset or Repeat: {0} = what runs there, {1} = the source.
            D("editor.expansion", "{0}, from {1}"),
            // A run log line: {0} = cycle, {1} = the event.
            D("editor.log_entry", "{0}: {1}"),
            // The engine's state, spoken when it changes.
            D("run.running", "running"),
            D("run.fast", "fast forward"),
            D("run.paused", "paused"),
            D("run.stopped", "stopped"),
            // An error the game shows: {0} = its message, {1} = where.
            D("run.error", "{0} at {1}"),
            D("run.error_places", "{0} and {1}"),
            // Instruction names (the game names both directions of a pair alike).
            D("instr.grab", "grab"),
            D("instr.drop", "drop"),
            D("instr.rotate_cw", "clockwise"),
            D("instr.rotate_ccw", "counter"),
            D("instr.extend", "extend"),
            D("instr.retract", "retract"),
            D("instr.pivot_cw", "pivot clockwise"),
            D("instr.pivot_ccw", "pivot counter"),
            D("instr.move_plus", "move plus"),
            D("instr.move_minus", "move minus"),
            D("instr.reset", "reset"),
            D("instr.repeat", "repeat"),
            D("instr.halt", "halt"),
            D("instr.period_override", "period override"),
            D("instr.wait", "wait"),
            // Run events, worded as what happened. An arm's sentence: {0} = row, {1} = its actions.
            D("event.arm", "arm {0}: {1}"),
            // A part's sentence (a glyph, a reagent): {0} = its name, {1} = what happened there.
            D("event.actor", "{0}: {1}"),
            D("event.grabbed", "grabbed {0}"),
            D("event.grabbed_nothing", "grabbed nothing"),
            D("event.dropped", "dropped {0}"),
            D("event.dropped_nothing", "released"),
            D("event.clockwise", "clockwise"),
            D("event.counter", "counter"),
            D("event.pivot_clockwise", "pivot clockwise"),
            D("event.pivot_counter", "pivot counter"),
            D("event.extended", "extended"),
            D("event.retracted", "retracted"),
            // An arm carried along its track: {0} = direction.
            D("event.moved", "moved {0}"),
            D("event.halted", "halted"),
            // A move made more than once in a cycle: {0} = the move, {1} = times.
            D("event.times", "{0} {1} times"),
            D("event.appeared", "{0} appeared"),
            D("event.removed", "{0} removed"),
            D("event.transmuted", "{0} became {1}"),
            D("event.bonded", "bonded {0} and {1}"),
            // A triplex glyph adding one of the bond's three kinds: {2} = how many it has now.
            D("event.triplex_partial", "{0} and {1}, {2} of 3"),
            D("event.triplex_bonded", "triplex bonded {0} and {1}"),
            D("event.unbonded", "unbonded {0} and {1}"),
            // An atom and its hex: {0} = atom, {1} = coordinates.
            D("event.atom_at", "{0} at {1}"),
            // Atoms of one kind in a molecule: {0} = count, {1} = atom.
            D("event.count", "{0} {1}"),
            // A product completed: {0} = product, {1} = done, {2} = required.
            D("event.product", "{0}: {1} of {2}"),

            // Editing the solution.
            D("editor.tray", "Parts"),
            // The instruction tray beside the tape.
            D("editor.instruction_tray", "Instructions"),
            // A multi-armed assembly (the game names them all alike): {0} = name, {1} = grippers.
            D("editor.with_grippers", "{0} ({1} grippers)"),
            // A histogram bar: {0} / {1} = its score range, {2} = its share of players (and a tag).
            D("histogram.bin", "{0} to {1}: {2}"),
            D("histogram.bin_one", "{0}: {1}"),
            D("histogram.percent", "{0}%"),
            // A drawn bar whose share rounds to 0.
            D("histogram.tiny", "under 1%"),
            // The bar the game's marker line stands over (this solution's score).
            D("histogram.now", "now"),
            // Puzzle info's markers: the selected solution's bar and the best score's bar.
            D("histogram.this", "this"),
            D("histogram.best", "best"),
            // A tray instruction the puzzle does not permit / one the solution may hold only once.
            D("editor.not_available", "not available"),
            D("editor.limit_reached", "limit reached"),
            D("editor.placed", "placed {0}"),
            D("editor.cancelled", "cancelled"),
            // {0} = what went: a part's name, or instructions.
            D("editor.deleted", "deleted {0}"),
            // A track turned (its shape is its hexes, it has no one direction).
            D("editor.rotated", "rotated"),
            D("editor.length", "length {0}"),
            D("editor.chamber", "chamber {0}"),
            D("editor.conduit_to", "{0}, to {1}"),
            // Drawing a track from its end: {0} = its hexes so far.
            D("editor.drawing", "drawing track, {0} hexes"),
            D("editor.track_done", "track, {0} hexes"),
            D("editor.track_hexes", "{0} hexes"),
            // A track hex's joins: {0} = the direction track plus / track minus moves an arm from here.
            D("editor.track_plus", "+{0}"),
            D("editor.track_minus", "-{0}"),
            D("editor.stop_to_edit", "Stop the engine to edit."),
            D("editor.stop_first", "Stop the engine first."),
            D("editor.run_to_empty", "no instruction here"),
            D("editor.run_to_outside", "outside the program"),
            D("program.overlaps", "overlaps {0}"),
            D("program.overlaps_at", "overlaps {0} at {1}"),
            D("program.overlap_line", "{0}, {1} at {2} overlaps {3}"),
            D("program.more_errors", "{0} more"),
            D("glyph.metal", "metal"),
            D("glyph.cardinal", "cardinal element"),
            D("glyph.centre", "centre"),
            D("glyph.in", "{0} in"),
            D("glyph.out", "{0} out"),
            D("diagram.salt", "{0}, joined to {1}"),
            D("diagram.placed", "{0} {1}"),
            D("diagram.vitae_mors", "{0} and {1}, on a line through {2}"),
            D("diagram.metals", "Metals, {0}: {1}; {2} between each"),
            D("diagram.metals_plain", "Metals: {0}; {1} between each"),
            D("diagram.extra", "{0}, {1}"),
            D("diagram.dir.right", "right"),
            D("diagram.dir.upper_right", "upper right"),
            D("diagram.dir.above", "above"),
            D("diagram.dir.upper_left", "upper left"),
            D("diagram.dir.left", "left"),
            D("diagram.dir.lower_left", "lower left"),
            D("diagram.dir.below", "below"),
            D("diagram.dir.lower_right", "lower right"),
            D("diagram.rel.right", "right of {0}"),
            D("diagram.rel.upper_right", "upper right of {0}"),
            D("diagram.rel.above", "above {0}"),
            D("diagram.rel.upper_left", "upper left of {0}"),
            D("diagram.rel.left", "left of {0}"),
            D("diagram.rel.lower_left", "lower left of {0}"),
            D("diagram.rel.below", "below {0}"),
            D("diagram.rel.lower_right", "lower right of {0}"),
            D("diagram.order.above", "bottom to top"),
            D("diagram.order.below", "top to bottom"),
            D("diagram.order.right", "left to right"),
            D("diagram.order.left", "right to left"),
            // Sigmar's Garden (the solitaire).
            D("solitaire.board", "Board"),
            D("solitaire.story", "Story"),
            // The story panel is waiting for a press to show its next line (the game's pulsing arrow).
            D("story.more", "More story"),
            // The counter strip under the board: how many of each marble are left.
            D("solitaire.counts", "Marbles"),
            D("solitaire.count", "{0}, {1}"),
            // The game draws a count in its warning colour (a salt or cardinal element that cannot
            // all be paired off).
            D("solitaire.warning", "warning"),
            // The icon button that opens the game's rules page (it has no caption).
            D("solitaire.rules", "Rules"),
            D("solitaire.wins", "{0}: {1}"),
            D("solitaire.selected", "selected"),
            D("solitaire.unselected", "unselected"),
            // Other marbles a pick dropped from the selection: {0} = their names.
            D("solitaire.dropped", "{0} unselected"),
            // Marbles a pick removed: {0} = their names.
            D("solitaire.cleared", "cleared {0}"),
            D("solitaire.dealt", "dealt"),
            D("solitaire.won", "board cleared"),
            D("bind.sol.center", "Cursor to the centre"),
            D("bind.sol.free_prev", "Previous free marble"),
            D("bind.sol.free_next", "Next free marble"),
            D("bind.sol.pick", "Pick the marble"),
            D("editor.finish_first", "Put the part down or cancel first."),
            // {0} = a part's name or "arm 2".
            D("editor.fixed", "{0} cannot be changed"),
            D("editor.cannot_copy", "{0} cannot be copied"),
            // An instruction this puzzle does not allow: {0} = its name.
            D("editor.unavailable", "{0} not available"),
            // More than three instructions, by count.
            D("editor.instruction_count", "{0} instructions"),
            // Instructions in the tape's selection: {0} = how many.
            D("editor.selected", "{0} selected"),
            D("editor.selection_cleared", "selection cleared"),
            // The board's selection. {0} = coordinates.
            D("editor.corner", "corner {0}"),
            // A marked rectangle: {0} = top-left, {1} = bottom-right.
            D("editor.range", "{0} to {1}"),
            // The rectangle's selection: {0} = the range, {1} = the parts (or "no parts").
            D("editor.marked", "{0}: {1}"),
            D("editor.no_parts", "no parts"),
            D("editor.pick_instructions", "instructions"),
            // Alt+P with no product on the board yet.
            D("editor.no_products", "no products placed"),
            // {0} = a part ("arm 2", "Glyph of Bonding") or a count of parts.
            D("editor.part_selected", "selected {0}"),
            D("editor.part_unselected", "unselected {0}"),
            // More than three parts, by count.
            D("editor.part_count", "{0} parts"),
            // A hex holding a selected part, said before its coordinates.
            D("editor.hex_selected", "selected"),
            // A group copied by Ctrl+Enter: what the game will not copy moves along: {0} = those parts.
            D("editor.also_moving", "moving {0}"),
            // A group part that cannot go where it is: {0} = the part, {1} = the game's reason.
            D("editor.part_error", "{0}: {1}"),
            D("editor.copied", "copied {0}"),
            D("editor.cut", "cut {0}"),
            D("editor.pasted", "pasted {0}"),
            // Clipboard instructions with no arm row or permission to land on: {0} = how many.
            D("editor.not_pasted", "{0} did not fit"),
            D("editor.nothing_to_undo", "nothing to undo"),
            D("editor.nothing_to_redo", "nothing to redo"),
            D("editor.no_change", "no change"),
            // More than three changes by an undo, by count.
            D("editor.changes", "{0} changes"),
            // A part and its hex: {0} = name, {1} = coordinates.
            D("editor.part_at", "{0} at {1}"),
            D("editor.removed", "removed {0}"),
            // A program cell an undo changed: {0} = arm, {1} = column, {2} = instruction.
            D("editor.cell_change", "arm {0}, {1}, {2}"),
            D("editor.cell_removed", "arm {0}, {1}, removed {2}"),

            // Key help labels of the editor's keys.
            D("bind.ed.hex.nw", "Cursor <dir:2>"),
            D("bind.ed.hex.ne", "Cursor <dir:1>"),
            D("bind.ed.hex.w", "Cursor <dir:3>"),
            D("bind.ed.hex.e", "Cursor <dir:0>"),
            D("bind.ed.hex.sw", "Cursor <dir:4>"),
            D("bind.ed.hex.se", "Cursor <dir:5>"),
            D("bind.ed.skip.nw", "Skip <dir:2>"),
            D("bind.ed.skip.ne", "Skip <dir:1>"),
            D("bind.ed.skip.w", "Skip <dir:3>"),
            D("bind.ed.skip.e", "Skip <dir:0>"),
            D("bind.ed.skip.sw", "Skip <dir:4>"),
            D("bind.ed.skip.se", "Skip <dir:5>"),
            D("bind.ed.tape.prev", "Program cursor back a cycle"),
            D("bind.ed.tape.next", "Program cursor forward a cycle"),
            D("bind.ed.tape.first", "Program cursor to the first instruction"),
            D("bind.ed.tape.last", "Program cursor to the last instruction"),
            D("bind.ed.arm", "Jump to arm"),
            D("bind.ed.arm.prev", "Previous arm to program"),
            D("bind.ed.arm.next", "Next arm to program"),
            D("bind.ed.cat.prev", "Previous category"),
            D("bind.ed.cat.next", "Next category"),
            D("bind.ed.item.prev", "Previous item"),
            D("bind.ed.item.next", "Next item"),
            D("bind.ed.item.jump", "Cursor to the item"),
            D("bind.ed.item.snap", "Toggle auto-snap to items"),
            D("bind.ed.details", "Details"),
            D("bind.ed.molecule", "Molecule"),
            D("bind.ed.neighbours", "Neighbours"),
            D("bind.ed.score", "Score"),
            D("bind.ed.tips", "Tutorial tips"),
            D("bind.ed.products", "Products"),
            D("bind.ed.play", "Play or pause"),
            D("bind.ed.step", "Step one cycle"),
            D("bind.ed.fast", "Fast forward"),
            D("bind.ed.instr_insert", "Insert an instruction"),
            D("bind.ed.shift_left", "Move instructions a cycle left"),
            D("bind.ed.shift_right", "Move instructions a cycle right"),
            // A key help row's keys: Shift held with any of the instruction keys listed above it.
            D("help.ed.instr_insert_keys", "Shift with an instruction key"),
            D("bind.ed.diagram", "Show diagram"),
            D("bind.ed.jump", "Jump between the board and the tape"),
            D("bind.ed.origin", "Cursor to the origin"),
            D("bind.ed.chamber.prev", "Previous chamber"),
            D("bind.ed.chamber.next", "Next chamber"),
            D("bind.cinematic.next", "Next line"),
            D("bind.ed.part_prev", "Previous part on this hex"),
            D("bind.ed.part_next", "Next part on this hex"),
            D("bind.ed.mark", "Mark a corner"),
            D("bind.ed.toggle_select", "Select or unselect the part here"),
            D("bind.ed.select_parts", "Select all parts"),
            D("bind.ed.delete", "Delete"),
            D("bind.ed.cancel", "Cancel"),
            D("bind.ed.clone", "Pick up a copy"),
            D("bind.ed.track", "Extend track"),
            D("bind.ed.rot.ccw", "Rotate counter-clockwise"),
            D("bind.ed.rot.cw", "Rotate clockwise"),
            D("bind.ed.len.up", "Longer arm"),
            D("bind.ed.len.down", "Shorter arm"),
            D("bind.ed.sel.up", "Select up"),
            D("bind.ed.sel.down", "Select down"),
            D("bind.ed.sel.left", "Select left"),
            D("bind.ed.sel.right", "Select right"),
            D("bind.ed.sel.all", "Select all instructions"),
            D("bind.ed.unselect", "Clear selection"),
            D("bind.ed.copy", "Copy"),
            D("bind.ed.cut", "Cut"),
            D("bind.ed.paste", "Paste"),
            D("bind.ed.undo", "Undo"),
            D("bind.ed.redo", "Redo"),
        };

        private static readonly Dictionary<string, string> _defaults = BuildDefaults();
        private static Dictionary<string, string> _overrides = new Dictionary<string, string>();

        private static Dictionary<string, string> BuildDefaults()
        {
            var d = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kv in Defaults) d[kv.Key] = kv.Value;
            return d;
        }

        /// <summary>The value for a key: the loaded translation, else the English default, else the
        /// key itself (a missing key is then audible rather than silent).</summary>
        public static string Get(string key)
        {
            if (_overrides.TryGetValue(key, out var o)) return Directions(o);
            if (_defaults.TryGetValue(key, out var v)) return Directions(v);
            CoreLog.Warning("Strings: missing key " + key);
            return key;
        }

        /// <summary>Whether direction words are spoken short ("nw"); the module points this at the
        /// player's setting.</summary>
        public static Func<bool> AbbreviateDirections = () => false;

        private static readonly Regex DirectionToken = new Regex("<dir:([0-5])>", RegexOptions.Compiled);

        // A value names a direction as <dir:N>, so its word follows the setting in every language.
        private static string Directions(string value) =>
            value.IndexOf("<dir:", StringComparison.Ordinal) < 0
                ? value
                : DirectionToken.Replace(value, m => HexDirection(m.Groups[1].Value[0] - '0'));

        /// <summary>Whether a key exists in the defaults (for optional lookups like role words).</summary>
        public static bool Has(string key) => _defaults.ContainsKey(key) || _overrides.ContainsKey(key);

        /// <summary>The value for a key with "{0}"-style slots filled.</summary>
        public static string F(string key, params object[] args)
        {
            string template = Get(key);
            try { return string.Format(template, args); }
            catch (FormatException) { return template; }
        }

        /// <summary>Install a translation, replacing any previous one whole (a language switch never
        /// blends two files): "key=value" lines split at the first '=' ("#" comments and blanks
        /// ignored, a written-out "\n" a line break); missing keys fall back to English. Pass null to
        /// return to the defaults. What the file got wrong is set aside, never applied, and comes
        /// back in the report for the caller to log: a line without a key, a key this table does not
        /// define (a typo, a file for a newer mod), an empty value (it would silence information), a
        /// value with a slot its English has not ("{2}" where the code fills two: the line would be
        /// spoken as the raw template).</summary>
        public static TranslationReport LoadTranslation(IEnumerable<string> lines)
        {
            var report = new TranslationReport();
            var map = new Dictionary<string, string>(StringComparer.Ordinal);
            if (lines != null)
            {
                int n = 0;
                foreach (var raw in lines)
                {
                    n++;
                    if (raw == null) continue;
                    string line = raw.Trim().TrimStart('\uFEFF');
                    if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal)) continue;
                    int eq = line.IndexOf('=');
                    if (eq <= 0) { report.Malformed.Add(n); continue; }
                    string key = line.Substring(0, eq).Trim();
                    string value = line.Substring(eq + 1).Trim().Replace("\\n", "\n");
                    if (!_defaults.TryGetValue(key, out var english)) { report.UnknownKeys.Add(key); continue; }
                    if (value.Length == 0) { report.EmptyKeys.Add(key); continue; }
                    if (!SlotsFit(value, english)) { report.BadSlots.Add(key); continue; }
                    map[key] = value;
                }
            }
            _overrides = map;
            report.Applied = map.Count;
            return report;
        }

        // Whether a translated template formats with the arguments its English takes: every "{n}" of
        // it one the English has, and the braces well formed.
        public static bool SlotsFit(string value, string english)
        {
            int max = -1;
            foreach (int slot in Slots(english)) if (slot > max) max = slot;
            try { string.Format(value, new object[Math.Max(max + 1, 0)]); }
            catch (FormatException) { return false; }
            return true;
        }

        /// <summary>The "{n}" slots a template uses, for the translation checks.</summary>
        public static HashSet<int> Slots(string template)
        {
            var slots = new HashSet<int>();
            if (template == null) return slots;
            for (int i = 0; i < template.Length - 2; i++)
            {
                if (template[i] != '{' || !char.IsDigit(template[i + 1])) continue;
                int j = i + 1, slot = 0;
                while (j < template.Length && char.IsDigit(template[j])) slot = slot * 10 + (template[j++] - '0');
                if (j < template.Length && (template[j] == '}' || template[j] == ':' || template[j] == ',')) slots.Add(slot);
            }
            return slots;
        }

        /// <summary>Every key of the table, in template order (the translation tests walk it).</summary>
        public static IEnumerable<string> Keys
        {
            get { foreach (var kv in Defaults) yield return kv.Key; }
        }

        /// <summary>A key's English default, or null.</summary>
        public static string Default(string key) => _defaults.TryGetValue(key, out var v) ? v : null;

        private const string TemplateHeader =
            "# Magnum Opus: the words the mod itself speaks (the game's own text is the game's).\n" +
            "# This file is the template, generated from the mod's strings table: copy it to\n" +
            "# <language>.txt, named for the game's language code (see the mod's documentation),\n" +
            "# and translate the values. key=value, one per line; keep every {0} slot (their\n" +
            "# order is free) and leave a <dir:N> as it is (the mod says a direction there); a missing key is spoken in English. The mod follows the game's language.\n";

        /// <summary>The translator template (lang/en.txt): every key with its English default, one per
        /// line, under a header that tells a translator what to do with it.</summary>
        public static string DumpTemplate()
        {
            var sb = new StringBuilder();
            sb.Append(TemplateHeader);
            foreach (var kv in Defaults)
                sb.Append(kv.Key).Append('=').Append(kv.Value.Replace("\n", "\\n")).Append('\n');
            return sb.ToString();
        }

        // ---- typed accessors ----

        public static string ModLoaded(string version) => F("app.loaded", version);
        public static string ModuleFailed => Get("app.module_failed");
        public static string UpdateAvailable(string version) => F("app.update_available", version);
        public static string FocusOn => Get("app.focus_on");
        public static string FocusOff => Get("app.focus_off");
        public static string Unmatched(int count) => F("app.unmatched", count);
        public static string MappingUpdated => Get("app.mapping_updated");
        public static string NamesMatched => Get("app.names_matched");
        public static string WrongBranch => Get("app.wrong_branch");
        public static string MappingDownloading => Get("app.mapping_downloading");
        public static string MappingNotPublished => Get("app.mapping_not_published");
        public static string MappingFailed => Get("app.mapping_failed");
        public static string PressAnyKey => Get("app.press_any_key");

        /// <summary>The role word for a control type key ("button" -> "button"); null when none.</summary>
        public static string Role(string word) => Has("role." + word) ? Get("role." + word) : null;
        public static string RoleTable => Get("role.table");
        public static string RoleList => Get("role.list");
        public static string RoleHeading => Get("role.heading");
        public static string RoleDialog => Get("role.dialog");

        public static string StateDisabled => Get("state.disabled");
        public static string StateSelected => Get("state.selected");
        public static string StateOn => Get("state.on");
        public static string StateOff => Get("state.off");
        public static string ExpandedState(bool expanded) => Get(expanded ? "state.expanded" : "state.collapsed");
        public static string ValueBlank => Get("value.blank");
        public static string ValueSpace => Get("value.space");
        public static string EditBegin => Get("edit.begin");
        public static string EditSelected(string text) => F("edit.selected", text);
        public static string EditUnselected(string text) => F("edit.unselected", text);
        public static string EditPoint => Get("edit.point");
        public static string EditDeleted(string text) => F("edit.deleted", text);

        public static string Position(int index, int count) => F("nav.position", index, count);
        public static string NoTooltip => Get("nav.no_tooltip");
        public static string NoDetails => Get("nav.no_details");
        public static string NoDragTarget => Get("drag.no_target");
        public static string NoDeleteTarget => Get("delete.no_target");
        public static string Minimum => Get("nav.minimum");
        public static string Maximum => Get("nav.maximum");

        public static string SearchNoMatch(string text) => F("search.no_match", text);
        public static string SearchCleared => Get("search.cleared");

        /// <summary>The spoken label of an input action ("bind.ui.down" -> "Navigate down"); the
        /// registration label when no entry exists.</summary>
        public static string BindLabel(string actionKey, string fallback)
        {
            // An instruction's action reads as the instruction.
            const string instruction = "ed.instr.";
            if (actionKey != null && actionKey.StartsWith(instruction, System.StringComparison.Ordinal))
                return Instruction(actionKey.Substring(instruction.Length));
            return Has("bind." + actionKey) ? Get("bind." + actionKey) : fallback;
        }

        public static string ScreenConfirm => Get("screen.confirm");
        public static string ScreenCredits => Get("screen.credits");
        public static string ScreenError => Get("screen.error");
        public static string ScreenHelp => Get("screen.help");
        public static string HelpNone => Get("help.none");
        public static string HelpRow(string label, string keys) => F("help.row", label, keys);

        public static string ScreenModMenu => Get("screen.mod_menu");
        public static string ModSettings => Get("mod.settings");
        public static string ModKeyHelp => Get("mod.key_help");
        public static string RebindPrompt(string action) => F("rebind.prompt", action);
        public static string RebindConflict(string key, string what) => F("rebind.conflict", key, what);
        public static string KeysAdd => Get("keys.add");
        public static string KeysReset => Get("keys.reset");
        public static string KeysResetAll => Get("keys.reset_all");
        public static string KeysResetAllAsk => Get("keys.reset_all.ask");
        public static string KeysResetAllDone => Get("keys.reset_all.done");
        public static string KeysYes => Get("keys.yes");
        public static string KeysNo => Get("keys.no");
        public static string KeysNone => Get("keys.none");
        public static string KeysReplace => Get("keys.replace");
        public static string KeysRemove => Get("keys.remove");
        public static string KeysRemoved(string key) => F("keys.removed", key);
        public static string KeysNeeded => Get("keys.needed");
        public static string KeysResetDone(string keys) => F("keys.reset.done", keys);
        public static string KeysCancelled => Get("keys.cancelled");
        public static string KeysKept(string key) => F("keys.kept", key);
        public static string KeysTaken(string key, string action) => F("keys.taken", key, action);
        public static string KeysBound(string key) => F("keys.bound", key);
        public static string KeysInstructionBare => Get("keys.instruction_bare");
        public static string TutorialStop => Get("tutorial.stop");
        public static string TutorialRestart => Get("tutorial.restart");
        public static string EditorHexHighlighted => Get("editor.hex_highlighted");
        public static string ModClose => Get("mod.close");
        public static string ModSpeakPositions => Get("mod.speak_positions");
        public static string ModShortDirections => Get("mod.short_directions");
        public static string EditorSnapOn => Get("editor.snap_on");
        public static string EditorSnapOff => Get("editor.snap_off");
        public static string ModFocusOnLaunch => Get("mod.focus_on_launch");

        public static string ItemKind(string kind) => Get("item." + kind);
        public static string StateCompleted => Get("state.completed");
        public static string StateLocked => Get("state.locked");
        public static string SelectChapters => Get("select.chapters");
        public static string SelectChapter(string title, string name) => F("select.chapter", title, name);
        public static string SelectJournal => Get("select.journal");
        public static string JournalIssue => Get("journal.issue");
        public static string JournalPrevious => Get("journal.previous");
        public static string JournalNext => Get("journal.next");
        public static string SwitchCampaign(string campaign) => F("select.switch_campaign", campaign);
        public static string DlcNotOwned => Get("select.dlc_not_owned");
        public static string CampaignOpusMagnum => Get("campaign.opus_magnum");
        public static string SelectUnlocked(string what) => F("select.unlocked", what);
        public static string SelectCompleted(string what) => F("select.completed", what);
        public static string SelectChapterUnlocked(string what) => F("select.chapter_unlocked", what);
        public static string InfoSolutions => Get("info.solutions");

        // ---- the solution editor ----
        public static string HexCoords(string x, string y) => F("hex.coords", x, y);
        public static string HexDirection(int dir) => Get((AbbreviateDirections() ? "hex.short." : "hex.dir.") + dir);
        public static string HexHere => Get("hex.here");
        public static string HexSteps(int count, string direction) => F("hex.steps", count, direction);
        public static string EditorBoard => Get("editor.board");
        public static string EditorProgram => Get("editor.program");
        public static string EditorLog => Get("editor.log");
        public static string EditorArm(int n) => F("editor.arm", n);
        public static string EditorArmBase(int n, string part, string direction, int length) => F("editor.arm_base", n, part, direction, length);
        public static string EditorWheelBase(int n, string part, string atoms) => F("editor.wheel_base", n, part, atoms);
        public static string EditorWheelAtom(string atom, string direction) => F("editor.wheel_atom", atom, direction);
        public static string EditorGrip(int n) => F("editor.grip", n);
        public static string EditorGripClosed(int n) => F("editor.grip_closed", n);
        public static string EditorBonds(string directions) => F("editor.bonds", directions);
        public static string EditorTriplex(string directions) => F("editor.triplex", directions);
        public static string EditorTriplexPartial(string directions) => F("editor.triplex_partial", directions);
        public static string EditorTriplexPartialDirection(string direction, int kinds) => F("editor.triplex_partial_dir", direction, kinds);
        public static string EditorHeldBy(int n) => F("editor.held_by", n);
        public static string EditorCell(int column, string instruction) => F("editor.cell", column, instruction);
        public static string EditorCycle(int n) => F("editor.cycle", n);
        public static string EditorCategory(string name, int count) => F("editor.category", name, count);
        public static string EditorCategoryName(string key) => Get("editor.cat." + key);
        public static string EditorAtomCount(int count, string atom) => F("editor.atom_count", count, atom);
        public static string EditorMoleculeBonds(int n) => n == 1 ? Get("editor.molecule_bond_one") : F("editor.molecule_bonds", n);
        public static string EditorMoleculeTriplex(int n) => n == 1 ? Get("editor.molecule_triplex_one") : F("editor.molecule_triplex", n);
        public static string EditorMoleculeTriplexPartial(int n) => n == 1 ? Get("editor.molecule_triplex_partial_one") : F("editor.molecule_triplex_partial", n);
        public static string EditorMoleculeRepeating(string atoms) => F("editor.molecule_repeating", atoms);
        public static string EditorMoleculeRepeatsEvery(string offset) => F("editor.molecule_repeats_every", offset);
        public static string EditorRepeatsHere => Get("editor.repeats_here");
        public static string EditorProductWants(string atom) => F("editor.product_wants", atom);
        public static string EditorNeighbour(string direction, string what) => F("editor.neighbour", direction, what);
        public static string EditorScoreItem(string label, string value) => F("editor.score_item", label, value);
        public static string EditorGoldMark => Get("editor.gold_mark");
        public static string EditorCloseArmed => Get("editor.close_armed");
        public static string EditorTray => Get("editor.tray");
        public static string EditorInstructionTray => Get("editor.instruction_tray");
        public static string EditorWithGrippers(string name, int n) => F("editor.with_grippers", name, n);
        public static string HistogramBin(string low, string high, string share) => F("histogram.bin", low, high, share);
        public static string HistogramBinOne(string score, string share) => F("histogram.bin_one", score, share);
        public static string HistogramPercent(int n) => F("histogram.percent", n);
        public static string HistogramTiny => Get("histogram.tiny");
        public static string HistogramNow => Get("histogram.now");
        public static string HistogramThis => Get("histogram.this");
        public static string HistogramBest => Get("histogram.best");
        public static string EditorNotAvailable => Get("editor.not_available");
        public static string EditorLimitReached => Get("editor.limit_reached");
        public static string EditorPlaced(string part) => F("editor.placed", part);
        public static string EditorCancelled => Get("editor.cancelled");
        public static string EditorDeleted(string what) => F("editor.deleted", what);
        public static string EditorRotated => Get("editor.rotated");
        public static string EditorLength(int length) => F("editor.length", length);
        public static string EditorChamber(int number) => F("editor.chamber", number);
        public static string EditorConduitTo(string name, string hex) => F("editor.conduit_to", name, hex);
        public static string EditorDrawing(int hexes) => F("editor.drawing", hexes);
        public static string EditorTrackDone(int hexes) => F("editor.track_done", hexes);
        public static string EditorTrackHexes(int hexes) => F("editor.track_hexes", hexes);
        public static string EditorStopToEdit => Get("editor.stop_to_edit");
        public static string EditorStopFirst => Get("editor.stop_first");
        public static string EditorRunToEmpty => Get("editor.run_to_empty");
        public static string EditorRunToOutside => Get("editor.run_to_outside");
        public static string ProgramOverlaps(string cycles) => F("program.overlaps", cycles);
        public static string ProgramOverlapsAt(string instruction, int cycle) => F("program.overlaps_at", instruction, cycle);
        public static string ProgramOverlapLine(string arm, string instruction, int cycle, string cycles) => F("program.overlap_line", arm, instruction, cycle, cycles);
        public static string ProgramMoreErrors(int count) => F("program.more_errors", count);
        public static string GlyphMetal => Get("glyph.metal");
        public static string GlyphCardinal => Get("glyph.cardinal");
        public static string GlyphCentre => Get("glyph.centre");
        public static string GlyphIn(string what) => F("glyph.in", what);
        public static string GlyphOut(string what) => F("glyph.out", what);
        public static string DiagramSalt(string salt, string joined) => F("diagram.salt", salt, joined);
        public static string DiagramPlaced(string name, string direction) => F("diagram.placed", name, direction);
        public static string DiagramVitaeMors(string vitae, string mors, string through) => F("diagram.vitae_mors", vitae, mors, through);
        public static string DiagramMetals(string order, string metals, string between) => F("diagram.metals", order, metals, between);
        public static string DiagramMetalsPlain(string metals, string between) => F("diagram.metals_plain", metals, between);
        public static string DiagramExtra(string text, string where) => F("diagram.extra", text, where);
        public static string DiagramDir(Text.Compass.Dir d) => Get("diagram.dir." + DirKey(d));
        public static string DiagramRelative(Text.Compass.Dir d, string anchor) => F("diagram.rel." + DirKey(d), anchor);
        // Runs go along an axis: Above, Below, Right or Left.
        public static string DiagramOrder(Text.Compass.Dir d) => Get("diagram.order." + DirKey(d));

        public static string SolitaireBoard => Get("solitaire.board");
        public static string SolitaireStory => Get("solitaire.story");
        public static string StoryMore => Get("story.more");
        public static string SolitaireCounts => Get("solitaire.counts");
        public static string SolitaireCount(string marble, int count) => F("solitaire.count", marble, count);
        public static string SolitaireWarning => Get("solitaire.warning");
        public static string SolitaireRules => Get("solitaire.rules");
        public static string SolitaireWins(string label, int wins) => F("solitaire.wins", label, wins);
        public static string SolitaireSelected => Get("solitaire.selected");
        public static string SolitaireUnselected => Get("solitaire.unselected");
        public static string SolitaireDropped(string names) => F("solitaire.dropped", names);
        public static string SolitaireCleared(string names) => F("solitaire.cleared", names);
        public static string SolitaireDealt => Get("solitaire.dealt");
        public static string SolitaireWon => Get("solitaire.won");

        private static string DirKey(Text.Compass.Dir d)
        {
            switch (d)
            {
                case Text.Compass.Dir.Right: return "right";
                case Text.Compass.Dir.UpperRight: return "upper_right";
                case Text.Compass.Dir.Above: return "above";
                case Text.Compass.Dir.UpperLeft: return "upper_left";
                case Text.Compass.Dir.Left: return "left";
                case Text.Compass.Dir.LowerLeft: return "lower_left";
                case Text.Compass.Dir.Below: return "below";
                default: return "lower_right";
            }
        }
        public static string EditorFinishFirst => Get("editor.finish_first");
        public static string EditorFixed(string what) => F("editor.fixed", what);
        public static string EditorCannotCopy(string what) => F("editor.cannot_copy", what);
        public static string EditorTrackPlus(string dir) => F("editor.track_plus", dir);
        public static string EditorTrackMinus(string dir) => F("editor.track_minus", dir);
        public static string EditorUnavailable(string what) => F("editor.unavailable", what);
        public static string EditorInstructionCount(int n) => F("editor.instruction_count", n);
        public static string EditorSelected(int n) => F("editor.selected", n);
        public static string EditorSelectionCleared => Get("editor.selection_cleared");
        public static string EditorCorner(string coords) => F("editor.corner", coords);
        public static string EditorRange(string from, string to) => F("editor.range", from, to);
        public static string EditorMarked(string range, string parts) => F("editor.marked", range, parts);
        public static string EditorNoParts => Get("editor.no_parts");
        public static string EditorPickInstructions => Get("editor.pick_instructions");
        public static string EditorNoProducts => Get("editor.no_products");
        public static string EditorPartSelected(string what) => F("editor.part_selected", what);
        public static string EditorPartUnselected(string what) => F("editor.part_unselected", what);
        public static string EditorPartCount(int n) => F("editor.part_count", n);
        public static string EditorHexSelected => Get("editor.hex_selected");
        public static string EditorAlsoMoving(string what) => F("editor.also_moving", what);
        public static string EditorPartError(string part, string error) => F("editor.part_error", part, error);
        public static string EditorCopied(string what) => F("editor.copied", what);
        public static string EditorCut(string what) => F("editor.cut", what);
        public static string EditorPasted(string what) => F("editor.pasted", what);
        public static string EditorNotPasted(int n) => F("editor.not_pasted", n);
        public static string EditorNothingToUndo => Get("editor.nothing_to_undo");
        public static string EditorNothingToRedo => Get("editor.nothing_to_redo");
        public static string EditorNoChange => Get("editor.no_change");
        public static string EditorChanges(int n) => F("editor.changes", n);
        public static string EditorPartAt(string part, string where) => F("editor.part_at", part, where);
        public static string EditorRemoved(string what) => F("editor.removed", what);
        public static string EditorCellChange(int arm, int col, string instruction) => F("editor.cell_change", arm, col, instruction);
        public static string EditorCellRemoved(int arm, int col, string instruction) => F("editor.cell_removed", arm, col, instruction);
        public static string EditorExpansion(string what, string source) => F("editor.expansion", what, source);
        public static string EditorLogEntry(int cycle, string text) => F("editor.log_entry", cycle, text);
        public static string RunRunning => Get("run.running");
        public static string RunFast => Get("run.fast");
        public static string RunPaused => Get("run.paused");
        public static string RunStopped => Get("run.stopped");
        public static string RunError(string message, string where) => F("run.error", message, where);
        public static string RunErrorPlaces(string a, string b) => F("run.error_places", a, b);
        public static string Instruction(string key) => Get("instr." + key);
        public static string EventArm(int n, string what) => F("event.arm", n, what);
        public static string EventActor(string who, string what) => F("event.actor", who, what);
        public static string EventGrabbed(string what) => F("event.grabbed", what);
        public static string EventGrabbedNothing => Get("event.grabbed_nothing");
        public static string EventDropped(string what) => F("event.dropped", what);
        public static string EventDroppedNothing => Get("event.dropped_nothing");
        public static string EventClockwise => Get("event.clockwise");
        public static string EventCounter => Get("event.counter");
        public static string EventPivotClockwise => Get("event.pivot_clockwise");
        public static string EventPivotCounter => Get("event.pivot_counter");
        public static string EventExtended => Get("event.extended");
        public static string EventRetracted => Get("event.retracted");
        public static string EventMoved(string direction) => F("event.moved", direction);
        public static string EventHalted => Get("event.halted");
        public static string EventTimes(string what, int times) => F("event.times", what, times);
        public static string EventAppeared(string atom) => F("event.appeared", atom);
        public static string EventRemoved(string atom) => F("event.removed", atom);
        public static string EventTransmuted(string from, string to) => F("event.transmuted", from, to);
        public static string EventBonded(string a, string b) => F("event.bonded", a, b);
        public static string EventTriplexPartial(string a, string b, int kinds) => F("event.triplex_partial", a, b, kinds);
        public static string EventTriplexBonded(string a, string b) => F("event.triplex_bonded", a, b);
        public static string EventUnbonded(string a, string b) => F("event.unbonded", a, b);
        public static string EventAtomAt(string atom, string where) => F("event.atom_at", atom, where);
        public static string EventCount(int count, string atom) => F("event.count", count, atom);
        public static string EventProduct(string product, int done, int required) => F("event.product", product, done, required);

        /// <summary>The all-keys heading of an input category ("Global keys").</summary>
        public static string KeyCategory(string category)
        {
            string name = "mod.category." + (category ?? "").ToLowerInvariant();
            return F("mod.key_category", Has(name) ? Get(name) : category);
        }
    }
}
