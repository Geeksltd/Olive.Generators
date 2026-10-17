namespace OliveGenerator
{
    using System;
    using System.IO;
    using System.Linq;
    using Olive;

    // Aliased rather than imported: NuGet.Packaging has a PackageReference of its own, which would
    // collide with this project's.
    using PackageBuilder = NuGet.Packaging.PackageBuilder;

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

        string Description;

        /// <summary>
        /// The item this package belongs to, captured while the context still points at it, so the
        /// run summary can attribute the package even though packing happens long afterwards.
        /// </summary>
        string Item;

        /// <summary>The projects this package is built from, so a caller can skip it when one failed.</summary>
        internal ProjectCreatorBase[] Projects => ProjectCreators;

        /// <summary>
        /// Captures the part of the nuspec that depends on the item the context points at, while
        /// that is still the item being generated. The nuspec itself is written after the build,
        /// because the dependency versions it declares are only settled then: a failed restore
        /// makes the build fall back to this generator's own defaults.
        /// </summary>
        public NugetCreatorBase PrepareNuspec()
        {
            Description = BuildDescription();
            Item = Log.Item;
            return this;
        }

        /// <summary>Writes the nuspec, packs and publishes, once the projects have been built.</summary>
        public void PackAndPublish()
        {
            var previous = Log.Item;
            Log.Item = Item;

            using var scope = Log.Step("Pack " + PackageId);

            var reported = RunSummary.Current.For(Item).Package(PackageId);
            reported.Version = ProjectCreatorBase.Version;

            try
            {
                WriteNuspec(Description, BuildDependencies());
                var package = CreateNugetPackage();

                Log.Detail($"packed {package.FullName} ({package.Length / 1024}KB)");

                reported.Destination = PublishNuget(package);
            }
            catch (Exception ex)
            {
                scope.Fail();
                reported.Failed = true;
                reported.Failure = ex.Message;
                Log.Error("Failed to publish " + PackageId, ex);
            }
            finally { Log.Item = previous; }
        }

        /// <summary>The id of the package this nuspec declares.</summary>
        protected virtual string PackageId => ProjectCreators.First().Name.TrimEnd(".Command");

        /// <summary>
        /// Builds the nupkg from the nuspec written above. This is the same packaging library that
        /// nuget.exe pack is itself a front end for, run in process: it reads the same file, applies
        /// the same version normalisation and resolves the same relative sources against the folder
        /// holding the nuspec. Doing it here rather than through nuget.exe means the generators no
        /// longer need a tool on the PATH that the dotnet CLI does not ship, and a bad nuspec comes
        /// back as an exception rather than as scraped output.
        /// </summary>
        FileInfo CreateNugetPackage()
        {
            var folder = NuspecFile.Directory;

            // Its own output folder per package, so nothing here can be confused with the package of
            // another project in the same tree: the endpoint package's id is a prefix of the M#
            // package's file name.
            var output = folder.GetOrCreateSubDirectory(".packages").GetOrCreateSubDirectory(PackageId);
            output.GetFiles("*.nupkg").Do(x => x.Delete());

            PackageBuilder builder;
            using (var nuspec = NuspecFile.OpenRead())
                builder = new PackageBuilder(nuspec, folder.FullName);

            // Named the way nuget names it, from the normalised version rather than the one written
            // into the nuspec: a segment such as "0412" is normalised to "412".
            var package = output.GetFile($"{builder.Id}.{builder.Version.ToNormalizedString()}.nupkg");

            using (var stream = package.Create())
                builder.Save(stream);

            package.Refresh();

            return package;
        }

        /// <summary>Copies or pushes the package, and returns where it went.</summary>
        string PublishNuget(FileInfo package)
        {
            if (theContext.Output != null)
            {
                package.CopyTo(theContext.Output.GetFile(package.Name));
                Log.Info($"{package.Name} -> {theContext.Output.FullName}");

                return theContext.Output.FullName;
            }

            using var scope = Log.Step($"Push {package.Name} to {theContext.NugetServer}");

            var key = theContext.NugetApiKey;

            // Passed only when there is one. An empty --api-key is an argument the CLI rejects,
            // where nuget.exe simply saw one word fewer on its command line.
            var arguments = $"nuget push \"{package.Name}\" --source {theContext.NugetServer}" +
                (key.IsEmpty() ? null : $" --api-key {key}");

            try
            {
                // The key is deliberately not echoed: this line goes into the build log.
                ProcessRunner.DotNet(arguments, package.Directory, secret: key);
            }
            catch { scope.Fail(); throw; }

            return theContext.NugetServer;
        }

        /// <summary>The package description. Read while the context still points at this item.</summary>
        protected abstract string BuildDescription();

        /// <summary>The dependency elements, read after the build so the versions are final.</summary>
        protected virtual string BuildDependencies() => null;

        void WriteNuspec(string description, string dependencies)
        {
            var creator = ProjectCreators.First();
            var folder = creator.Name;

            var nuspec = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<package xmlns=""http://schemas.microsoft.com/packaging/2010/07/nuspec.xsd"">
  <metadata>
    <id>{PackageId}</id>
    <version>{ProjectCreatorBase.Version}</version>
    <title>{folder}</title>
    <authors>{Authors}</authors>
    <iconUrl>{creator.IconUrl}</iconUrl>
    <description>{description}</description>
    {(dependencies.HasValue() ? $"<dependencies>{dependencies}</dependencies>" : null)}
  </metadata>
  <files>
     {ProjectCreators.SelectMany(x => x.GetTargetFiles()).ToLinesString()}
  </files>
</package>";

            NuspecFile.WriteAllText(nuspec);
            Log.Diagnostic("wrote " + NuspecFile.FullName + Environment.NewLine + nuspec);
        }

        /// <summary>Who the published package says produced it.</summary>
        protected virtual string Authors => "Olive Data End Point Generator";

        /// <summary>
        /// The version to declare for a dependency of the generated package: whatever the project
        /// was actually built against, so the package lines up with its publisher.
        /// </summary>
        protected static string GetDependencyVersion(ProjectCreatorBase creator, PackageReference package)
            => creator?.VersionOf(package) ?? package.FallbackVersion;
    }
}
