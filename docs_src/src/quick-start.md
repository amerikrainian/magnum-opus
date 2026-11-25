# Quick Start

## Getting around

- Tab / Shift+Tab move between a screen's stops, arrows within one, Enter activates. Escape backs out.
- F1 lists the keys that work where you are; Enter on one runs it.
- Ctrl+Shift+M opens the mod menu: settings, and every key, each one rebindable.

## The board

- The solution editor's board is a hex grid. Each hex reads x, y: y counts rows, up the screen is positive; x counts hexes east. Rows sit half a hex apart, so from 0, 0 the hex to the northeast is 0.5, 1 and the one to the northwest is -0.5, 1.
- Six keys move the cursor: Q northwest, E northeast, A west, D east, Z southwest, C southeast. If you like, you may also use the numpad: 7, 9, 4, 6, 1, 3.
- Alt+Shift with a hex key skips along that line to the next hex that reads differently. Ctrl+Home goes to 0, 0.
- Arms sit on a base hex and reach out to one gripper or more. Arms are numbered as the program numbers its rows: arm 1 is row 1.
- Reagents appear on their input hexes. A product is done when a molecule exactly matching it sits on its hexes. Glyphs work on the atoms over them: bonding, transmutation and so on.
- Grippers pass through anything, but atoms collide with atoms and with arm bases, and two arm bases on tracks collide too. Two grippers can't pull one molecule two ways at once.

## Reading the board

- A hex reads its coordinates, then the atom on it, then the parts.
- A track hex reads its joins with the move that goes there. For example: "Track, +northeast, -southeast" means that if an arm were to be told to execute the move + instruction, it would move northeast.
- S reads the details of what's under the cursor; W the molecule there (size, composition, bonds, who holds it); X the six neighbours.
- [ / ] choose a category (Marks, Reagents, Products, Arms, Glyphs, Tracks, Atoms), , / . go through its items, moving the cursor there. Backslash switches to stationary view with the objects telling you how far they are from your cursor position. You can press slash (/) to jump to them.
- M marks the hex under the cursor; Ctrl+1 to Ctrl+9 jump to a mark. Shift+M clears the mark here, Alt+Shift+M all of them. Marks last until you leave the solution.
- Ctrl+Shift+S reads the score, Alt+P each product's count, Ctrl+Shift+T the puzzle's notes.

## Building

- The Parts stop is the game's toolbar. Enter on a part picks up a new one at the board cursor.
- On the board, Enter picks up the part under the cursor and puts it down again. While you carry it, it follows the cursor; Escape puts it back where it was.
- Ctrl+Enter picks up a copy.
- Shift+A / Shift+D rotate counter-clockwise / clockwise round the cursor hex. Shift+W / Shift+S make an arm longer / shorter. Delete removes.
- Where several parts share a hex, Ctrl+Up / Ctrl+Down choose which one these keys act on.
- Tracks: a new track starts drawing when you put it down; Shift+Enter extends an existing one from its end. Hex keys lay track or take it back; Enter finishes.
- Selecting several parts: Shift+Space on one corner, then on the opposite corner, selects everything in that rectangle. Ctrl+Shift+Space adds or removes the part under the cursor, Ctrl+A selects every part, Escape clears. With the cursor on a selected part, the keys above act on the whole selection, and Ctrl+C / Ctrl+X / Ctrl+V copy, cut and paste it.

## Writing a program

- The Program stop is a grid: one row per arm, one column per cycle. Every arm runs one instruction per cycle, all at the same time.
- Up / Down choose the arm, Left / Right the cycle, Home / End the arm's first / last instruction. Digits 1 to 9 jump to an arm. These work on the board too, so you can program without leaving it.
- The game's instruction keys type at the cursor and step on, overwriting: on QWERTY, A / D rotate counter-clockwise / clockwise, Q / E pivot, W / S extend / retract, F / R grab / drop, G / T move along a track, C reset, V repeat, X period override, B halt. They follow the game's Keyboard Layout option.
- Shift with an instruction key inserts, pushing the rest along. Delete clears the cell, Backspace the one before.
- On the board, Alt with an instruction key places it for the program cursor's arm at its cycle, wherever the board cursor is.
- Shift+arrows select a rectangle of cells, Ctrl+Shift+A all of them; Ctrl+C / Ctrl+X / Ctrl+V copy, cut and paste. Ctrl+Shift+Left / Right move the arm's whole program, or the selection, a cycle.
- Alt+Backspace jumps from the board to the program on the arm under the cursor, and back.
- The Instructions stop is the game's tray: Enter places that instruction at the program cursor, S reads its description.
- Ctrl+Z / Ctrl+Y undo and redo.

## Running and debugging

- Space plays and pauses, Ctrl+Space steps one cycle, Alt+Space fast forwards. Enter on a program cell runs to that cycle.
- The Run log stop keeps everything that was said during the run.

## Production puzzles

- Only the hexes inside the chambers exist; the cursor stops at a chamber's edge.
- Page Up / Page Down go to the previous / next chamber's centre.
