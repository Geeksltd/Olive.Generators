using System.Collections.Generic;
using Olive;

namespace OliveGenerator
{
    class EventBusCommandProjectCreator : ProjectCreatorBase
    {
        const string NAME = "EventBusCommand";

        public EventBusCommandProjectCreator() : base(
            Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.CommandType.FullName)
            )
        {
        }

        protected override string Framework => "netstandard2.0";

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        /// <summary>What the command project references, readable without creating one, for the hash.</summary>
        internal static PackageReference[] Packages => new[]
        {
            PackageReference.Olive,
            PackageReference.Olive_EventBus,
            PackageReference.Olive_Entities
        };

        internal override PackageReference[] References => Packages;

        protected override void AddFiles()
        {
            using (Log.Step("Write the command class", LogLevel.Detailed))
                Folder.GetFile($"{Context.Current.CommandType.Name}.cs")
                    .WriteAllText(EventBusCommandClassProgrammer.Generate());

            using (Log.Step("Write README.txt", LogLevel.Detailed))
                Folder.GetFile("README.txt").WriteAllText(ReadmeFileGenerator.Generate());

            // The generated command refers to its types by their full name:
            DtoTypes.GenerateEnums(Folder, useNamespace: true);
            DtoTypes.GenerateDtoClasses(Folder);
        }

        public override IEnumerable<string> GetTargetFiles()
        {
            var readme = Folder.GetFile("README.txt").FullName;
            return base.GetTargetFiles().Concat($@"<file src=""{readme}"" target="""" />");
        }

        public override IEnumerable<PackageReference> GetNugetDependencies() => References;
    }
}