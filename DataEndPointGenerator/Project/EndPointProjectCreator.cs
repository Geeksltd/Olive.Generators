using Olive;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OliveGenerator
{
    class EndpointProjectCreator : ProjectCreator
    {
        public EndpointProjectCreator() : base("Endpoint") { }

        protected override string Framework => "netstandard2.0";

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        protected override PackageReference[] References
            => new[] {
                PackageReference.Olive_Entities_Data_Replication,
                PackageReference.Olive_Entities_Data_Replication_QueueUrlProvider,
            };

        protected override void AddFiles()
        {
            Console.Write("Adding the endpoint class...");
            Folder.GetFile($"{Context.EndpointName}.cs").WriteAllText(EndpointClassProgrammer.Generate());
            Console.WriteLine("Done");
            Console.Write("Adding ReamMe.txt file ...");
            Folder.GetFile("README.txt").WriteAllText(ReadmeFileGenerator.Generate());
            Console.WriteLine("Done");
        }

        public override IEnumerable<string> GetTargetFiles()
        {
            var readme = Folder.GetFile("README.txt").FullName;
            return base.GetTargetFiles().Concat($@"<file src=""{readme}"" target="""" />");
        }

        internal override IEnumerable<PackageReference> GetNugetDependencies()
        {
            yield break;
            // return new[]
            // {
            //    PackageReference.Olive_Entities_Data_Replication
            // };
        }

        protected override string[] AddEmbeddedResources()
        {
            const string key = "ReferenceData";

            var files = Context.FindReferenceDataFiles();

            if (files == null || !files.Any())
                return Array.Empty<string>();

            foreach (var file in files)
            {
                var newFileName = System.IO.Path.Combine(Folder.FullName, key + file.FullName?.Split(key)?.LastOrDefault()).AsFile();
                if (!newFileName.Directory.Exists()) newFileName.Directory.Create();
                file.CopyTo(newFileName, overwrite: true);
            }

            return Folder
              .GetSubDirectory(key)
              .GetFiles("*.json", System.IO.SearchOption.AllDirectories)
              .Select<System.IO.FileInfo, string>(x => key + x.FullName?.Split(key)?.LastOrDefault())
              .ToArray();
        }
    }
}