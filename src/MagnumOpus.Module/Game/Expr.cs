using System;
using System.Linq.Expressions;
using System.Reflection;

namespace MagnumOpus.Module.Game
{
    /// <summary>
    /// Game members as MethodInfo/ConstructorInfo for Harmony, by expression: the compiler emits an
    /// ldtoken for the target, which the load-time remap rewrites to the running build's shipping
    /// name. NEVER target a game member by a name string (typeof(X).GetMethod("Human")): strings are
    /// not remapped and fail on the shipping build.
    /// </summary>
    public static class Expr
    {
        public static MethodInfo MethodOf(Expression<Action> call) => Method(call.Body);

        public static MethodInfo MethodOf<T>(Expression<Func<T>> call) => Method(call.Body);

        public static MethodInfo MethodOf<TArg>(Expression<Action<TArg>> call) => Method(call.Body);

        private static MethodInfo Method(Expression body)
        {
            if (body is UnaryExpression u) body = u.Operand; // a conversion around the call
            if (body is MethodCallExpression mc) return mc.Method;
            throw new ArgumentException("Expr.MethodOf needs a method call expression, got " + body.NodeType);
        }

        public static ConstructorInfo ConstructorOf<T>(Expression<Func<T>> create) =>
            (create.Body as NewExpression)?.Constructor ?? throw new ArgumentException("Expr.ConstructorOf needs a new expression");

        /// <summary>A property getter (or field-like call) target.</summary>
        public static MethodInfo GetterOf<T>(Expression<Func<T>> member) =>
            ((member.Body as MemberExpression)?.Member as PropertyInfo)?.GetGetMethod(true)
            ?? throw new ArgumentException("Expr.GetterOf needs a property access");
    }
}
