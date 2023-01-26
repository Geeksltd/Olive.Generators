﻿namespace OliveGenerator
{
    using System;
    using System.IO;
    using System.Linq;
    using Olive;

    class NugetCreator
    {
        ProjectCreator[] ProjectCreators;
        FileInfo NuspecFile;

        public NugetCreator(params ProjectCreator[] projectCreators)
        {
            ProjectCreators = projectCreators;
            NuspecFile = Context.TempPath.GetFile(ProjectCreators.First().Name + ".nuspec");
        }

        public void Create()
        {
            Console.Write("Creating Nuget packages...");
            CreateNuspec();
            var package = CreateNugetPackage();

            PublishNuget(package);
        }

        FileInfo CreateNugetPackage()
        {
            Environment.CurrentDirectory = NuspecFile.Directory.FullName;
            Context.Run("nuget.exe pack " + NuspecFile.Name);

            return NuspecFile.Directory.GetFiles(NuspecFile.NameWithoutExtension().TrimEnd(".Endpoint") + "*.nupkg", SearchOption.AllDirectories)
                .FirstOrDefault() ?? throw new Exception("Nuget package was not succesfully generated.");
        }

        void PublishNuget(FileInfo package)
        {
            if (Context.Output != null)
                package.CopyTo(Context.Output.GetFile(package.Name));
            else
            {
                Console.Write($"Publishing Nuget package to '{Context.NugetServer}' with key '{Context.NugetApiKey}'...");
                Context.Run($"nuget.exe push {package.Name} {Context.NugetApiKey} -source {Context.NugetServer}");
                Console.WriteLine("Done");
            }
        }

        void CreateNuspec()
        {
            var creator = ProjectCreators.First();
            var folder = creator.Name;

            var nuspec = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>{folder.TrimEnd(".Endpoint")}</id>
    <version>{ProjectCreator.Version}</version>
    <title>{folder}</title>
    <authors>Olive Data End Point Generator</authors>
    <iconUrl>{creator.IconUrl}</iconUrl>
    <description>Provides an easy method to invoke the Api functions of {Context.EndpointName}</description>
    <dependencies>
    {creator.GetNugetDependencies().Select(x => $"<dependency id=\"{x.Package}\" version=\"{x.Version}\" />").ToLinesString()}
    </dependencies>   
  </metadata>
  <files>
     {ProjectCreators.SelectMany(x => x.GetTargetFiles()).ToLinesString()}
  </files>
</package>";

            NuspecFile.WriteAllText(nuspec);
        }

        // static string GetLatestNugetVersion(string package)
        // {
        //    var html = $"https://www.nuget.org/packages/{package}".AsUri().Download()
        //        .RiskDeadlockAndAwaitResult();

        //    var pref = "<meta property=\"og:title\" content=\"" + package + " ";
        //    return html.Substring(pref, "\"", inclusive: false);
        // }
    }
}