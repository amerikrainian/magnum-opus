using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Input;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.UI;
using Screen = MagnumOpus.Core.Screens.Screen;

namespace MagnumOpus.Module.Screens
{
    // The game's own InputManager (global namespace) shadows Core's otherwise.
    using InputManager = MagnumOpus.Core.Input.InputManager;

    /// <summary>Every key the mod binds, whatever the screen, as its groups ("Board keys",
    /// "Instruction keys"...), each opening its actions (<see cref="KeyGroupScreen"/>); Reset all keys
    /// ends the list. The keys that do something HERE are <see cref="HelpScreen"/>'s (F1).</summary>
    public sealed class KeyListScreen : Screen
    {
        public override string Key => "mod.keys";
        public override string ScreenName => Strings.ModKeyHelp;
        public override bool IsActive() => false; // only ever a child of the mod menu

        public override void Build(GraphBuilder b)
        {
            b.PushContext(null, null); // the screen name says "All keys"
            foreach (string name in InputManager.Actions.Select(a => a.Group).Where(g => g != null).Distinct())
            {
                string group = name;
                b.AddItem(ControlId.Structural("keys:" + group), Nodes.Button(() => Strings.KeyCategory(group), () => PushChild(new KeyGroupScreen(group))));
            }
            b.AddItem(ControlId.Structural("keys:resetall"), Nodes.Button(() => Strings.KeysResetAll, AskResetAll, () => KeyBindings.Changed.Count > 0));
            b.PopContext();
        }

        private static void AskResetAll()
        {
            // No first: focus lands on it.
            ChoiceSubmenuScreen.Open(Strings.KeysResetAllAsk, new[]
            {
                new ChoiceOption(Strings.KeysNo, null),
                new ChoiceOption(Strings.KeysYes, () =>
                {
                    KeyBindings.ResetAll();
                    Speech.Say(Strings.KeysResetAllDone, interrupt: true);
                }),
            });
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }

    /// <summary>One group's actions, "what it does: its keys". Enter on an action opens its keys to
    /// change (<see cref="ActionKeysScreen"/>).</summary>
    public sealed class KeyGroupScreen : Screen
    {
        private readonly string _group;

        public KeyGroupScreen(string group) { _group = group; }

        public override string Key => "mod.keygroup";
        public override string ScreenName => Strings.KeyCategory(_group);
        public override bool IsActive() => false; // only ever a child of the key list

        public override void Build(GraphBuilder b)
        {
            b.PushContext(null, null, positions: false); // the screen name says the group
            foreach (var action in InputManager.Actions)
            {
                if (action.Group != _group) continue;
                var a = action;
                b.AddItem(ControlId.Structural("keys:" + a.Key), Nodes.Button(() => Strings.HelpRow(a.DisplayLabel, a.BindingsDisplay), () => PushChild(new ActionKeysScreen(a))));
            }
            b.PopContext();
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }

    /// <summary>
    /// One action's keys, a child of the key group: each key (Enter: Replace or Remove), Add key, and
    /// Reset to default while the keys differ from it. Changes save at once (<see cref="KeyBindings"/>).
    /// A protected action's last key cannot be removed (Replace still changes it).
    /// </summary>
    public sealed class ActionKeysScreen : Screen
    {
        private readonly InputAction _action;

        public ActionKeysScreen(InputAction action) { _action = action; }

        public override string Key => "mod.actionkeys";
        public override string ScreenName => _action.DisplayLabel;
        public override bool IsActive() => false; // only ever a child of the key group

        public override void Build(GraphBuilder b)
        {
            b.PushContext(null, null); // the screen name says the action
            if (_action.Bindings.Count == 0)
                b.AddItem(ControlId.Structural("actionkeys:none"), Nodes.Text(() => Strings.KeysNone));
            foreach (var binding in _action.Bindings)
            {
                var bound = binding;
                b.AddItem(ControlId.Structural("actionkeys:" + bound.Type + ":" + bound.Serialize()), Nodes.Button(() => bound.DisplayName, () => Choose(bound)));
            }
            b.AddItem(ControlId.Structural("actionkeys:add"), Nodes.Button(() => Strings.KeysAdd, () => PushChild(new BindKeyScreen(_action, null))));
            b.AddItem(ControlId.Structural("actionkeys:reset"), Nodes.Button(
                () => Strings.HelpRow(Strings.KeysReset, KeyBindings.DefaultsDisplay(_action)), Reset, () => !KeyBindings.IsDefault(_action)));
            b.PopContext();
        }

