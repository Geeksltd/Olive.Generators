using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Xml.Linq;
using Olive;

namespace OliveGenerator
{
    /// <summary>
    /// Works out which package versions the generated project should reference, by reading them off
    /// the microservice being generated from rather than hard-coding them here.
    /// <para>
    /// A generated endpoint, command or proxy then compiles against exactly what its publisher uses,
    /// and this generator no longer needs a release of its own every time Olive or M# publishes one.
    /// The hard-coded versions that remain are only a fallback for when the target cannot tell us.
    /// </para>
    /// </summary>
    static class TargetPackageVersions
    {
        const string MSharpPackage = "MSharp";

        static DirectoryInfo Folder;
        static Dictionary<string, string> Deps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Read while the projects of a whole run are being generated, so keep it thread safe.
        static Lazy<string> MSharpVersion = NewMSharpVersionReader();

        /// <summary>Reads the package versions of the microservice that owns the given assembly.</summary>
        public static void Load(FileInfo targetAssembly)
        {
            Folder = targetAssembly?.Directory;
            Deps = ReadDependencies(targetAssembly);
            MSharpVersion = NewMSharpVersionReader();

            Log.Detail($"Read {Deps.Count} package version(s) from the target application. " +
                "The generated projects reference these, so they line up with their publisher.");
        }

        static Lazy<string> NewMSharpVersionReader()
            => new Lazy<string>(ReadMSharpVersion, LazyThreadSafetyMode.ExecutionAndPublication);

        /// <summary>
        /// The version of the specified package used by the target microservice, or null when it
        /// does not use it, in which case the caller should fall back to its own default.
        /// </summary>
        public static string For(string package)
        {
            if (package.IsEmpty()) return null;

            if (package.Equals(MSharpPackage, StringComparison.OrdinalIgnoreCase)) return MSharp();

            return Deps.TryGet(package) ?? FromAssemblyFile(package);
        }

        /// <summary>
        /// The M# version installed in the solution being generated from. M# is a build-time tool,
        /// so it is not among the target's dependencies; its version comes from the nuspec in the
        /// solution's M# folder instead.
        /// </summary>
        public static string MSharp() => MSharpVersion.Value;

        /// <summary>
        /// Whether the failure is nuget being unable to restore the requested version, as opposed
        /// to something unrelated such as a compile error in the generated code. Only then is it
        /// worth retrying with this generator's own defaults.
        /// </summary>
        public static bool IsMissingPackageVersion(Exception error)
        {
            var message = error?.Message;
            if (message.IsEmpty()) return false;

            return new[] { "NU1101", "NU1102", "NU1103", "Unable to find package", "is incompatible with" }
                .Any(x => message.Contains(x, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>The error lines of a failed build or restore, without the surrounding chatter.</summary>
        public static string SummariseFailure(Exception error)
        {
            var message = error?.Message.OrEmpty().Trim();

            var errors = message.ToLines().Trim()
                .Where(x => x.Contains("error", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            return errors.Any() ? errors.ToLinesString().Trim() : message;
        }

        /// <summary>Reads "package id" -> "version" out of the target application's deps.json.</summary>
        static Dictionary<string, string> ReadDependencies(FileInfo targetAssembly)
        {
            var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            var file = targetAssembly?.Directory
                .GetFile(Path.GetFileNameWithoutExtension(targetAssembly.Name) + ".deps.json");

            if (file?.Exists() != true) return result;

            try
            {
                using var document = JsonDocument.Parse(file.ReadAllText());

                if (!document.RootElement.TryGetProperty("libraries", out var libraries)) return result;

                foreach (var library in libraries.EnumerateObject())
                {
                    // Project references are listed too, but with a meaningless 1.0.0 version.
                    if (library.Value.TryGetProperty("type", out var type) &&
                        type.GetString() != "package") continue;

                    var parts = library.Name.Split('/');
                    if (parts.Length == 2) result[parts[0]] = parts[1];
                }
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to read {file.FullName}: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Falls back to the version stamped on the assembly itself, for when the target has no
        /// deps.json, e.g. a class library rather than a published application.
        /// </summary>
        static string FromAssemblyFile(string package)
        {
            var file = Folder?.GetFile(package + ".dll");
            if (file?.Exists() != true) return null;

            try
            {
                var informational = FileVersionInfo.GetVersionInfo(file.FullName)
                    .ProductVersion?.Split('+')[0].Trim();

                if (IsVersion(informational)) return informational;

                return AssemblyName.GetAssemblyName(file.FullName).Version?.ToString(3);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to read the version of {file.FullName}: {ex.Message}");
                return null;
            }
        }

        static bool IsVersion(string value)
            => value.HasValue() && Version.TryParse(value.Split('-')[0], out _);

        static string ReadMSharpVersion()
        {
            var nuspec = FindMSharpFolder()?.GetFiles("*.nuspec").FirstOrDefault();
            if (nuspec == null) return null;

            try
            {
                return XDocument.Load(nuspec.FullName)
                    .Descendants().FirstOrDefault(x => x.Name.LocalName == "version")?.Value.Trim()
                    .OrNullIfEmpty();
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to read {nuspec.FullName}: {ex.Message}");
                return null;
            }
        }

        static DirectoryInfo FindMSharpFolder()
        {
            for (var folder = Folder; folder != null; folder = folder.Parent)
            {
                var result = folder.GetSubDirectory("M#");
                if (result.Exists()) return result;
            }

            return null;
        }
    }
}
