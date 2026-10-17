using Olive;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OliveGenerator
{
    class EndpointProjectCreator : ProjectCreatorBase
    {
        public EndpointProjectCreator() : base(
            Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.EndpointType.FullName + ".Endpoint"))
        {
        }

        protected override string Framework => "netstandard2.0";

        /// <summary>The data endpoint packages have always carried the usual assembly attributes.</summary>
        protected override bool GenerateAssemblyInfo => true;

        protected override string ProjectDescription => "Auto-generated package for data endpoint: " + Name;

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        /// <summary>What the endpoint project references, readable without creating one, for the hash.</summary>
        internal static PackageReference[] Packages
            => new[] {
                PackageReference.Olive_Entities_Data_Replication,
                PackageReference.Olive_Entities_Data_Replication_QueueUrlProvider,
            };

        internal override PackageReference[] References => Packages;

        protected override void AddFiles()
        {
            using (Log.Step("Write the endpoint class", LogLevel.Detailed))
                Folder.GetFile($"{Context.Current.EndpointName}.cs").WriteAllText(EndpointClassProgrammer.Generate());

            using (Log.Step("Write README.txt", LogLevel.Detailed))
                Folder.GetFile("README.txt").WriteAllText(ReadmeFileGenerator.Generate());
        }

        public override IEnumerable<string> GetTargetFiles()
        {
            var readme = Folder.GetFile("README.txt").FullName;
            return base.GetTargetFiles().Concat($@"<file src=""{readme}"" target="""" />");
        }

        /// <summary>
        /// The generated endpoint class derives from DestinationEndpoint and resolves its queue url
        /// through the provider, so consumers need both. Declaring them lets nuget say which version
        /// is missing at restore time, rather than leaving it to fail as a binding error at runtime.
        /// </summary>
        public override IEnumerable<PackageReference> GetNugetDependencies() => References;

        protected override string[] AddEmbeddedResources()
        {
            const string key = "ReferenceData";

            var files = Context.Current.FindReferenceDataFiles();

            if (files == null || !files.Any())
            {
                Log.Detail("No reference data files were found for this endpoint.");
                return Array.Empty<string>();
            }

            foreach (var file in files)
            {
                var newFileName = System.IO.Path.Combine(Folder.FullName, key + file.FullName?.Split(key)?.LastOrDefault()).AsFile();
                if (!newFileName.Directory.Exists()) newFileName.Directory.Create();
                file.CopyTo(newFileName, overwrite: true);
                Log.Detail("reference data: " + file.FullName);
            }

            return Folder
              .GetSubDirectory(key)
              .GetFiles("*.json", System.IO.SearchOption.AllDirectories)
              .Select<System.IO.FileInfo, string>(x => key + x.FullName?.Split(key)?.LastOrDefault())
              .ToArray();
        }
    }
}