        // Replace or Remove, for one key.
        private void Choose(InputBinding binding)
        {
            var self = this;
            ChoiceSubmenuScreen.Open(binding.DisplayName, new[]
            {
                new ChoiceOption(Strings.KeysReplace, () => self.PushChild(new BindKeyScreen(_action, binding))),
                new ChoiceOption(Strings.KeysRemove, () =>
                {
                    if (KeyBindings.Remove(_action, binding)) Speech.Say(Strings.KeysRemoved(binding.DisplayName), interrupt: true);
                }, enabled: KeyBindings.CanRemove(_action), detail: KeyBindings.CanRemove(_action) ? null : Strings.KeysNeeded),
            });
        }

        private void Reset()
        {
            KeyBindings.Reset(_action);
            Speech.Say(Strings.KeysResetDone(_action.BindingsDisplay), interrupt: true);
        }

        public override IEnumerable<ElementAction> GetActions()
        {
            yield return new ElementAction(ActionIds.Back, Strings.Get("bind.ui.back"), _ => ParentScreen?.RemoveChild(this));
        }
    }

    /// <summary>
    /// Waits for the new key of an action: added, or in place of <c>replacing</c>. The mod stands down
    /// meanwhile (<see cref="CapturesRawInput"/>) and the game hears nothing (GameKeys). In phases,
    /// each starting once every key is up, so the Enter that opened it is never taken and the new key
    /// never reaches the mod afterwards: waiting, listening (the first key that is not a modifier goes
    /// down: it, with the Ctrl, Shift and Alt held then, is the key; Escape alone cancels), settling.
    /// A key another action has where this one works is refused, named, and the capture listens on;
    /// one another action has only elsewhere is taken and that action named. An instruction takes a
    /// key without modifiers: Shift and Alt with it are its other forms.
    /// </summary>
    public sealed class BindKeyScreen : Screen
    {
        private enum Phase { Waiting, Listening, Settling }

        private readonly InputAction _action;
        private readonly InputBinding _replacing;
        private Phase _phase = Phase.Waiting;

        public BindKeyScreen(InputAction action, InputBinding replacing)
        {
            _action = action;
            _replacing = replacing;
        }

        public override string Key => "mod.bindkey";
        public override bool IsActive() => false; // only ever a child of an action's keys
        public override bool CapturesRawInput => true;
        public override bool AllowsTypeahead => false;
        public override string ScreenName => Strings.RebindPrompt(_action.DisplayLabel);

        public override void OnUpdate()
        {
            try { Step(); }
            catch (Exception e)
            {
                CoreLog.Warning("BindKeyScreen: " + e);
                Close();
            }
        }

        private void Step()
        {
            switch (_phase)
            {
                case Phase.Waiting:
                    if (!SdlKeyboard.AnyActive) _phase = Phase.Listening;
                    return;
                case Phase.Listening:
                    var key = SdlKeyboard.FirstPressed();
                    if (key == null) return;
                    bool ctrl = SdlKeyboard.CtrlHeld, shift = SdlKeyboard.ShiftHeld, alt = SdlKeyboard.AltHeld;
                    if (key == Scancode.Escape && !ctrl && !shift && !alt)
                    {
                        Speech.Say(Strings.KeysCancelled, interrupt: true);
                        _phase = Phase.Settling;
                        return;
                    }
                    if (Take(key.Value, ctrl, shift, alt)) _phase = Phase.Settling;
                    return;
                case Phase.Settling:
                    if (!SdlKeyboard.AnyActive) Close();
                    return;
            }
        }

        // Applies the key; false (listening on) when it cannot be this action's.
        private bool Take(Scancode key, bool ctrl, bool shift, bool alt)
        {
            InputBinding binding;
            if (InstructionSlots.IsInstruction(_action))
            {
                if (ctrl || shift || alt)
                {
                    Speech.Say(Strings.KeysInstructionBare, interrupt: true);
                    return false;
                }
                binding = new FixedInstructionKey(key);
            }
            else binding = new KeyboardBinding(key, ctrl, shift, alt);

            string name = binding.DisplayName;
            var own = KeyBindings.Existing(_action, binding);
            if (own != null)
            {
                // The key being replaced, pressed again: nothing changes.
                if (own == _replacing) { Speech.Say(Strings.KeysKept(name), interrupt: true); return true; }
                Speech.Say(Strings.KeysTaken(name, _action.DisplayLabel), interrupt: true);
                return false;
            }
            var conflict = KeyBindings.Conflict(_action, binding);
            if (conflict != null)
            {
                Speech.Say(Strings.KeysTaken(name, conflict.DisplayLabel), interrupt: true);
                return false;
            }
            if (_replacing != null) KeyBindings.Replace(_action, _replacing, binding);
            else KeyBindings.Add(_action, binding);
            var said = new List<string> { Strings.KeysBound(name) };
            foreach (var other in KeyBindings.Overlaps(_action, binding))
                said.Add(Strings.RebindConflict(name, other.DisplayLabel));
            Speech.Say(string.Join(". ", said), interrupt: true);
            return true;
        }

        private void Close() => ParentScreen?.RemoveChild(this);
    }
}
