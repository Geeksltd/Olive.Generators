using Newtonsoft.Json;
using Olive;
using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace OliveGenerator
{
    internal class Extract
    {
        internal static void Start()
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.ResetColor();
            Console.WriteLine("Finding packages...");

            foreach (var project in Context.ProjectFiles)
            {
                var packages = PackageExtractor.Extract(project);
                var unique = packages.Where(x => !Context.Packages.Any(y => x.Package == y.Package && x.Version == y.Version));
                if (unique.None()) continue;

                Context.Packages.AddRange(unique);
                Console.WriteLine($"Found package: {project.Name} : {unique.Count()}");
            }

            Console.WriteLine();
            CreateOutput();
        }

        static void CreateOutput()
        {
            var file = Path.Combine(Context.BasePath.FullName, Context.FileName).AsFile();
            var nugetPackages = new XElement("packages");
            Context.Packages.OrderBy(x => x.Package)
                            .Select(x => new XElement("package", new XAttribute("id", x.Package), new XAttribute("version", x.Version)))
                            .Do(x => nugetPackages.Add(x));

            file.WriteAllText(nugetPackages.ToString());

            Console.WriteLine($"Generating Output file \"{Context.FileName}\"  at \"{Context.BasePath.FullName}\"");
            Console.WriteLine();
        }


    }
}