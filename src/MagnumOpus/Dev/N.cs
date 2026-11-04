#if DEBUG
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MagnumOpus.Host;

namespace MagnumOpus
{
    /// <summary>
    /// Human-name access to the live game for the dev REPL (POST /eval). /eval compiles against the
    /// RUNNING game, whose members carry shipping (obfuscated) names, so typed code there cannot say
    /// GameLogic.instance; this goes through the build's name table instead:
    ///   N.Game                          GameLogic.instance
    ///   N.Top                           the active screen; N.Screens() the stack, bottom to top
    ///   N.Static("GameLogic", "instance")
    ///   N.Get(obj, "screenStack")       a field (or a parameterless method's result) by human name
    ///   N.Set(obj, "field", value)
    ///   N.Call(obj, "PushScreen", screen)   by human name and argument count
    ///   N.Type("Sim")                   a game type by human full name (Outer/Inner)
    ///   N.Dump(obj[, depth])            fields in token order with human names
    ///   N.Name(obj)                     the human type name of an object
    /// DEBUG only.
    /// </summary>
    public static class N
    {
        public static Type Type(string humanFull) =>
            GameBinding.Type(humanFull) ?? throw new ArgumentException("no game type named " + humanFull);

        public static object Game => Static("GameLogic", "instance");

        public static object Top => Call(Game, "GetCurrentScreen");

        public static List<object> Screens()
        {
            var stack = Get(Game, "screenStack");
            var list = stack == null ? null : FirstListField(stack);
            return list == null ? new List<object>() : list.Cast<object>().ToList();
        }

        private static IList FirstListField(object o)
        {
            foreach (var f in o.GetType().GetFields(GameBinding.All))
                if (f.GetValue(o) is IList l) return l;
            return null;
        }

        public static object Static(string humanType, string member) => Get(null, member, Type(humanType));

        public static object Get(object obj, string member) => Get(obj, member, obj?.GetType());

        // A game member by human name on the RUNTIME type: the name table is keyed by the generic
        // definition (SettingsItem`1), the member is bound on the closed type (SettingsItem<string>).
        private static FieldInfo FieldOn(System.Type t, string member)
        {
            var def = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
            if (GameBinding.Table == null || def.Assembly != GameBinding.Game) return null;
            return GameBinding.Table.TryField(GameBinding.HumanName(def), member, out var shipping) ? t.GetField(shipping, GameBinding.All) : null;
        }

        private static MethodInfo MethodOn(System.Type t, string member, int argc)
        {
            var def = t.IsGenericType ? t.GetGenericTypeDefinition() : t;
            if (GameBinding.Table == null || def.Assembly != GameBinding.Game) return null;
            var names = new HashSet<string>(GameBinding.Table.Methods(GameBinding.HumanName(def), member).Select(o => o.Value));
            var hits = t.GetMethods(GameBinding.All).Where(m => names.Contains(m.Name) && m.GetParameters().Length == argc).ToList();
            return hits.Count == 1 ? hits[0] : null;
        }

        private static object Get(object obj, string member, System.Type type)
        {
            for (var t = type; t != null; t = t.BaseType)
            {
                var f = FieldOn(t, member);
                if (f != null) return f.GetValue(f.IsStatic ? null : obj);
                var m = MethodOn(t, member, 0);
                if (m != null) return m.Invoke(m.IsStatic ? null : obj, null);
            }
            // Not a game member (BCL types, real names): plain reflection.
            var plain = type?.GetField(member, GameBinding.All | BindingFlags.FlattenHierarchy);
            if (plain != null) return plain.GetValue(obj);
            var prop = type?.GetProperty(member, GameBinding.All | BindingFlags.FlattenHierarchy);
            if (prop != null) return prop.GetValue(obj, null);
            throw new MissingMemberException(GameBinding.HumanName(type), member);
        }

        public static void Set(object obj, string member, object value)
        {
            for (var t = obj.GetType(); t != null; t = t.BaseType)
            {
                var f = FieldOn(t, member);
                if (f != null) { f.SetValue(f.IsStatic ? null : obj, value); return; }
            }
            throw new MissingMemberException(GameBinding.HumanName(obj.GetType()), member);
        }

        public static object Call(object obj, string method, params object[] args) =>
            CallOn(obj, obj?.GetType(), method, args);

        public static object CallStatic(string humanType, string method, params object[] args) =>
            CallOn(null, Type(humanType), method, args);

        private static object CallOn(object obj, System.Type type, string method, object[] args)
        {
            args = args ?? new object[0];
            for (var t = type; t != null; t = t.BaseType)
            {
                var m = MethodOn(t, method, args.Length);
                if (m != null) return m.Invoke(m.IsStatic ? null : obj, args);
            }
            throw new MissingMethodException(GameBinding.HumanName(type), method + "/" + args.Length);
        }

        public static string Name(object obj) => obj == null ? "null" : GameBinding.HumanName(obj.GetType());

        public static string Dump(object obj, int depth = 1) => Dev.ObjectDumper.Dump(obj, depth);
    }
}
#endif
