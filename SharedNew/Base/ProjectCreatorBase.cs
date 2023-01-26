using System;
using System.Collections.Generic;
using System.IO;
using Olive;

namespace OliveGenerator
{
    public abstract class ProjectCreatorBase
    {
        protected DirectoryInfo Folder { get; set; }

        internal virtual string Name => Folder.Name;

        public DirectoryInfo MockFolder => Folder.GetOrCreateSubDirectory("Mock");

        public static string Version = LocalTime.Now.ToString("yyMMdd.HH.mmss");

        protected abstract string Framework { get; }
        protected abstract string[] References { get; }

        protected ProjectCreatorBase(DirectoryInfo folder) => Folder = folder;

        internal void Build()
        {
            Create();
            AddFiles();
            AddNugetReferences();

            Console.Write("Building " + Folder + "...");
            NugetCreatorBase.Run("dotnet build");
            Console.WriteLine("Done");
        }

        void Create()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("-------------------");
            Console.ResetColor();

            Console.WriteLine("Creating a new class library project at " + Folder.FullName + "...");

            Folder.GetFile(Name + ".csproj")
                .WriteAllText($@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
      <TargetFramework>{Framework}</TargetFramework>
      <DocumentationFile>{BinFolder}\{Name}.xml</DocumentationFile>
      <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
      <Version>{Version}</Version>
      <PackageIconUrl>{IconUrl}</PackageIconUrl>
    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>
  </PropertyGroup>
</Project>");

            Environment.CurrentDirectory = Folder.FullName;
        }

        void AddNugetReferences()
        {
            foreach (var item in References)
            {
                Console.Write("Adding nuget reference " + item + "...");
                NugetCreatorBase.Run("dotnet add package " + item);
                Console.WriteLine("Done");
            }
        }
        // ------------------------------------------

        protected abstract void AddFiles();

        internal abstract string IconUrl { get; }

        public virtual IEnumerable<string> GetNugetDependencies()
        {
            yield break;
        }

        string BinFolder => $@"bin\Debug\{Framework}";

        protected string OutputFile(string extension)
        {
            return Folder.GetSubDirectory(BinFolder)
                .GetFile(Name + extension)
                .FullName.TrimStart(Folder.Parent.FullName).TrimStart("\\");
        }

        public virtual IEnumerable<string> GetTargetFiles()
        {
            yield return $@"<file src=""{OutputFile(".dll")}"" target=""lib\{Framework}\"" />";
            yield return $@"<file src=""{OutputFile(".xml")}"" target=""lib\{Framework}\"" />";
        }
    }
}