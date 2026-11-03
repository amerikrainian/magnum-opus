using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mono.Cecil;

namespace MagnumOpus.Names
{
    /// <summary>
    /// The entry points: rename a game module into human names (in memory for the runtime table, or
    /// to disk for the compile reference), and the reference-only finishing passes (the enum
    /// constants the named layer re-adds, publicizing).
    /// </summary>
    public static class GameNames
    {
        /// <summary>Read a game exe with no assembly resolution (we only touch its own metadata) and
        /// without locking the file.</summary>
        public static ModuleDefinition ReadModule(string path)
        {
            byte[] bytes = File.ReadAllBytes(path);
            return ModuleDefinition.ReadModule(new MemoryStream(bytes), new ReaderParameters
            {
                ReadingMode = ReadingMode.Immediate,
                AssemblyResolver = new NullResolver(),
                ReadSymbols = false,
            });
        }

        /// <summary>Rename with the community mappings. Returns the log; the source is returned too,
        /// for the reference-only passes that need intermediary type names.</summary>
        public static RenameLog ApplyMappings(ModuleDefinition module, ObfMap obf, NamedMap named, out MappingNameSource source)
        {
            if (obf.Mvid != Guid.Empty && obf.Mvid != module.Mvid)
                throw new InvalidOperationException("mapping is for build " + obf.Mvid + ", the exe is " + module.Mvid + ".");
            source = new MappingNameSource(obf, named);
            return Renamer.Apply(module, source);
        }

        /// <summary>The runtime table for a game exe and its build's mapping.</summary>
        public static NameTable TableFromMappings(ModuleDefinition module, ObfMap obf, NamedMap named)
        {
            var log = ApplyMappings(module, obf, named, out _);
            return NameTable.FromRenamed(module, log);
        }

        /// <summary>Add the enum constants the named layer re-creates (the obfuscator stripped them),
        /// so module code can write ChapterAlignment.Left. Constants compile into the module as plain
        /// numbers; the running game never needs them. Reference only.</summary>
        public static int AddEnumVariants(ModuleDefinition module, NamedMap named, MappingNameSource source)
        {
            int added = 0;
            foreach (var t in module.GetTypes().Where(t => t.IsEnum))
            {
                // The source cached each type's mapping entry while the renamer asked it for names.
                string inter = source.InterOf(t);
                if (inter == null) continue;
                if (!named.EnumVariants.TryGetValue(inter, out var variants)) continue;
                var valueField = t.Fields.FirstOrDefault(f => !f.IsStatic);
                if (valueField == null) continue;
                var existing = new HashSet<string>(t.Fields.Select(f => f.Name), StringComparer.Ordinal);
                foreach (var kv in variants)
                {
                    if (!existing.Add(kv.Value)) continue;
                    var field = new FieldDefinition(kv.Value,
                        FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.Literal | FieldAttributes.HasDefault, t)
                    {
                        Constant = Convert(kv.Key, valueField.FieldType.MetadataType),
                    };
                    t.Fields.Add(field);
                    added++;
                }
            }
            return added;
        }

        private static object Convert(long v, MetadataType underlying)
        {
            switch (underlying)
            {
                case MetadataType.SByte: return (sbyte)v;
                case MetadataType.Byte: return (byte)v;
                case MetadataType.Int16: return (short)v;
                case MetadataType.UInt16: return (ushort)v;
                case MetadataType.UInt32: return (uint)v;
                case MetadataType.Int64: return v;
                case MetadataType.UInt64: return (ulong)v;
                default: return (int)v;
            }
        }

        /// <summary>Make every type, field and method public, so module code reads private game state
        /// and calls private methods directly. Compile-time only: at runtime the module carries
        /// [IgnoresAccessChecksTo("Lightning")], which CLR 4 honours. Explicit interface
        /// implementations (dotted private names) stay as they are.</summary>
        public static void Publicize(ModuleDefinition module)
        {
            foreach (var t in module.GetTypes())
            {
                if (t.IsNested) t.IsNestedPublic = true;
                else t.IsPublic = true;
                foreach (var f in t.Fields)
                {
                    f.IsPublic = true;
                }
                foreach (var m in t.Methods)
                {
                    if (m.IsPrivate && m.HasOverrides) continue;
                    m.IsPublic = true;
                }
            }
        }

        private sealed class NullResolver : IAssemblyResolver
        {
            public AssemblyDefinition Resolve(AssemblyNameReference name) => null;
            public AssemblyDefinition Resolve(AssemblyNameReference name, ReaderParameters parameters) => null;
            public void Dispose() { }
        }
    }
}
