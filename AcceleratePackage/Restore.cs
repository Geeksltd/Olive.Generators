using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Olive;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OliveGenerator
{
    internal class Restore
    {
        static FileInfo NugetConfigFile;
        static DirectoryInfo TempWorkingDir;

        internal static void Start()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Extract Mode");
            Console.ResetColor();

            ParsePackages();
            CreateTempNugetConfig();
            RestorePackages();

            TempWorkingDir.Delete(recursive: true);
        }

        static void ParsePackages()
        {
            Console.WriteLine($"Reading Restore file \"{Context.FileName}\" ");
            var jsonFile = Path.Combine(Context.BasePath.FullName, Context.FileName);
            var jsonFileValue = JsonConvert.DeserializeObject(File.ReadAllText(jsonFile));
            var jsonFileJArray = JArray.Parse(jsonFileValue.ToString());
            Console.WriteLine($"{jsonFileJArray.Count} nuget was found from file.");
            foreach (var item in jsonFileJArray.Children<JObject>())
            {
                try
                {
                    Context.Packages.Add(new NugetPackage(item.Properties().First().Value.ToString(), item.Properties().Last().Value.ToString()));
                    Console.WriteLine($"{item.Properties().First().Value.ToString()} was added to the list");
                }
                catch (Exception ex)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine($"One Nuget from the list  was not add to the list with error : {ex.Message}");
                    Console.ResetColor();
                }
            }

            Console.WriteLine();
        }

        static void CreateTempNugetConfig()
        {
            TempWorkingDir = Path.GetTempPath().AsDirectory().CreateSubdirectory($@"AcceleratePackage\" + Guid.NewGuid());
            NugetConfigFile = TempWorkingDir.GetFile("packages.config");
            var nugetPackages = new XElement("packages");
            Context.Packages.OrderBy(x => x.Package)
                            .Select(x => new XElement("package", new XAttribute("id", x.Package), new XAttribute("version", x.Version)))
                            .Do(x => nugetPackages.Add(x));

            NugetConfigFile.WriteAllText(nugetPackages.ToString());

            Console.WriteLine($"Created Temp nuget package file in {NugetConfigFile.FullName}");
            Console.WriteLine();
        }


        static void RestorePackages()
        {
            Environment.CurrentDirectory = TempWorkingDir.FullName;
            Context.Run("nuget restore -PackagesDirectory \"%userprofile%\\.nuget\\packages\"");
        }
    }
}