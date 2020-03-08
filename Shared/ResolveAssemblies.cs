using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Olive;

namespace OliveGenerator
{
    public static class ResolveAssembliesExtensions
    {
        public static void ResolveAssemblies(this AppDomain domain)
        {
            domain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
        {
            var fileName = args.Name.Split(',').Select(x => x.Trim())
                .FirstOrDefault(x => x.HasValue())?.ToLower();

            if (fileName.IsEmpty()) return null;
            else fileName = fileName.ToLower();

            if (!fileName.EndsWith(".dll")) fileName += ".dll";

            var file = Path.Combine(Environment.CurrentDirectory, fileName);
            if (File.Exists(file)) return Assembly.LoadFile(file);

            var version = Version.Parse(args.Name.Substring("Version=", ",", inclusive: false));

            var global = Environment.GetEnvironmentVariable("ProgramFiles").AsDirectory()
                .GetSubDirectory("dotnet\\shared\\Microsoft.AspNetCore.App");

            var matches = global.GetFiles(fileName, SearchOption.AllDirectories)
                .Where(v => v.Directory.Name.All(c => c.IsDigit() || c == '.'))
                .Select(v => new { File = v.FullName, Version = Version.Parse(v.Directory.Name) })
                .ToArray();

            var chosen = matches.OrderByDescending(x => x.Version == version)
                 .ThenByDescending(x => x.Version.Major == version.Major || x.Version.Minor == version.Minor)
                 .ThenByDescending(x => x.Version)
                 .FirstOrDefault();

            if (chosen != null)
                return Assembly.LoadFile(chosen.File);

            Console.WriteLine("Not found: " + file);
            return null;
        }
    }
}
