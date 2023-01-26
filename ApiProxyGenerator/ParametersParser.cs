using System;
using System.IO;
using System.Linq;
using Olive;

namespace OliveGenerator
{
    class ParametersParser : ParametersParserBase
    {
        public ParametersParser(ContextBase context, string[] args) : base(context, args)
        {
        }
        public static ParametersParser SetArgs(string[] args) { return Current = new ParametersParser(Context.Current, args); }

        public static ParametersParser Current { get; private set; }

        public virtual bool Start()
        {
            base.Start();

            if (Param("controller").IsEmpty()) return false;
            return Param("out").HasValue() || Param("push").HasValue();
        }

        public override void LoadParameters(string defaultName)
        {
            Context.Current.ControllerName = Param("controller");
            base.LoadParameters("api-proxy");
            GetServiceName();
        }

        // static bool LoadFromControllerFile(FileInfo file)
        // {
        //    if (file == null || !file.Exists)
        //    {
        //        Console.WriteLine(file?.FullName + " controller file does not exist!");
        //        return false;
        //    }

        //    var lines = file.ReadAllText().ToLines().Trim();
        //    var @namespace = lines.FirstOrDefault(x => x.StartsWith("namespace "))?.TrimBefore("namespace ", trimPhrase: true);
        //    var @class = lines.FirstOrDefault(x => x.StartsWith("public class "))?
        //        .TrimBefore("public class ", trimPhrase: true).TrimAfter(" ");

        //    Context.ControllerName = @namespace.WithSuffix(".") + @class;

        //    var directory = file.Directory;
        //    while (directory.Name.ToLower() != "website")
        //    {
        //        directory = directory.Parent;
        //        if (directory.Root == directory) return false;
        //    }

        //    return LoadFromWebsite(directory);
        // }

        // static bool LoadFromWebsite(DirectoryInfo websiteFolder)
        // {
        //    if (!websiteFolder.Exists)
        //    {
        //        Console.WriteLine(websiteFolder.FullName + " does not exist!");
        //        return false;
        //    }

        //    Context.TempPath = websiteFolder.CreateSubdirectory("obj\\api-proxy");

        //    Console.WriteLine("Processing " + websiteFolder.FullName + "...");

        //    Context.AssemblyFile = websiteFolder.GetFile("bin\\Debug\\netcoreapp2.1\\Website.dll");
        //    Directory.SetCurrentDirectory(websiteFolder.FullName);

        //    Context.PublisherService = websiteFolder.GetFile("appSettings.json").ReadAllText().ToLines().Trim()
        //          .SkipWhile(x => !x.StartsWith("\"Microservice\":"))
        //          .FirstOrDefault(x => x.StartsWith("\"Name\":"))
        //          ?.TrimBefore(":", trimPhrase: true).TrimEnd(",").Trim(' ', '\"');

        //    if (Context.PublisherService.IsEmpty())
        //    {
        //        Console.WriteLine("Setting of Microservice:Name under appSettings.json was not found.");
        //        return false;
        //    }

        //    return true;
        // }

        string GetServiceName()
        {
            var value = base.Param("serviceName");
            if (value.HasValue()) return value;

            var appSettings = FindAppSettings();

            value = appSettings.ReadAllText().ToLines().Trim()
                    .SkipWhile(x => !x.StartsWith("\"Microservice\":"))
                    .SkipWhile(x => !x.StartsWith("\"Me\":"))
                    .FirstOrDefault(x => x.StartsWith("\"Name\":"))
                    ?.RemoveBeforeAndIncluding(":").TrimEnd(",").Trim(' ', '\"');

            if (value.IsEmpty())
                throw new Exception("Failed to find Microservice:Me:Name in " + appSettings.FullName);

            return value;
        }

        static FileInfo FindAppSettings()
        {
            var dir = Context.Current.Source.Parent;

            while (dir.Root.FullName != dir.FullName)
            {
                var result = dir.GetFile("appSettings.json");
                if (result.Exists()) return result;
                dir = dir.Parent;
            }

            throw new Exception("Failed to find appSettings.json in any of the parent directories.");
        }
    }
}