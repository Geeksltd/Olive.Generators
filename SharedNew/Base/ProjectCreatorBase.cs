using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Olive;

namespace OliveGenerator
{
    public abstract class ProjectCreatorBase
    {
        protected DirectoryInfo Folder { get; set; }

        internal virtual string Name => Folder.Name;

        public DirectoryInfo MockFolder => Folder.GetOrCreateSubDirectory("Mock");

        static string CachedVersion;

        /// <summary>
        /// How the generated package's version is stamped. The generators do not agree on it, and
        /// changing the shape of a published version is not something consolidating them may do,
        /// so each sets its own before generating anything.
        /// </summary>
        public static string VersionFormat = "yyMMdd.HH.mmss";

        /// <summary>Settled once, so every project of a run carries the same version.</summary>
        public static string Version => CachedVersion ??= LocalTime.Now.ToString(VersionFormat);

        protected abstract string Framework { get; }

        /// <summary>
        /// Every framework this project targets. Two frameworks of one package used to mean two
        /// projects generating identical sources, and so two of everything MSBuild does.
        /// </summary>
        protected virtual string[] Frameworks => new[] { Framework };

        /// <summary>The frameworks element for the project file, singular or plural as needed.</summary>
        string TargetFrameworks => Frameworks.IsSingle()
            ? $"<TargetFramework>{Frameworks.Single()}</TargetFramework>"
            : $"<TargetFrameworks>{Frameworks.ToString(";")}</TargetFrameworks>";

        /// <summary>
        /// The references that apply to one of the targeted frameworks. They differ between them:
        /// the net46 flavour of the M# package carries only M# itself.
        /// </summary>
        protected virtual PackageReference[] ReferencesFor(string framework) => References;
        internal abstract PackageReference[] References { get; }

        /// <summary>The project's own description element, for the generators that write one.</summary>
        protected virtual string ProjectDescription => null;

        /// <summary>
        /// Whether MSBuild stamps the usual assembly attributes onto the generated dll. The data
        /// endpoint packages have always carried them and the others have not, and that is visible
        /// to whoever consumes the package, so it stays a per-generator decision.
        /// </summary>
        protected virtual bool GenerateAssemblyInfo => false;

        /// <summary>
        /// Whether the target's package versions could not be restored, so this generator's own
        /// defaults were used instead. The generated package must then declare those.
        /// </summary>
        internal bool UseFallbackVersions { get; private set; }

        protected ProjectCreatorBase(DirectoryInfo folder) => Folder = folder;

        internal FileInfo ProjectFile => Folder.GetFile(Name + ".csproj");

        /// <summary>
        /// Writes the project and its sources. Building is a separate step, so that every project
        /// of a run can go into one MSBuild invocation.
        /// </summary>
        internal void Generate()
        {
            using var scope = Log.Step("Generate project " + Name);

            Log.Detail("folder: " + Folder.FullName);
            Log.Detail("targets: " + Frameworks.ToString(", "));

            try
            {
                var embedded = AddEmbeddedResources();
                EmbeddedResources = RenderEmbeddedResources(embedded);

                if (embedded?.Any() == true)
                    Log.Detail($"embedded {embedded.Length} reference data file(s).");

                WriteProjectFile();
                ReportReferences();
                AddFiles();
            }
            catch { scope.Fail(); throw; }
        }

        /// <summary>
        /// Which version of each dependency the generated project ended up referencing, and whether
        /// it came from the microservice being generated from. When a published package turns out
        /// to reference something unexpected, this is the line that says why.
        /// </summary>
        void ReportReferences()
        {
            foreach (var framework in Frameworks)
                foreach (var item in ReferencesFor(framework).OrEmpty())
                    Log.Detail($"{framework}: {item.Package} {VersionOf(item)} " +
                        (UseFallbackVersions || !item.IsFromTarget
                            ? "(this generator's default)"
                            : "(the version the target application uses)"));
        }

        /// <summary>Rewrites the project against this generator's own default package versions.</summary>
        internal void FallBackToDefaultVersions()
        {
            UseFallbackVersions = true;
            WriteProjectFile();
        }

