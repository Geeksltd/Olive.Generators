using System;
using System.IO;
using System.Text.Json;
using System.Threading;
using Olive;

namespace OliveGenerator
{
    /// <summary>
    /// The framework the microservice being generated from targets, read off that application in
    /// the same spirit as its package versions rather than written into this generator.
    /// <para>
    /// Most generated projects target netstandard2.0 and need none of this. The M# leg of a package
    /// cannot: M# is framework specific, so that project has to name one, and naming it here meant
    /// an application that had moved on still had its packages built for whatever framework the
    /// last release of this generator happened to have been written against.
    /// </para>
    /// </summary>
    static class TargetFramework
    {
        static Lazy<string> Resolved = Reader(null);

        /// <summary>
        /// The target's framework moniker, such as "net10.0", or null when it does not say. A class
        /// library has no runtimeconfig.json, so a caller needs a default of its own.
        /// </summary>
        public static string Moniker => Resolved.Value;

        /// <summary>Reads the framework of the microservice that owns the given assembly.</summary>
        public static void Load(FileInfo targetAssembly) => Resolved = Reader(targetAssembly);

        // Read while the projects of a whole run are being generated, so keep it thread safe.
        static Lazy<string> Reader(FileInfo assembly)
            => new Lazy<string>(() => Read(assembly), LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// Taken from the runtimeconfig.json beside the assembly, which carries the moniker as the
        /// build wrote it. The deps.json spells the same thing ".NETCoreApp,Version=v10.0", which
        /// would have to be translated back into one.
        /// </summary>
        static string Read(FileInfo assembly)
        {
            var file = assembly?.Directory
                .GetFile(Path.GetFileNameWithoutExtension(assembly.Name) + ".runtimeconfig.json");

            if (file?.Exists() != true) return null;

            try
            {
                using var document = JsonDocument.Parse(file.ReadAllText());

                if (!document.RootElement.TryGetProperty("runtimeOptions", out var options)) return null;
                if (!options.TryGetProperty("tfm", out var tfm)) return null;

                var result = tfm.GetString().OrNullIfEmpty();
                if (result != null) Log.Detail("the target application targets " + result + ".");

                return result;
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to read {file.FullName}: {ex.Message}");
                return null;
            }
        }
    }
}
