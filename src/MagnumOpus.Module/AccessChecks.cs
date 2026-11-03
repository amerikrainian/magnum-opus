// The module compiles against a PUBLICIZED reference (game\Lightning.named.ref.exe: every game type
// and member public). This attribute makes the .NET Framework CLR skip the visibility checks at JIT
// time, so the same references bind to the private and internal members of the live game. Verified
// on .NET Framework 4.8 by SpeechChem (a two-assembly experiment, then live).
[assembly: System.Runtime.CompilerServices.IgnoresAccessChecksTo("Lightning")]

namespace System.Runtime.CompilerServices
{
    /// <summary>Recognized by the CLR by name; the BCL does not ship it for .NET Framework, so the
    /// consuming assembly declares it itself.</summary>
    [AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
    internal sealed class IgnoresAccessChecksToAttribute : Attribute
    {
        public IgnoresAccessChecksToAttribute(string assemblyName) { AssemblyName = assemblyName; }

        public string AssemblyName { get; }
    }
}
