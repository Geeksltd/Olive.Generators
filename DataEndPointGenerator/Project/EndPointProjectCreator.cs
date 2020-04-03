using Olive;
using System;
using System.Collections.Generic;

namespace OliveGenerator
{
    class EndpointProjectCreator : ProjectCreator
    {
        public EndpointProjectCreator() : base("Endpoint") { }

        protected override string Framework => "netstandard2.0";

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        protected override string[] References
            => new[] { "Olive", "Olive.Entities", "Olive.Entities.Data.Replication" };

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

        internal override IEnumerable<string> GetNugetDependencies()
        {
            return new[]
            {
                "Olive",
                "Olive.Entities",
                "Olive.Entities.Data.Replication",
            };
        }
    }
}