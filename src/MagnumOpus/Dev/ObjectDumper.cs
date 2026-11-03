#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using MagnumOpus.Host;

namespace MagnumOpus.Dev
{
    /// <summary>
    /// The /gui endpoint's engine: a reflection dump of the live screen model with HUMAN names (the
    /// build's name table maps every shipping field and type name back). Fields print in metadata
    /// order as `f[N] name: Type`; values render shallowly, nested objects expand to the depth asked,
    /// collections show their first elements. Main thread only (the dev server routes it through the
    /// tick pump).
    /// </summary>
    internal static class ObjectDumper
    {
        private const int MaxCollectionItems = 24;
        private const int MaxLines = 500;

        public static string DumpActiveScreen()
        {
            if (!GameBinding.Bound) return "[not bound] no name table for this build\n";
            var stack = N.Screens();
            if (stack.Count == 0) return "(no screens on the stack yet)\n";
            var sb = new StringBuilder();
            sb.Append("stack (bottom -> top):\n");
            for (int i = 0; i < stack.Count; i++)
                sb.Append("  ").Append(i).Append(": ").Append(N.Name(stack[i])).Append('\n');
            sb.Append('\n');
            DumpInto(sb, stack[stack.Count - 1], "active", 0, 2, new HashSet<object>(ReferenceComparer.Instance));
            return sb.ToString();
        }

        public static string Dump(object obj, int depth)
        {
            var sb = new StringBuilder();
            DumpInto(sb, obj, "value", 0, depth, new HashSet<object>(ReferenceComparer.Instance));
            return sb.ToString();
        }

        private static void DumpInto(StringBuilder sb, object obj, string label, int indent, int depth, HashSet<object> seen)
        {
            if (Lines(sb) > MaxLines) return;
            string pad = new string(' ', indent * 2);
            if (obj == null) { sb.Append(pad).Append(label).Append(" = null\n"); return; }

            var t = obj.GetType();
            if (IsLeaf(t)) { sb.Append(pad).Append(label).Append(" = ").Append(Render(obj)).Append('\n'); return; }
            if (!seen.Add(obj)) { sb.Append(pad).Append(label).Append(" = <cycle: ").Append(Pretty(t)).Append(">\n"); return; }

            sb.Append(pad).Append(label).Append(": ").Append(Pretty(t)).Append('\n');

            if (obj is IEnumerable && depth > 0)
            {
                int i = 0;
                foreach (var item in (IEnumerable)obj)
                {
                    if (i >= MaxCollectionItems) { sb.Append(pad).Append("  ... (truncated)\n"); break; }
                    DumpInto(sb, item, "[" + i + "]", indent + 1, depth - 1, seen);
                    i++;
                }
                if (i == 0) sb.Append(pad).Append("  (empty)\n");
                return;
            }
            if (depth <= 0) return;

            var fields = new List<FieldInfo>();
            for (var bt = t; bt != null && bt != typeof(object); bt = bt.BaseType)
                fields.InsertRange(0, bt.GetFields(GameBinding.All).Where(f => !f.IsLiteral).OrderBy(f => f.MetadataToken));
            for (int i = 0; i < fields.Count; i++)
            {
                if (Lines(sb) > MaxLines) { sb.Append(pad).Append("  ... (line cap)\n"); return; }
                var f = fields[i];
                object v;
                try { v = f.GetValue(f.IsStatic ? null : obj); }
                catch (Exception ex) { sb.Append(pad).Append("  ").Append(FieldName(f)).Append(" <threw: ").Append(ex.GetType().Name).Append(">\n"); continue; }
                string name = "f[" + i + "] " + (f.IsStatic ? "static " : "") + FieldName(f) + ": " + Pretty(f.FieldType);
                DumpInto(sb, v, name, indent + 1, depth - 1, seen);
            }
        }

        private static string FieldName(FieldInfo f)
        {
            var decl = f.DeclaringType;
            if (GameBinding.Table == null || decl == null || decl.Assembly != GameBinding.Game) return f.Name;
            return GameBinding.Table.HumanMember(decl.FullName.Replace('+', '/'), f.Name);
        }

        private static bool IsLeaf(Type t)
            => t.IsPrimitive || t.IsEnum || t == typeof(string) || t == typeof(decimal)
               || t == typeof(IntPtr) || t == typeof(UIntPtr)
               || (t.IsValueType && t.Assembly == typeof(int).Assembly);

        private static string Render(object v)
        {
            if (v is string s) return "\"" + s + "\"";
            if (v is Enum e && e.GetType().Assembly == GameBinding.Game) return Pretty(e.GetType()) + "(" + Convert.ToInt64(e) + ")";
            return v.ToString();
        }

        private static string Pretty(Type t)
        {
            string name = GameBinding.HumanName(t.IsGenericType ? t.GetGenericTypeDefinition() : t);
            int slash = name.LastIndexOf('/');
            if (slash >= 0) name = name.Substring(slash + 1);
            if (!t.IsGenericType) return name;
            int tick = name.IndexOf('`');
            if (tick > 0) name = name.Substring(0, tick);
            return name + "<" + string.Join(",", t.GetGenericArguments().Select(Pretty)) + ">";
        }

        private static int Lines(StringBuilder sb)
        {
            int n = 0;
            for (int i = 0; i < sb.Length; i++) if (sb[i] == '\n') n++;
            return n;
        }

        private sealed class ReferenceComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceComparer Instance = new ReferenceComparer();
            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);
            int IEqualityComparer<object>.GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
#endif
