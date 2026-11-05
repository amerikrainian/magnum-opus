using System;
using System.Collections.Generic;
using System.Linq;
using MagnumOpus.Core.Graph;
using MagnumOpus.Core.Strings;
using MagnumOpus.Core.Text;
using MagnumOpus.Module.Game;
using MagnumOpus.Module.Patches;
using MagnumOpus.Module.UI;

namespace MagnumOpus.Module.Screens
{
    /// <summary>
    /// The alchemical reference diagram (PeriodicTableScreen, the editor's Show Diagram, titled
    /// "Common Alchemical Primes &amp; Their Transmutations"). The game draws one picture with the
    /// element names over it. Read as lines (the stock picture checked against the game's own screen
    /// 2026-11-05): Salt joined to the four cardinal elements ("Air above, Fire left, ..."); Vitae and
    /// Mors on a line through Salt; the metals as a chain ("bottom to top", a Quicksilver between each
    /// two); then the game's quote, then Close (the game's Escape, which its close button answers).
    /// What comes from where, so content mods read as far as they can:
    /// - Names: the game's, live.
    /// - Directions: from where the game draws each name's label (<see cref="TextCapture"/>,
    ///   <see cref="Compass"/>), so a rearranged picture reads as drawn; a label not drawn (or not yet
    ///   captured) leaves its direction out.
    /// - Metal chains: the game's atom registry (AtomTypes.atoms, each metal's successor), so a metal
    ///   a mod adds to a chain, or a chain of its own, is read.
    /// - The links Salt-cardinals and Salt-Vitae-Mors: the stock picture's (the game has them only as
    ///   lines in the picture and as fixed glyph rules); a link a mod draws is not read.
    /// - Any other text the screen draws (a mod's label): read after the links, placed by the nearest
    ///   element label ("Zinc, below Lead").
    /// </summary>
    public sealed class PeriodicTableGameScreen : GameScreen<PeriodicTableScreen>
    {
        public PeriodicTableGameScreen() { ReadsText.Add(typeof(PeriodicTableScreen)); }

        public override string Key => "game.diagram";

        private DateTime? _waitingSince;

        // Active once a frame of the screen's text is captured (the game draws its title every frame),
        // so the landing reads with the label positions: the capture only runs once the opening fade
        // is over (the game's input is off during it), the same frame the screen would otherwise
        // activate, and the first line read without its directions. Never silent: without text it
        // activates after half a second.
        public override bool IsActive()
        {
            if (!base.IsActive()) { _waitingSince = null; return false; }
            string title = GameText.Of(PeriodicTableScreen.title);
            if (TextCapture.LastFrame.Any(d => GameText.Clean(d.Text) == title)) return true;
            if (_waitingSince == null) _waitingSince = DateTime.UtcNow;
            return (DateTime.UtcNow - _waitingSince.Value).TotalSeconds > 0.5;
        }

        public override string ScreenName => GameText.Of(PeriodicTableScreen.title);

        public override void Build(GraphBuilder b)
        {
            if (Game == null) return;
            b.PushContext(null, null);
            b.AddItem(ControlId.Structural("diagram:salt"), Nodes.Text(SaltLine));
            b.AddItem(ControlId.Structural("diagram:vitae"), Nodes.Text(VitaeMorsLine));
            var chains = MetalChains();
            for (int i = 0; i < chains.Count; i++)
            {
                var chain = chains[i];
                b.AddItem(ControlId.Structural("diagram:metals:" + i), Nodes.Text(() => MetalsLine(chain)));
            }
            var extras = Extras(chains);
            for (int i = 0; i < extras.Count; i++)
            {
                string line = extras[i];
                b.AddItem(ControlId.Structural("diagram:extra:" + i), Nodes.Text(() => line));
            }
            b.AddItem(ControlId.Structural("diagram:quote"), Nodes.Text(Quote));
            b.AddItem(ControlId.Structural("diagram:close"), Nodes.Button(() => Strings.ModClose, () => GameKeys.Forced = GameKeys.Escape));
            b.PopContext();
        }

        private static string Name(AtomType t) => Board.AtomName(t);

        // ---- where the labels are ----

        // Each text drawn last frame by its cleaned text (the first drawn, top to bottom, where one
        // repeats).
        private static Dictionary<string, TextCapture.Drawn> Labels()
        {
            var map = new Dictionary<string, TextCapture.Drawn>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in TextCapture.LastFrame)
            {
                string t = GameText.Clean(d.Text);
                if (!string.IsNullOrWhiteSpace(t) && !map.ContainsKey(t)) map[t] = d;
            }
            return map;
        }

        // Where one label is from another, or null when either is not drawn.
        private static Compass.Dir? Direction(Dictionary<string, TextCapture.Drawn> labels, string name, string from)
        {
            if (!labels.TryGetValue(name, out var a) || !labels.TryGetValue(from, out var o)) return null;
            return Compass.Of(a.X - o.X, a.Y - o.Y);
        }

