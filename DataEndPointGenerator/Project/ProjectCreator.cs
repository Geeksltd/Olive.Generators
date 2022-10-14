using Olive;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace OliveGenerator
{
    abstract class ProjectCreator
    {
        internal virtual string Name => Folder.Name;

        public DirectoryInfo Folder;

        public DirectoryInfo MockFolder => Folder.GetOrCreateSubDirectory("Mock");

        internal static string Version = LocalTime.Now.ToString("yyMM.ddHH.mmss");

        protected abstract string Framework { get; }
        protected abstract PackageReference[] References { get; }

        protected ProjectCreator(string name)
        {
            Folder = Context.TempPath.GetOrCreateSubDirectory(Context.EndpointType.FullName + "." + name);
        }

        protected abstract void AddFiles();

        internal abstract string IconUrl { get; }

        void Create()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("-------------------");
            Console.ResetColor();

            Console.WriteLine("Creating a new class library project at " + Folder.FullName + "...");

            Folder.GetFile(Name + ".csproj").WriteAllText($@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
      <TargetFramework>{Framework}</TargetFramework>
      <DocumentationFile>{BinFolder}\{Name}.xml</DocumentationFile>
      <GeneratePackageOnBuild>true</GeneratePackageOnBuild>
      <Version>{Version}</Version>
      <PackageIconUrl>{IconUrl}</PackageIconUrl>
      <Description>Auto-generated package for data endpoint: {Name}</Description>
  </PropertyGroup>
  <ItemGroup>
    {GetNugetReferences()}
  </ItemGroup>
</Project>");

            Environment.CurrentDirectory = Folder.FullName;
        }

        string GetNugetReferences()
        {
            var builder = new StringBuilder();
            foreach (var item in References)
            {
                builder.AppendLine($"    <PackageReference Include=\"{item.Package}\" Version=\"{item.Version}\" />");
            }
            return builder.ToString();
        }

        //void AddNugetReferences()
        //{
        //    foreach (var item in References)
        //    {
        //        Console.Write("Adding nuget reference " + item.Package + " version : " + item.Version + "...");
        //        Context.Run("dotnet add package " + item.Package + " -v " + item.Version);
        //        Console.WriteLine("Done");
        //    }
        //}

        internal void Build()
        {
            Create();
            AddFiles();
            //AddNugetReferences();

            Console.Write("Building " + Folder + "...");
            Context.Run("dotnet build");
            Console.WriteLine("Done");
        }

        internal virtual IEnumerable<PackageReference> GetNugetDependencies()
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