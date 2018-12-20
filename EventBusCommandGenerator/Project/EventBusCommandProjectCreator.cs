using Olive;
using System;
using System.Collections.Generic;

namespace OliveGenerator
{
    class EventBusCommandProjectCreator : ProjectCreator
    {
        public EventBusCommandProjectCreator() : base("EventBusCommand") { }

        protected override string Framework => "netstandard2.0";

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        protected override string[] References
            => new[] { "Olive", "Olive.EventBus", "Olive.Entities" };

        protected override void AddFiles()
        {
            Console.Write("Adding the command class...");
            Folder.GetFile($"{Context.CommandType.Name}.cs").WriteAllText(EventBusCommandClassProgrammer.Generate());
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
                "Olive.EventBus"
            };
        }
    }
}