using System.Linq;
using System.Text;
using Mono.Cecil;

namespace MagnumOpus.Names
{
    /// <summary>
    /// Name and signature strings computed from the CURRENT names of a Cecil model, never from Cecil's
    /// cached FullName (a nested type's cache is not reset when its declaring type is renamed). One
    /// format serves the renamer's collision keys, the reverse name table and the module remap, so a
    /// reference in the module and the definition it targets always produce the same key. Generic
    /// parameters are positional (!0 for a type's, !!0 for a method's): a member reference on a generic
    /// instance and the definition it targets agree on positions, not on parameter names.
    /// </summary>
    public static class Sig
    {
        /// <summary>namespace.Name, nested types Outer/Inner.</summary>
        public static string Full(TypeReference t)
        {
            if (t.DeclaringType != null) return Full(t.DeclaringType) + "/" + t.Name;
            return string.IsNullOrEmpty(t.Namespace) ? t.Name : t.Namespace + "." + t.Name;
        }

        public static string Type(TypeReference t)
        {
            var sb = new StringBuilder();
            Append(sb, t);
            return sb.ToString();
        }

        private static void Append(StringBuilder sb, TypeReference t)
        {
            switch (t)
            {
                case GenericParameter gp:
                    sb.Append(gp.Type == GenericParameterType.Method ? "!!" : "!").Append(gp.Position);
                    return;
                case GenericInstanceType git:
                    Append(sb, git.ElementType);
                    sb.Append('<');
                    for (int i = 0; i < git.GenericArguments.Count; i++)
                    {
                        if (i > 0) sb.Append(',');
                        Append(sb, git.GenericArguments[i]);
                    }
                    sb.Append('>');
                    return;
                case ArrayType at:
                    Append(sb, at.ElementType);
                    sb.Append('[').Append(',', at.Rank - 1).Append(']');
                    return;
                case ByReferenceType br:
                    Append(sb, br.ElementType);
                    sb.Append('&');
                    return;
                case PointerType pt:
                    Append(sb, pt.ElementType);
                    sb.Append('*');
                    return;
                case RequiredModifierType rm:
                    Append(sb, rm.ElementType);
                    return;
                case OptionalModifierType om:
                    Append(sb, om.ElementType);
                    return;
                case PinnedType pin:
                    Append(sb, pin.ElementType);
                    return;
                case SentinelType st:
                    Append(sb, st.ElementType);
                    return;
                case FunctionPointerType _:
                    sb.Append(t.FullName);
                    return;
                default:
                    sb.Append(Full(t));
                    return;
            }
        }

        /// <summary>Return type, parameter types and generic arity: "System.Void(Part,System.Int32)`0".</summary>
        public static string Method(MethodReference m)
        {
            var sb = new StringBuilder();
            Append(sb, m.ReturnType);
            sb.Append('(');
            for (int i = 0; i < m.Parameters.Count; i++)
            {
                if (i > 0) sb.Append(',');
                Append(sb, m.Parameters[i].ParameterType);
            }
            sb.Append(")`").Append(m.HasGenericParameters ? m.GenericParameters.Count : 0);
            return sb.ToString();
        }

        /// <summary>A method reference's arity, including a generic-instance call site's.</summary>
        internal static int Arity(MethodReference m) =>
            m is GenericInstanceMethod gim ? gim.ElementMethod.GenericParameters.Count : (m.HasGenericParameters ? m.GenericParameters.Count : 0);

        /// <summary>The community files' form: Cecil's own FullName, '+' folded to '/'. Only valid on
        /// a model whose names have not been changed in this process (Cecil caches it).</summary>
        public static string CecilFull(TypeReference t) => t.FullName.Replace('+', '/');

        public static string[] CecilArgs(MethodReference m) =>
            m.Parameters.Select(p => CecilFull(p.ParameterType)).ToArray();
    }
}
