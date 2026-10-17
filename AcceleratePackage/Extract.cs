using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Olive;

namespace OliveGenerator
{
    internal class Extract
    {
        internal static void Start()
        {
            using var scope = Log.Step($"Read the package references of {Context.ProjectFiles.Count} project(s)");

            foreach (var project in Context.ProjectFiles)
            {
                var packages = PackageExtractor.Extract(project);

                // Materialised before it is added to the list it filters against. Left lazy, the
                // count printed below re-ran the query after the add and so always said zero.
                var unique = packages
                    .Where(x => !Context.Packages.Any(y => x.Package == y.Package && x.Version == y.Version))
                    .ToArray();

                if (unique.None()) continue;

                Context.Packages.AddRange(unique);
                Log.Detail($"{project.Name}: {unique.Length} package(s) not already listed");
            }

            scope.Note($"{Context.Packages.Count} distinct packages");

            CreateOutput();
        }

        static void CreateOutput()
        {
            var file = Path.Combine(Context.BasePath.FullName, Context.FileName).AsFile();
            var report = Context.Packages.OrderBy(x => x.Package).Select(x => new { x.Package, x.Version }).ToArray();

            var json = JsonConvert.SerializeObject(report, Formatting.Indented);
            file.WriteAllText(json);

            Log.Info($"Wrote {report.Length} packages to {file.FullName}");
        }
    }
}