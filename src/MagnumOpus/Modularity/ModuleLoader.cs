using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MagnumOpus.Contracts;
using MagnumOpus.Host;
using MagnumOpus.Names;
using Mono.Cecil;

namespace MagnumOpus.Modularity
{
    /// <summary>
    /// Loads (and hot-reloads) the reloadable half: MagnumOpus.Core.dll and MagnumOpus.Module.dll from
    /// the data folder. .NET Framework has no collectible load contexts, so both are
    /// Assembly.Load(byte[])'d: the files stay unlocked (a build overwrites them with the game running)
    /// and each generation gets fresh statics; old generations stay pinned until exit (a dev-loop cost).
    ///
    /// Per load, on the BYTES (Cecil):
    ///  - Core gets a per-generation assembly name (MagnumOpus.Core.g3), and the module's reference to
    ///    it is renamed to match, so every generation binds to its own Core whatever is already loaded;
    ///    <see cref="ModuleAssemblies.Resolve"/> answers those names.
    ///  - The module's game references are rewritten from the human names it compiled against to the
    ///    running build's shipping names (<see cref="Remapper"/> over <see cref="GameBinding.Table"/>).
    ///    What the build lacks is logged and handed to the module (<see cref="ModHostImpl.Unresolved"/>):
    ///    the code that uses it throws when reached, the rest runs.
    ///
    /// LOAD-THEN-SWAP: the candidate is constructed and Load()ed before the previous module is disposed,
    /// so a broken build leaves the running module in place. Hence the module rule: Harmony ids are
    /// per load and Dispose unpatches its own.
    /// </summary>
    internal sealed class ModuleLoader
    {
        private readonly string _dir;
        private readonly ModHostImpl _host;

        public IModModule Module { get; private set; }
        public int Generation { get; private set; }

        public string CorePath => Path.Combine(_dir, "MagnumOpus.Core.dll");
        public string ModulePath => Path.Combine(_dir, "MagnumOpus.Module.dll");

        public ModuleLoader(string dataDir, ModHostImpl host)
        {
            _dir = dataDir;
            _host = host;
        }

        public bool Reload()
        {
            int gen = Generation + 1;
            IModModule candidate = null;
            try
            {
                string coreName = "MagnumOpus.Core.g" + gen;
                byte[] core = RenameAssembly(File.ReadAllBytes(CorePath), coreName);
                var coreAsm = Assembly.Load(core);
                ModuleAssemblies.Register(coreAsm);

                var unresolved = new List<string>();
                byte[] module = PrepareModule(File.ReadAllBytes(ModulePath), coreName, unresolved);
                var asm = Assembly.Load(module);
                ModuleAssemblies.Register(asm);

                Type impl = asm.GetTypes().FirstOrDefault(t => typeof(IModModule).IsAssignableFrom(t) && !t.IsAbstract && !t.IsInterface);
                if (impl == null)
                {
                    Log.Error("[module] no IModModule implementation in " + ModulePath + ".");
                    return false;
                }

                _host.Unresolved = unresolved;
                _host.Generation = gen;
                candidate = (IModModule)Activator.CreateInstance(impl);
                candidate.Load(_host);
            }
            catch (Exception ex)
            {
                Log.Error("[module] load failed; keeping the running module (if any)", ex);
                try { candidate?.Dispose(); } catch { }
                _host.Generation = Generation;
                return false;
            }

            var old = Module;
            Module = candidate;
            Generation = gen;
            Log.Info("[module] " + (old == null ? "loaded" : "reloaded") + " (generation " + gen + ", dll "
                + File.GetLastWriteTime(ModulePath).ToString("HH:mm:ss") + ").");
            if (old != null)
            {
                _host.SuccessorLoaded = true;
                try { old.Dispose(); }
                catch (Exception ex) { Log.Error("[module] old module Dispose failed (continuing on the new one)", ex); }
                finally { _host.SuccessorLoaded = false; }
            }
            return true;
        }

        private static byte[] RenameAssembly(byte[] bytes, string name)
        {
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                module.Assembly.Name.Name = name;
                var output = new MemoryStream();
                module.Write(output);
                return output.ToArray();
            }
        }

        private static byte[] PrepareModule(byte[] bytes, string coreName, List<string> unresolved)
        {
            using (var module = ModuleDefinition.ReadModule(new MemoryStream(bytes)))
            {
                foreach (var r in module.AssemblyReferences)
                    if (r.Name == "MagnumOpus.Core") r.Name = coreName;

                var result = Remapper.Remap(module, GameBinding.Table);
                unresolved.AddRange(result.Unresolved);
                Log.Info("[remap] module bound to build " + GameBinding.Mvid + " (" + GameBinding.Source + "): "
                    + result.Types + " type and " + result.Members + " member reference(s) renamed, "
                    + result.Unresolved.Count + " unresolved.");
                foreach (var u in result.Unresolved) Log.Warning("[remap] unresolved: " + u);

                var output = new MemoryStream();
                module.Write(output);
                return output.ToArray();
            }
        }
    }

    /// <summary>Answers assembly loads for the byte-loaded generations (the CLR never probes for
    /// them), and for any assembly already loaded under the requested name.</summary>
    internal static class ModuleAssemblies
    {
        private static readonly Dictionary<string, Assembly> ByName = new Dictionary<string, Assembly>(StringComparer.OrdinalIgnoreCase);
        private static readonly object Gate = new object();

        public static void Register(Assembly asm)
        {
            lock (Gate) ByName[asm.GetName().Name] = asm;
        }

        public static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string name = new AssemblyName(args.Name).Name;
            lock (Gate)
                if (ByName.TryGetValue(name, out var hit)) return hit;
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                if (!a.IsDynamic && a.GetName().Name == name) return a;
            return null;
        }
    }
}