        // "Air above": an element with its direction from another, or just its name.
        private static string Placed(Dictionary<string, TextCapture.Drawn> labels, AtomType t, AtomType from)
        {
            var dir = Direction(labels, Name(t), Name(from));
            return dir.HasValue ? Strings.DiagramPlaced(Name(t), Strings.DiagramDir(dir.Value)) : Name(t);
        }

        // ---- the lines ----

        private static string SaltLine()
        {
            var labels = Labels();
            var cardinals = new[] { AtomTypes.air, AtomTypes.fire, AtomTypes.water, AtomTypes.earth };
            return Strings.DiagramSalt(Name(AtomTypes.salt), string.Join(", ", cardinals.Select(c => Placed(labels, c, AtomTypes.salt))));
        }

        private static string VitaeMorsLine()
        {
            var labels = Labels();
            return Strings.DiagramVitaeMors(Placed(labels, AtomTypes.vitae, AtomTypes.salt), Placed(labels, AtomTypes.mors, AtomTypes.salt), Name(AtomTypes.salt));
        }

        private static string MetalsLine(List<AtomType> chain)
        {
            var labels = Labels();
            string metals = string.Join(", ", chain.Select(Name));
            string between = Name(AtomTypes.quicksilver);
            Compass.Dir? run = null;
            if (labels.TryGetValue(Name(chain[0]), out var first) && labels.TryGetValue(Name(chain[chain.Count - 1]), out var last))
                run = Compass.Run(last.X - first.X, last.Y - first.Y);
            return run.HasValue
                ? Strings.DiagramMetals(Strings.DiagramOrder(run.Value), metals, between)
                : Strings.DiagramMetalsPlain(metals, between);
        }

        // The game draws the quote and its author as one text.
        private static string Quote() => GameText.Clean(string.Concat(PeriodicTableScreen.quote, "\n\n--", PeriodicTableScreen.name));

        /// <summary>Every chain of metals in the registry, basest first: from each metal nothing
        /// promotes to, along its successors (which need not be in the registry). A chain of one is
        /// not a chain.</summary>
        private static List<List<AtomType>> MetalChains()
        {
            var chains = new List<List<AtomType>>();
            var atoms = AtomTypes.atoms;
            if (atoms == null) return chains;
            var promotedTo = new HashSet<byte>();
            foreach (var a in atoms)
                if (!ReferenceEquals(a, null) && a.isMetallic && a.successorMetal.HasValue()) promotedTo.Add(a.successorMetal.GetValue().byteId);
            foreach (var a in atoms)
            {
                if (ReferenceEquals(a, null) || !a.isMetallic || !a.successorMetal.HasValue() || promotedTo.Contains(a.byteId)) continue;
                var chain = new List<AtomType>();
                var seen = new HashSet<byte>();
                var at = a;
                while (seen.Add(at.byteId))
                {
                    chain.Add(at);
                    if (!at.successorMetal.HasValue()) break;
                    at = at.successorMetal.GetValue();
                }
                if (chain.Count > 1) chains.Add(chain);
            }
            return chains;
        }

        // Text the screen draws that the lines above do not already say (a content mod's labels), top
        // to bottom, each placed by the nearest element label; none before the capture has run.
        private static List<string> Extras(List<List<AtomType>> chains)
        {
            var elements = new List<AtomType> { AtomTypes.salt, AtomTypes.air, AtomTypes.fire, AtomTypes.water, AtomTypes.earth, AtomTypes.vitae, AtomTypes.mors, AtomTypes.quicksilver };
            foreach (var chain in chains) elements.AddRange(chain);
            var names = elements.Select(Name).Where(n => n.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            var known = new HashSet<string>(names, StringComparer.OrdinalIgnoreCase) { GameText.Of(PeriodicTableScreen.title), Quote() };
            var labels = Labels();
            var anchors = names.Where(labels.ContainsKey).Select(n => new KeyValuePair<string, TextCapture.Drawn>(n, labels[n])).ToList();
            var lines = new List<string>();
            var said = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var d in TextCapture.LastFrame)
            {
                string t = GameText.Clean(d.Text);
                if (string.IsNullOrWhiteSpace(t) || known.Contains(t) || !said.Add(t)) continue;
                lines.Add(Locate(t, d, anchors));
            }
            return lines;
        }

        // "Zinc, below Lead": a text placed by the nearest element label, or as it is when none is drawn.
        private static string Locate(string text, TextCapture.Drawn at, List<KeyValuePair<string, TextCapture.Drawn>> anchors)
        {
            if (anchors.Count == 0) return text;
            var nearest = anchors.OrderBy(a => (a.Value.X - at.X) * (a.Value.X - at.X) + (a.Value.Y - at.Y) * (a.Value.Y - at.Y)).First();
            var dir = Compass.Of(at.X - nearest.Value.X, at.Y - nearest.Value.Y);
            return dir.HasValue ? Strings.DiagramExtra(text, Strings.DiagramRelative(dir.Value, nearest.Key)) : text;
        }
    }
}
