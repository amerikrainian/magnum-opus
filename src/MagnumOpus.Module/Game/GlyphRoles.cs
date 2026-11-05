using System;
using System.Collections.Generic;
using MagnumOpus.Core.Strings;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// What each hex of a stock glyph takes or gives, which the game draws (a quicksilver symbol on
    /// one hex of projection, a lead symbol on the other) but stores nowhere: a glyph's PartType holds
    /// only its footprint, and its rule is a hand-written block per glyph in Sim's glyph pass (its
    /// drawing likewise, with images made for that glyph). Checked 2026-11-05, Quintessential included:
    /// its glyph recipes are predicates that name their hexes only once they fire. So this table is
    /// transcribed from the game's Sim, by part type id, offsets in the glyph's own frame (the hex
    /// is found through Part.InFrontBy, rotation included). A glyph not listed (a content mod's, or a
    /// stock one whose hexes are alike: bonding, unbonding, triplex, calcification, equilibrium) reads
    /// no role; S reads its description.
    /// </summary>
    internal static class GlyphRoles
    {
        private struct Role
        {
            public HexIndex Offset;
            public Func<string> Text;
            public Role(int q, int r, Func<string> text) { Offset = new HexIndex(q, r); Text = text; }
        }

        private static string Atom(AtomType t) => Board.AtomName(t);
        private static string In(string what) => Strings.GlyphIn(what);
        private static string Out(string what) => Strings.GlyphOut(what);

        private static readonly Dictionary<string, Role[]> Table = new Dictionary<string, Role[]>
        {
            // Bonds the centre to each of the three others.
            ["bonder-speed"] = new[] { new Role(0, 0, () => Strings.GlyphCentre) },
            // A cardinal element (may be on a wheel) copied onto a salt.
            ["glyph-duplication"] = new[]
            {
                new Role(0, 0, () => In(Strings.GlyphCardinal)),
                new Role(1, 0, () => In(Atom(AtomTypes.salt))),
            },
            // A loose quicksilver consumed, the metal promoted.
            ["glyph-projection"] = new[]
            {
                new Role(0, 0, () => In(Atom(AtomTypes.quicksilver))),
                new Role(1, 0, () => In(Strings.GlyphMetal)),
            },
            // The metal demoted, a quicksilver put out on the empty hex.
            ["glyph-rejection"] = new[]
            {
                new Role(0, 0, () => In(Strings.GlyphMetal)),
                new Role(1, 0, () => Out(Atom(AtomTypes.quicksilver))),
            },
            // Two loose atoms of one metal, the next metal put out.
            ["glyph-purification"] = new[]
            {
                new Role(0, 0, () => In(Strings.GlyphMetal)),
                new Role(1, 0, () => In(Strings.GlyphMetal)),
                new Role(0, 1, () => Out(Strings.GlyphMetal)),
            },
            // One loose metal split into two lower ones.
            ["glyph-division"] = new[]
            {
                new Role(0, 0, () => In(Strings.GlyphMetal)),
                new Role(-1, 0, () => Out(Strings.GlyphMetal)),
                new Role(1, 0, () => Out(Strings.GlyphMetal)),
            },
            // A loose quicksilver made into a copy of the metal (which may be on a wheel).
            ["glyph-proliferation"] = new[]
            {
                new Role(1, 1, () => In(Atom(AtomTypes.quicksilver))),
                new Role(-1, 1, () => In(Strings.GlyphMetal)),
                new Role(1, -1, () => Out(Strings.GlyphMetal)),
            },
            // Two loose salts made into vitae and mors.
            ["glyph-life-and-death"] = new[]
            {
                new Role(0, 0, () => In(Atom(AtomTypes.salt))),
                new Role(1, 0, () => In(Atom(AtomTypes.salt))),
                new Role(0, 1, () => Out(Atom(AtomTypes.vitae))),
                new Role(1, -1, () => Out(Atom(AtomTypes.mors))),
            },
            // One loose atom of each cardinal element, any order, made into quintessence.
            ["glyph-unification"] = new[]
            {
                new Role(-1, 1, () => In(Strings.GlyphCardinal)),
                new Role(0, 1, () => In(Strings.GlyphCardinal)),
                new Role(0, -1, () => In(Strings.GlyphCardinal)),
                new Role(1, -1, () => In(Strings.GlyphCardinal)),
                new Role(0, 0, () => Out(Atom(AtomTypes.quintessence))),
            },
            // A loose quintessence made into the four cardinal elements, each on its own hex.
            ["glyph-dispersion"] = new[]
            {
                new Role(0, 0, () => In(Atom(AtomTypes.quintessence))),
                new Role(-1, 0, () => Out(Atom(AtomTypes.air))),
                new Role(0, -1, () => Out(Atom(AtomTypes.fire))),
                new Role(1, -1, () => Out(Atom(AtomTypes.water))),
                new Role(1, 0, () => Out(Atom(AtomTypes.earth))),
            },
            // A loose atom on the centre is removed; the ring only takes room.
            ["glyph-disposal"] = new[] { new Role(0, 0, () => Strings.GlyphCentre) },
        };

        /// <summary>The role of a board hex within a glyph ("metal in", "Quicksilver out"), or null.</summary>
        public static string At(Part part, HexIndex hex)
        {
            if (part == null || !Table.TryGetValue(part.GetType().id ?? "", out var roles)) return null;
            foreach (var role in roles)
                if (part.InFrontBy(role.Offset) == hex) return role.Text();
            return null;
        }
    }
}
