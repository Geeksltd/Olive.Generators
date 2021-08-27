using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

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


            var chosen = SearchInGlobal(global, fileName, version);

            if (chosen != null) return chosen;

            global = Environment.GetEnvironmentVariable("ProgramFiles").AsDirectory()
                .GetSubDirectory("dotnet\\shared\\Microsoft.NETCore.App");

            chosen = SearchInGlobal(global, fileName, version);

            if (chosen != null) return chosen;

            Console.WriteLine("Not found: " + file);

            return null;
        }

        private static Assembly SearchInGlobal(DirectoryInfo folder, string fileName, Version version)
        {
            var matches = folder.GetFiles(fileName, SearchOption.AllDirectories)
                .Select(v => new { File = v.FullName, Version = Version.Parse(v.Directory.Name) })
                .ToArray();

            var chosen = matches.OrderByDescending(x => x.Version == version)
                 .ThenByDescending(x => x.Version.Major == version.Major || x.Version.Minor == version.Minor)
                 .ThenByDescending(x => x.Version)
                 .FirstOrDefault();

            
            if (chosen != null)
                return Assembly.LoadFile(chosen.File);

            return null;

        }
    }

    //    public static Assembly LoadFromAssemblyPath(string assemblyFullPath)
    //    {
    //        var fileNameWithOutExtension = Path.GetFileNameWithoutExtension(assemblyFullPath);
    //        var fileName = Path.GetFileName(assemblyFullPath);
    //        var directory = Path.GetDirectoryName(assemblyFullPath);

    //        var inCompileLibraries = DependencyContext.Default.CompileLibraries.Any(l => l.Name.Equals(fileNameWithOutExtension, StringComparison.OrdinalIgnoreCase));
    //        var inRuntimeLibraries = DependencyContext.Default.RuntimeLibraries.Any(l => l.Name.Equals(fileNameWithOutExtension, StringComparison.OrdinalIgnoreCase));

    //        var assembly = (inCompileLibraries || inRuntimeLibraries)
    //            ? Assembly.Load(new AssemblyName(fileNameWithOutExtension))
    //            : AssemblyLoadContext.Default.LoadFromAssemblyPath(assemblyFullPath);

    //        if (assembly != null)
    //            LoadReferencedAssemblies(assembly, fileName, directory);

    //        return assembly;
    //    }

    //    private static void LoadReferencedAssemblies(Assembly assembly, string fileName, string directory)
    //    {
    //        var filesInDirectory = Directory.GetFiles(directory).Where(x => x != fileName).Select(x => Path.GetFileNameWithoutExtension(x)).ToList();
    //        var references = assembly.GetReferencedAssemblies();

    //        foreach (var reference in references)
    //        {
    //            if (filesInDirectory.Contains(reference.Name))
    //            {
    //                var loadFileName = reference.Name + ".dll";
    //                var path = Path.Combine(directory, loadFileName);
    //                var loadedAssembly = AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
    //                if (loadedAssembly != null)
    //                    LoadReferencedAssemblies(loadedAssembly, loadFileName, directory);
    //            }
    //        }

    //    }
    //}
}
