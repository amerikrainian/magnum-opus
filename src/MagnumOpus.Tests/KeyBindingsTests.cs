using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Input;
using Xunit;

namespace MagnumOpus.Tests
{
    // The mod's key rebinding: only changes against the registered defaults are saved, a saved form
    // replaces an action's keys, protected actions keep one, and conflicts exist only where both
    // actions work.
    public class KeyBindingsTests : IDisposable
    {
        // A key combination by name ("Ctrl+M"), saved as "fake:Ctrl+M".
        private sealed class Key : InputBinding
        {
            private readonly string _name;
            public Key(string name) => _name = name;
            public override string DisplayName => _name;
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => "fake";
            public override string Serialize() => _name;
        }

        // An instruction's key: the key alone on the tape, only with Alt on the board.
        private sealed class Instr : InputBinding
        {
            private readonly string _name;
            public Instr(string name) => _name = name;
            public override string DisplayName => _name;
            public override IEnumerable<string> Combos => new[] { _name, "Shift+" + _name, "Alt+" + _name };
            public override IEnumerable<string> CombosIn(string scope) => scope == "board" ? new[] { "Alt+" + _name } : Combos;
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => "instr";
            public override string Serialize() => _name;
        }

        // A kind that cannot be saved: its action is not rebindable.
        private sealed class Unsaved : InputBinding
        {
            public override string DisplayName => "E";
            public override bool JustPressed() => false;
            public override bool Held() => false;
            public override bool Released() => false;
            public override string Type => "unsaved";
            public override string Serialize() => "E";
        }

        private string _saved;

        public KeyBindingsTests()
        {
            InputManager.Clear();
            KeyBindings.Clear();
            InputBinding.RegisterType("fake", data => new Key(data));
            InputBinding.RegisterType("instr", data => new Instr(data));
            InputManager.Register("mod.menu", "Mod menu", InputCategory.Global).AddBinding(new Key("Ctrl+Shift+M"));
            InputManager.Register("ui.back", "Back", InputCategory.UI).AddBinding(new Key("Escape"));
            InputManager.Register("ed.details", "Details", InputCategory.UI).In("board", "tray").AddBinding(new Key("S"));
            InputManager.Register("ed.hex", "Hex", InputCategory.UI).In("board").AddBinding(new Key("Q"));
            InputManager.Register("ed.products", "Products", InputCategory.UI).In("board", "tape").AddBinding(new Key("Alt+P"));
            InputManager.Register("ed.select", "Select", InputCategory.UI).In("tape").AddBinding(new Key("Shift+Left"));
            InputManager.Register("ed.instr.grab", "Grab", InputCategory.UI).In("tape", "board").AddBinding(new Instr("F"));
            InputManager.Register("other", "Other", InputCategory.UI).AddBinding(new Unsaved());
            KeyBindings.Snapshot();
            KeyBindings.Persist = s => _saved = s;
        }

        public void Dispose()
        {
            KeyBindings.Clear();
            InputManager.Clear();
        }

        private static InputAction A(string key) => InputManager.Find(key);

        [Fact]
        public void Only_changed_actions_are_saved_and_a_return_to_the_defaults_saves_nothing()
        {
            KeyBindings.Add(A("ed.details"), new Key("F3"));
            Assert.Equal("ed.details=fake:S fake:F3", _saved);
            Assert.Equal(new[] { "ed.details" }, KeyBindings.Changed.Select(a => a.Key));
            KeyBindings.Reset(A("ed.details"));
            Assert.Equal("", _saved);
            Assert.True(KeyBindings.IsDefault(A("ed.details")));
            Assert.Empty(KeyBindings.Changed);
        }

        [Fact]
        public void A_saved_form_replaces_the_keys_and_an_empty_list_unbinds()
        {
            KeyBindings.Load("ed.details=fake:Ctrl+C;ed.hex=");
            Assert.Equal("Ctrl+C", A("ed.details").BindingsDisplay);
            Assert.Empty(A("ed.hex").Bindings);
            Assert.Equal("ed.details=fake:Ctrl+C;ed.hex=", KeyBindings.Format());
            Assert.Equal(2, KeyBindings.Changed.Count);
        }

