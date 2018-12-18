using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Olive;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OliveGenerator
{
    internal class Restore
    {
        static string TempProjectPath, TempProjectName;

        internal static void Start()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Extract Mode");
            Console.ResetColor();

            ParsePackages();
            CreateTempDirectory();
            CreateTempProject();
            AddPackages();

            Environment.CurrentDirectory = AppDomain.CurrentDomain.BaseDirectory;
            TempProjectPath.AsDirectory().Delete(recursive: true);
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

        static void CreateTempDirectory()
        {
            TempProjectPath = Path.Combine(Path.GetTempPath(), $@"AcceleratePackage\" + Guid.NewGuid());
            Directory.CreateDirectory(TempProjectPath);
            Console.WriteLine($"Created Temp Directory in {TempProjectPath}");
            Console.WriteLine();
        }

        static void CreateTempProject()
        {
            TempProjectName = Guid.NewGuid().ToString();
            Environment.CurrentDirectory = TempProjectPath;
            Context.Run($@"dotnet new console -n {TempProjectName} -lang C# ");
            Console.WriteLine($"Created Temp Project with name \"{TempProjectName}\" in \"{TempProjectPath}\"");
            Console.WriteLine();
        }

        static void AddPackages()
        {
            Console.WriteLine($"Adding Packges to the Project : {TempProjectName}");
            Environment.CurrentDirectory = Path.Combine(TempProjectPath, TempProjectName);

            void add(NugetPackage item)
            {
                Context.Run("dotnet add package " + item.Package + " -v " + item.Version);
                Console.WriteLine("Added nuget reference " + item.Package + " > " + item.Version + "...");
            }

            if (Context.Parallel)
            {
                var addTasks = Context.Packages.Select(item => Task.Factory.StartNew(() => add(item)));
                Task.Factory.RunSync(() => Task.WhenAll(addTasks));
            }
            else
            {
                Context.Packages.Do(x => add(x));
            }

            Console.WriteLine();
        }
    }
}