//namespace OliveGenerator
//{
//    using Olive;
//    using System;
//    using System.IO;
//    using System.Linq;

//    class NugetCreator
//    {
//        ProjectCreator[] ProjectCreators;
//        FileInfo NuspecFile;

//        public NugetCreator(params ProjectCreator[] projectCreators)
//        {
//            ProjectCreators = projectCreators;
//            NuspecFile = Context2.TempPath.GetFile(ProjectCreators.First().Name + ".nuspec");
//        }

//        public void Create()
//        {
//            Console.Write("Creating Nuget packages...");
//            CreateNuspec();
//            var package = CreateNugetPackage();

//            PublishNuget(package);
//        }

//        FileInfo CreateNugetPackage()
//        {
//            Environment.CurrentDirectory = NuspecFile.Directory.FullName;
//            Context2.Run("nuget.exe pack " + NuspecFile.Name);

//            return NuspecFile.Directory.GetFiles(NuspecFile.NameWithoutExtension().TrimEnd(".Proxy") + "*.nupkg").FirstOrDefault()
//                ?? throw new Exception("Nuget package was not succesfully generated.");
//        }

//        void PublishNuget(FileInfo package)
//        {
//            if (Context2.Output != null)
//                package.CopyTo(Context2.Output.GetFile(package.Name));
//            else
//            {
//                Console.Write($"Publishing Nuget package to '{Context2.NugetServer}' with key '{Context2.NugetApiKey}'...");
//                Context2.Run($"nuget.exe push {package.Name} {Context2.NugetApiKey} -source {Context2.NugetServer}");
//                Console.WriteLine("Done");
//            }
//        }

//        void CreateNuspec()
//        {
//            var creator = ProjectCreators.First();
//            var folder = creator.Name;

//            var nuspec = $@"<?xml version=""1.0"" encoding=""utf-8""?>
//<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
//  <metadata>
//    <id>{folder.TrimEnd(".Proxy")}</id>
//    <version>{ProjectCreator.Version}</version>
//    <title>{folder}</title>
//    <authors>Olive Api Proxy Generator</authors>
//    <iconUrl>{creator.IconUrl}</iconUrl>
//    <description>Provides an easy method to invoke the Api functions of {Context2.ControllerName}</description>  
//  </metadata>
//  <files>
//     {ProjectCreators.SelectMany(x => x.GetTargetFiles()).ToLinesString()}
//  </files>
//</package>";

//            NuspecFile.WriteAllText(nuspec);
//        }

//        static string GetLatestNugetVersion(string package)
//        {
//            var html = $"https://www.nuget.org/packages/{package}".AsUri().Download()
//                .RiskDeadlockAndAwaitResult();

//            var pref = "<meta property=\"og:title\" content=\"" + package + " ";
//            return html.Substring(pref, "\"", inclusive: false);
//        }
//    }
//}
