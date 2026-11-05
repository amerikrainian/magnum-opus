using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Screens;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.UI;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Input;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens.Editor
{
    /// <summary>
    /// The parts stop: the game's parts tray, its sections in its order (Products, Reagents,
    /// Mechanisms, Glyphs) with the same rules (SolutionEditorPartsPanel): a reagent or product
    /// until it is placed, a mechanism or glyph the puzzle permits, unavailable at its limit (a
    /// unique part, a puzzle's part count). Each reads its name (the game's tooltip name; a
    /// multi-armed assembly with its gripper count, which the game shows in its picture) and cost; S
    /// or / reads the game's tooltip description, as hovering does. Enter creates the part at the
    /// board cursor and carries it there (<see cref="CarryMode"/>).
    /// </summary>
    internal sealed class TraySection : ScreenSection
    {
        private readonly EditorContext _ctx;

        public TraySection(EditorContext ctx) { _ctx = ctx; }

        private struct TrayItem { public PartType Type; public int Index; public bool Available; }

        private List<KeyValuePair<string, List<TrayItem>>> Sections(SolutionEditorScreen ed)
        {
            var solution = ed.GetSolution();
            var puzzle = solution.GetPuzzle();
            var products = new List<TrayItem>();
            for (int i = 0; i < puzzle.outputs.Length; i++)
            {
                if (puzzle.outputs[i].hexPos.HasValue()) continue;
                var type = puzzle.outputs[i].molecule.HasRepetitionPos() ? PartTypes.repeatingOutput : PartTypes.normalOutput;
                int index = i;
                bool placed = solution.parts.Any(p => p.GetType().IsOutput() && p.GetInputOutputIndex() == index);
                products.Add(new TrayItem { Type = type, Index = i, Available = !placed });
            }
            var reagents = new List<TrayItem>();
            for (int i = 0; i < puzzle.inputs.Length; i++)
            {
                if (puzzle.inputs[i].hexPos.HasValue()) continue;
                int index = i;
                bool placed = solution.parts.Any(p => p.GetType().isInput && p.GetInputOutputIndex() == index);
                reagents.Add(new TrayItem { Type = PartTypes.input, Index = i, Available = !placed });
            }
            var mechanisms = Permitted(solution, PartTypes.singleArm, PartTypes.biArm, PartTypes.triArm, PartTypes.hexArm,
                PartTypes.pistonArm, PartTypes.track, PartTypes.berlosWheel, PartTypes.ravarisWheel);
            var glyphs = Permitted(solution, PartTypes.bonder, PartTypes.multibonder, PartTypes.triplexBonder, PartTypes.unbonder,
                PartTypes.calcificationGlyph, PartTypes.duplicationGlyph, PartTypes.projectionGlyph, PartTypes.purificationGlyph,
                PartTypes.rejectionGlyph, PartTypes.divisionGlyph, PartTypes.proliferationGlyph, PartTypes.animismusGlyph,
                PartTypes.disposalGlyph, PartTypes.unificationGlyph, PartTypes.dispersionGlyph, PartTypes.equilibriumGlyph);
            var sections = new List<KeyValuePair<string, List<TrayItem>>>();
            void Add(string english, List<TrayItem> items)
            {
                if (items.Count > 0) sections.Add(new KeyValuePair<string, List<TrayItem>>(GameText.Translate(english), items));
            }
            Add("Products", products);
            Add("Reagents", reagents);
            Add("Mechanisms", mechanisms);
            Add("Glyphs", glyphs);
            return sections;
        }

        private static List<TrayItem> Permitted(Solution solution, params PartType[] types)
        {
            var puzzle = solution.GetPuzzle();
            var items = new List<TrayItem>();
            foreach (var type in types)
            {
                if (ReferenceEquals(type, null) || !puzzle.HasPermissionForPart(type)) continue;
                int limit = type.isUnique ? 1 : (puzzle.limitedPartAmounts.TryGetValue(type, out int l) ? l : int.MaxValue);
                bool available = true;
                if (limit != int.MaxValue)
                    available = solution.CollectPartsAndSubparts().Count(p => p.GetType() == type) < limit;
                items.Add(new TrayItem { Type = type, Available = available });
            }
            return items;
        }

        public override void Build(GraphBuilder b)
        {
            var ed = _ctx.Ed;
            if (ed == null || ed.GetSolution() == null) return;
            var sections = Sections(ed);
            if (sections.Count == 0) return;
            b.BeginStop(EditorContext.StopTray);
            b.PushContext(Strings.EditorTray, null, positions: false);
            foreach (var section in sections)
            {
                b.PushContext(section.Key, Strings.RoleList);
                foreach (var item in section.Value)
                {
                    var it = item;
                    string id = "editor:tray:" + it.Type.id + ":" + it.Index;
                    _byId[id] = it;
                    b.AddItem(ControlId.Structural(id), new NodeVtable
                    {
                        // No role word: "Fixed-Length Arm, 20g" (user rule 2026-11-04).
                        ControlType = ControlTypes.Text,
                        Announcements = new List<NodeAnnouncement>
                        {
                            Nodes.LabelPart(() => Name(it)),
                            Nodes.ValuePart(() => Cost(it)),
                            Nodes.DisabledPart(() => it.Available),
                        },
                        SearchText = () => Name(it),
                        OnActivate = () => Take(it),
                    });
                }
                b.PopContext();
            }
            b.PopContext();
        }

        private readonly Dictionary<string, TrayItem> _byId = new Dictionary<string, TrayItem>();

        private bool Focused => Equals(Navigation.FocusedStopKey, EditorContext.StopTray);

        public override IEnumerable<ElementAction> GetActions()
        {
            if (!Focused || _ctx.Ed == null) yield break;
            yield return new ElementAction(EditorActions.Details, Strings.Get("bind.ed.details"), _ => SayDetails());
            yield return new ElementAction(EditorActions.Info, Strings.Get("bind.ed.details"), _ => SayDetails());
        }

        // S or /: the game's tooltip for the focused part (its name and description).
        private void SayDetails()
        {
            var key = Navigation.FocusedNode?.Id?.StructuralKey as string;
            if (key == null || !_byId.TryGetValue(key, out var it)) return;
            string d = GameText.Of(it.Type.description);
            EditorContext.Say(d.Length > 0 ? Name(it) + ". " + d : Name(it));
        }

        private string Name(TrayItem it)
        {
            var ed = _ctx.Ed;
            string name = Board.TypeName(it.Type);
            if (ed == null || (!it.Type.isInput && !it.Type.IsOutput())) return name;
            var puzzle = ed.GetSolution().GetPuzzle();
            var molecule = it.Type.isInput ? puzzle.GetInputMolecule(it.Index).molecule : puzzle.GetOutputMolecule(it.Index).molecule;
            if (molecule != null && molecule.displayName.HasValue())
                name = GameText.Clean(string.Format(UIConsts.inputOutputNameFormat, name, molecule.displayName.GetValue().GetTranslated()));
            return name;
        }

        private static string Cost(TrayItem it)
        {
            if (it.Type.isInput || it.Type.IsOutput()) return null;
            return GameText.Cost(it.Type.cost);
        }

        // A new part at the board cursor, carried there: the board takes focus.
        private void Take(TrayItem it)
        {
            var ed = _ctx.Ed;
            if (ed == null || !it.Available || _ctx.Busy() || !_ctx.CanEdit()) return;
            var part = new Part(it.Type, false);
            part.SetupInputOutputFromSolution(ed.GetSolution(), it.Index);
            Navigation.JumpToStop(EditorContext.StopBoard);
            _ctx.Carry.Start(part, isNew: true, isClone: false);
        }
    }
}
