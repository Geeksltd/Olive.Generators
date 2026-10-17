using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Olive;

namespace OliveGenerator
{
    public static class ResolveAssembliesExtensions
    {
        static bool Hooked;

        public static void ResolveAssemblies(this AppDomain domain)
        {
            // Called again for each item a run generates, and a second handler would repeat every
            // probe rather than resolve anything the first one could not.
            if (Hooked) return;

            domain.AssemblyResolve += CurrentDomain_AssemblyResolve;
            Hooked = true;
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var request = ParseName(args.Name);
            if (request == null) return null;

            var fileName = request.Name + ".dll";

            // The target application's folder, not the current directory, which is wherever the
            // generator happened to be started from.
            foreach (var folder in new[] { GeneratorHost.TargetFolder, Environment.CurrentDirectory }.Distinct())
            {
                var file = TargetAppLoadContext.FindDll(folder, request.Name);
                if (file != null) return LoadInto(args, file);
            }

            var version = request.Version ?? new Version();

            var chosen = SearchInGlobal(SharedFramework("Microsoft.AspNetCore.App"), fileName, version)
                ?? SearchInGlobal(SharedFramework("Microsoft.NETCore.App"), fileName, version);

            if (chosen != null)
            {
                Log.Diagnostic($"Resolved {request.Name} from the shared framework: {chosen}");
                return LoadInto(args, chosen);
            }

            // Diagnostic: reflection over a large application asks for assemblies that simply
            // are not there, and printing every one of them buried everything else.
            Log.Diagnostic("Could not resolve assembly: " + fileName);

            return null;
        }

        /// <summary>
        /// A malformed name throws rather than reporting the assembly as missing, and this runs
        /// inside an event handler where that would surface as an unrelated failure.
        /// </summary>
        static AssemblyName ParseName(string name)
        {
            if (name.IsEmpty()) return null;

            try
            {
                var result = new AssemblyName(name);
                return result.Name.IsEmpty() ? null : result;
            }
            catch (Exception ex)
            {
                Log.Warn($"Cannot parse the requested assembly name '{name}': {ex.Message}");
                return null;
            }
        }

        static DirectoryInfo SharedFramework(string name)
        {
            var programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            if (programFiles.IsEmpty()) return null;

            return programFiles.AsDirectory().GetSubDirectory("dotnet\\shared\\" + name);
        }

        /// <summary>
        /// Loads into the same context as the assembly that asked for it. Assembly.LoadFile() would
        /// create a context of its own, giving the shared Olive types a second identity and failing
        /// every cast between the generator's types and the target application's.
        /// </summary>
        static Assembly LoadInto(ResolveEventArgs args, string file)
        {
            var context = AssemblyLoadContext.GetLoadContext(args.RequestingAssembly ?? typeof(ResolveAssembliesExtensions).Assembly)
                ?? AssemblyLoadContext.Default;

            try { return context.LoadFromAssemblyPath(file); }
            catch (Exception ex)
            {
                Log.Warn($"Failed to load {file}: {ex.Message}");
                return null;
            }
        }

        static Version ParseVersion(string value) => Version.TryParse(value, out var result) ? result : null;

        /// <summary>
        /// Every dll in a shared framework, by file name. One recursive enumeration of the whole
        /// tree, rather than one per assembly that fails to resolve.
        /// </summary>
        static readonly ConcurrentDictionary<string, ILookup<string, FileInfo>> SharedFrameworkFiles =
            new ConcurrentDictionary<string, ILookup<string, FileInfo>>(StringComparer.OrdinalIgnoreCase);

        static string SearchInGlobal(DirectoryInfo folder, string fileName, Version version)
        {
            if (folder?.Exists() != true) return null;

            var files = SharedFrameworkFiles.GetOrAdd(folder.FullName,
                _ => folder.GetFiles("*.dll", SearchOption.AllDirectories)
                    .ToLookup(x => x.Name, StringComparer.OrdinalIgnoreCase));

            // A folder whose name is not a version, e.g. a preview build, is simply not a candidate.
            var matches = files[fileName]
                .Select(v => new { File = v.FullName, Version = ParseVersion(v.Directory.Name) })
                .Where(v => v.Version != null)
                .ToArray();

            return matches.OrderByDescending(x => x.Version == version)
                 .ThenByDescending(x => x.Version.Major == version.Major || x.Version.Minor == version.Minor)
                 .ThenByDescending(x => x.Version)
                 .FirstOrDefault()?.File;
        }
    }
}