        internal void WriteProjectFile()
        {
            ProjectFile.WriteAllText($@"<Project Sdk=""Microsoft.NET.Sdk"">
  <PropertyGroup>
      {TargetFrameworks}
      <GenerateDocumentationFile>true</GenerateDocumentationFile>
      <Version>{Version}</Version>
      <PackageIconUrl>{IconUrl}</PackageIconUrl>
{Describe()}    <RestoreUseStaticGraphEvaluation>true</RestoreUseStaticGraphEvaluation>
    <NoWarn>$(NoWarn);CS1591;NU1701;NU5100</NoWarn>
  </PropertyGroup>
{GetNugetReferences()}{EmbeddedResources}</Project>");

            Log.Diagnostic("wrote " + ProjectFile.FullName);
        }

        /// <summary>The property elements the generators do not agree on.</summary>
        string Describe()
        {
            var result = new StringBuilder();

            if (ProjectDescription.HasValue())
                result.AppendLine($"      <Description>{ProjectDescription}</Description>");

            if (!GenerateAssemblyInfo)
                result.AppendLine("    <GenerateAssemblyInfo>false</GenerateAssemblyInfo>");

            return result.ToString();
        }

        string EmbeddedResources = "";

        /// <summary>
        /// Files to ship inside the generated assembly rather than compile, such as the reference
        /// data of a data endpoint. Most generated projects have none.
        /// </summary>
        protected virtual string[] AddEmbeddedResources() => null;

        static string RenderEmbeddedResources(string[] files)
        {
            if (files == null || files.None()) return "";

            return $@"
  <ItemGroup>
	{files.Select(x => $"<Content Remove=\"{x}\" />").ToLinesString()}
  </ItemGroup>
  <ItemGroup>
	{files.Select(x => $"<EmbeddedResource Include=\"{x}\" />").ToLinesString()}
  </ItemGroup>
";
        }

        /// <summary>
        /// The references go straight into the project file. Adding them one at a time through
        /// "dotnet add package" cost a full nuget restore per package, before the build restored
        /// everything again.
        /// </summary>
        string GetNugetReferences()
        {
            var builder = new StringBuilder();

            foreach (var framework in Frameworks)
            {
                var references = ReferencesFor(framework);
                if (references.None()) continue;

                // Conditional only when multi-targeting, so a single-framework project file reads
                // exactly as it did before.
                var condition = Frameworks.IsSingle() ? null : $" Condition=\"'$(TargetFramework)' == '{framework}'\"";

                builder.AppendLine($"  <ItemGroup{condition}>");

                foreach (var item in references)
                    builder.AppendLine($"    <PackageReference Include=\"{item.Package}\" Version=\"{VersionOf(item)}\" />");

                builder.AppendLine("  </ItemGroup>");
            }

            return builder.ToString();
        }

        /// <summary>The version the generated project was actually built against.</summary>
        internal string VersionOf(PackageReference reference)
            => UseFallbackVersions ? reference.FallbackVersion : reference.Version;

        /// <summary>The version of the given package that the generated project ended up referencing.</summary>
        public string VersionOf(string package)
            => References.FirstOrDefault(x => package.Equals(x.Package, StringComparison.OrdinalIgnoreCase))
                is PackageReference found ? VersionOf(found) : null;

        // ------------------------------------------

        protected abstract void AddFiles();

        internal abstract string IconUrl { get; }

        public virtual IEnumerable<PackageReference> GetNugetDependencies()
        {
            yield break;
        }

        string BinFolder(string framework) => $@"bin\Debug\{framework}";

        string OutputFile(string framework, string extension)
        {
            // Relative to the folder holding the nuspec, which is where nuget resolves it from.
            return System.IO.Path.GetRelativePath(Folder.Parent.FullName,
                Folder.GetSubDirectory(BinFolder(framework)).GetFile(Name + extension).FullName);
        }

        public virtual IEnumerable<string> GetTargetFiles()
        {
            foreach (var framework in Frameworks)
            {
                yield return $@"<file src=""{OutputFile(framework, ".dll")}"" target=""lib\{framework}\"" />";
                yield return $@"<file src=""{OutputFile(framework, ".xml")}"" target=""lib\{framework}\"" />";
            }
        }
    }
}