        [Fact]
        public void A_protected_action_keeps_its_defaults_over_an_empty_save_and_its_last_key()
        {
            KeyBindings.Load("mod.menu=");
            Assert.Equal("Ctrl+Shift+M", A("mod.menu").BindingsDisplay);
            Assert.False(KeyBindings.Remove(A("mod.menu"), A("mod.menu").Bindings[0]));
            Assert.Single(A("mod.menu").Bindings);
            KeyBindings.Add(A("mod.menu"), new Key("F10"));
            Assert.True(KeyBindings.Remove(A("mod.menu"), A("mod.menu").Bindings[0]));
            Assert.Equal("F10", A("mod.menu").BindingsDisplay);
        }

        [Fact]
        public void Unknown_actions_survive_a_save_and_unreadable_keys_are_skipped()
        {
            KeyBindings.Load("gone.action=fake:X;ed.details=nope:Z fake:V");
            Assert.Equal("V", A("ed.details").BindingsDisplay);
            KeyBindings.Add(A("ed.hex"), new Key("Y"));
            Assert.Equal("ed.details=fake:V;ed.hex=fake:Q fake:Y;gone.action=fake:X", _saved);
        }

        [Fact]
        public void A_kind_that_cannot_be_saved_is_not_rebindable_and_a_save_cannot_touch_it()
        {
            Assert.False(KeyBindings.IsRebindable(A("other")));
            Assert.True(KeyBindings.IsRebindable(A("ed.details")));
            KeyBindings.Load("other=fake:Q");
            Assert.Equal("E", A("other").BindingsDisplay);
        }

        [Fact]
        public void Conflicts_exist_only_where_both_actions_work()
        {
            // Both on the board.
            Assert.Equal("ed.hex", KeyBindings.Conflict(A("ed.details"), new Key("Q"))?.Key);
            // Everywhere meets everything.
            Assert.Equal("ui.back", KeyBindings.Conflict(A("ed.hex"), new Key("Escape"))?.Key);
            Assert.Equal("mod.menu", KeyBindings.Conflict(A("ed.hex"), new Key("Ctrl+Shift+M"))?.Key);
            Assert.Equal("ed.hex", KeyBindings.Conflict(A("mod.menu"), new Key("Q"))?.Key);
            // Tape only against board and tray only: no scope in common, so only an overlap.
            Assert.Null(KeyBindings.Conflict(A("ed.select"), new Key("S")));
            Assert.Equal(new[] { "ed.details" }, KeyBindings.Overlaps(A("ed.select"), new Key("S")).ConvertAll(a => a.Key));
            Assert.Same(A("ed.hex").Bindings[0], KeyBindings.Existing(A("ed.hex"), new Key("Q")));
        }

        [Fact]
        public void An_instruction_key_collides_by_what_it_answers_in_each_place()
        {
            // On the board an instruction key is only Alt + it, so the board's bare Q does not matter...
            Assert.Null(KeyBindings.Conflict(A("ed.instr.grab"), new Instr("Q")));
            // ...but Alt + it does, and on the tape so does Shift + it.
            Assert.Equal("ed.products", KeyBindings.Conflict(A("ed.instr.grab"), new Instr("P"))?.Key);
            Assert.Equal("ed.select", KeyBindings.Conflict(A("ed.instr.grab"), new Instr("Left"))?.Key);
            // A bare key elsewhere meets the instruction's own key on the tape.
            Assert.Equal("ed.instr.grab", KeyBindings.Conflict(A("ed.select"), new Key("F"))?.Key);
        }

        [Fact]
        public void Reset_all_restores_every_action_and_forgets_unknown_entries()
        {
            KeyBindings.Load("gone.action=fake:X;ed.details=fake:V;ed.hex=");
            KeyBindings.ResetAll();
            Assert.Equal("S", A("ed.details").BindingsDisplay);
            Assert.Equal("Q", A("ed.hex").BindingsDisplay);
            Assert.Equal("", _saved);
            Assert.Empty(KeyBindings.Changed);
        }

        [Fact]
        public void The_saved_form_parses_entries_in_order()
        {
            var entries = KeyBindings.Parse(" a.b=fake:Ctrl+M  x:y ;;c=;bad");
            Assert.Equal(2, entries.Count);
            Assert.Equal("a.b", entries[0].Key);
            Assert.Equal(new List<(string, string)> { ("fake", "Ctrl+M"), ("x", "y") }, entries[0].Value);
            Assert.Equal("c", entries[1].Key);
            Assert.Empty(entries[1].Value);
        }
    }
}
