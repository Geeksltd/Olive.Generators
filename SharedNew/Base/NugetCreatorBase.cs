namespace OliveGenerator
{
    using Olive;
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;

    public abstract class NugetCreatorBase
    {
        protected ContextBase theContext;
        protected ProjectCreatorBase[] ProjectCreators;
        protected FileInfo NuspecFile;

        protected NugetCreatorBase(ContextBase context, params ProjectCreatorBase[] projectCreators)
        {
            theContext = context;
            ProjectCreators = projectCreators;
            NuspecFile = theContext.TempPath.GetFile(ProjectCreators.First().Name + ".nuspec");
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

            Run("nuget.exe pack " + NuspecFile.Name);

            return NuspecFile.Directory.GetFiles(NuspecFile.NameWithoutExtension().TrimEnd(".Proxy") + "*.nupkg", SearchOption.AllDirectories)
                .FirstOrDefault() ?? throw new Exception("Nuget package was not succesfully generated.");
        }

        void PublishNuget(FileInfo package)
        {
            if (theContext.Output != null)
                package.CopyTo(theContext.Output.GetFile(package.Name));
            else
            {
                Console.Write($"Publishing Nuget package to '{theContext.NugetServer}' with key '{theContext.NugetApiKey}'...");
                Run($"nuget.exe push {package.Name} {theContext.NugetApiKey} -source {theContext.NugetServer}");
                Console.WriteLine("Done");
            }
        }

        internal static string Run(string command)
        {
            var cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            var result = cmd.StandardOutput.ReadToEnd().ToStringOrEmpty().Trim();

            if (result.StartsWith("Could not ")) throw new Exception(result);

            if (result.Contains("Build FAILED")) throw new Exception(result.RemoveBefore("Build FAILED"));

            return result;
        }

        internal static void AddNugetReferences(string[] References)
        {
            foreach (var item in References)
            {
                Console.Write("Adding nuget reference " + item + "...");
                Run("dotnet add package " + item);
                Console.WriteLine("Done");
            }
        }

        protected abstract void CreateNuspec();

        protected virtual void CreateNuspec(string description, string dependencies)
        {
            var creator = ProjectCreators.First();
            var folder = creator.Name;

            var nuspec = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>{folder.TrimEnd(".Command")}</id>
    <version>{ProjectCreatorBase.Version}</version>
    <title>{folder}</title>
    <authors>Olive Data End Point Generator</authors>
    <iconUrl>{creator.IconUrl}</iconUrl>
    <description>{description }</description>
    { (dependencies.HasValue() ? $"<dependencies>{dependencies}</dependencies>" : null)}
  </metadata>
  <files>
     {ProjectCreators.SelectMany(x => x.GetTargetFiles()).ToLinesString()}
  </files>
</package>";

            NuspecFile.WriteAllText(nuspec);
        }

        protected static string GetLatestNugetVersion(string package)
        {
            var html = $"https://www.nuget.org/packages/{package}".AsUri().Download()
                .RiskDeadlockAndAwaitResult();

            var pref = "<meta property=\"og:title\" content=\"" + package + " ";
            return html.Substring(pref, "\"", inclusive: false);
        }
    }
}
