using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Olive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace OliveGenerator
{
    internal class Restore
    {
        /// <summary>
        /// How many times a restore may be retried after dropping the packages it could not find.
        /// One unavailable package should not cost the cache every other one, but a run that keeps
        /// failing is a problem to report rather than to whittle away at.
        /// </summary>
        const int MaxAttempts = 3;

        static DirectoryInfo TempProject;

        internal static void Start()
        {
            if (Context.Parallel)
                Log.Warn("-parallel no longer does anything: the packages are now restored " +
                    "together in one pass rather than one at a time.");

            ParsePackages();

            if (Context.Packages.None())
            {
                Log.Warn("No packages were listed, so there is nothing to restore.");
                return;
            }

            try
            {
                CreateTempProject();
                RestorePackages();
            }
            finally
            {
                // In a finally: a failed run used to leave its temp project behind for good.
                Cleanup();
            }
        }

        static void ParsePackages()
        {
            using var scope = Log.Step($"Read {Context.FileName}");

            var jsonFile = Path.Combine(Context.BasePath.FullName, Context.FileName);
            var jsonFileValue = JsonConvert.DeserializeObject(File.ReadAllText(jsonFile));
            var jsonFileJArray = JArray.Parse(jsonFileValue.ToString());

            Log.Detail($"{jsonFileJArray.Count} package(s) listed.");

            foreach (var item in jsonFileJArray.Children<JObject>())
            {
                try
                {
                    // By name rather than by position: the file is not guaranteed to list the
                    // properties in the order this tool happens to write them.
                    var package = Value(item, "Package");
                    var version = Value(item, "Version");

                    if (package.IsEmpty()) throw new Exception("It has no package name.");

                    Context.Packages.Add(new NugetPackage(package, version));
                    Log.Diagnostic($"listed: {package} {version}");
                }
                catch (Exception ex)
                {
                    Log.Warn($"One entry of {Context.FileName} was skipped: {ex.Message}");
                }
            }

            scope.Note($"{Context.Packages.Count} usable");
        }

        static string Value(JObject item, string name)
        {
            var property = item.Properties()
                .FirstOrDefault(x => x.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            return property?.Value?.ToString();
        }

        static void CreateTempProject()
        {
            TempProject = Path.Combine(Path.GetTempPath(), "AcceleratePackage", Guid.NewGuid().ToString())
                .AsDirectory();

            TempProject.Create();

            // Written directly rather than through "dotnet new console", which is a process and a
            // template evaluation for a project file this already knows the whole content of.
            TempProject.GetFile("Program.cs").WriteAllText("class Program { static void Main() { } }");

            Log.Detail($"Created a temporary project in {TempProject.FullName}");
        }

        /// <summary>
        /// Pulls every listed package into the local nuget cache with a single restore.
        /// <para>
        /// Each package used to be added with its own "dotnet add package", and every one of those
        /// restores the whole project again, so the work grew with the square of the list. They
        /// also all rewrote one project file, which the -parallel switch then did concurrently.
        /// </para>
        /// </summary>
        static void RestorePackages()
        {
            // Referenced, so their dependencies are cached too, which is what makes the later real
            // restore quick. Downloaded as well, because one project resolves all of them into a
            // single graph, and a version another entry supersedes would otherwise never be
            // fetched even though it is exactly the version something asked to cache.
            var referenced = Context.Packages.ToList();
            var downloaded = Context.Packages.Where(x => IsExactVersion(x.Version)).ToList();

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                WriteProjectFile(referenced, downloaded);

                using var scope = Log.Step($"Restore {referenced.Count} package(s)" +
                    (attempt == 1 ? null : $", attempt {attempt} of {MaxAttempts}"));

                try
                {
                    ProcessRunner.DotNet("restore --nologo", TempProject);
                    return;
                }
                catch (Exception ex) when (attempt < MaxAttempts)
                {
                    // Nowhere to be found, so neither referencing nor downloading it can work.
                    var missing = Blamed(ex, referenced, "NU1101", "NU1102", "NU1103", "Unable to find package");

                    // Real, but with no build for this project's framework. It can still be pulled
                    // into the cache, which is all this tool was asked to do.
                    var incompatible = Blamed(ex, referenced, "NU1202", "NU1201");

                    if (missing.None() && incompatible.None()) throw;

                    scope.Fail();

                    Announce("Not available on any configured feed, so left out of the cache", missing);
                    Announce("No build for this framework, so cached without its dependencies", incompatible);

                    referenced = referenced.Except(missing).Except(incompatible).ToList();
                    downloaded = downloaded.Except(missing).ToList();

                    if (referenced.None() && downloaded.None()) return;
                }
            }
        }

        static void Announce(string what, NugetPackage[] packages)
        {
            if (packages.None()) return;

            Log.Warn($"{what}: " + packages.Select(x => $"{x.Package} {x.Version}").ToString(", "));
        }

        static void WriteProjectFile(IEnumerable<NugetPackage> referenced, IEnumerable<NugetPackage> downloaded)
        {
            var references = new StringBuilder();

            foreach (var item in referenced)
                references.AppendLine(item.Version.HasValue()
                    ? $"    <PackageReference Include=\"{item.Package}\" Version=\"{item.Version}\" />"
                    : $"    <PackageReference Include=\"{item.Package}\" />");

            foreach (var item in downloaded)
                references.AppendLine($"    <PackageDownload Include=\"{item.Package}\" Version=\"[{item.Version}]\" />");

            TempProject.GetFile("AcceleratePackage.csproj").WriteAllText($@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>{Framework}</TargetFramework>
    <!-- The point of the project is the restore, so nothing here should stop one. Unrelated
         entries of one list routinely pin different versions of a shared dependency, and that
         is a downgrade error in a project that has to resolve them all together. -->
    <NoWarn>$(NoWarn);NU1605;NU1701;NU1702</NoWarn>
    <RestoreUseStaticGraphEvaluation>true</RestoreUseStaticGraphEvaluation>
  </PropertyGroup>
  <ItemGroup>
{references}  </ItemGroup>
</Project>");
        }

        /// <summary>A plain version, which is what PackageDownload needs to pin one exactly.</summary>
        static bool IsExactVersion(string version)
            => version.HasValue() && !version.ContainsAny(new[] { "[", "]", "(", ")", "*", "," });

        /// <summary>The framework this tool itself runs on, so the sdk restoring is new enough for it.</summary>
        static string Framework
        {
            get
            {
                var version = Environment.Version;
                return $"net{version.Major}.{version.Minor}";
            }
        }

        /// <summary>The listed packages that the failed restore blamed with one of the given codes.</summary>
        static NugetPackage[] Blamed(Exception error, IEnumerable<NugetPackage> packages, params string[] codes)
        {
            var message = error?.Message;
            if (message.IsEmpty()) return Array.Empty<NugetPackage>();

            var blamed = message.ToLines()
                .Where(line => codes.Any(x => line.Contains(x, StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            if (blamed.None()) return Array.Empty<NugetPackage>();

            return packages
                .Where(p => blamed.Any(line => line.Contains(p.Package, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        static void Cleanup()
        {
            if (TempProject?.Exists() != true) return;

            try { TempProject.Delete(recursive: true); }
            catch (Exception ex)
            {
                Log.Warn($"Could not remove {TempProject.FullName}: {ex.Message}");
            }
        }
    }
}
