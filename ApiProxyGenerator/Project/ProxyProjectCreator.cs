using System.Collections.Generic;
using Olive;

namespace OliveGenerator
{
    class ProxyProjectCreator : ProjectCreatorBase
    {
        public ProxyProjectCreator() : base(Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.ControllerType.FullName + "." + "Proxy")
            )
        { }

        protected override string Framework => "netstandard2.0";

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.ApiProxyGenerator/ProxyIcon.png";

        internal override PackageReference[] References => new[]
        {
            PackageReference.Olive,
            PackageReference.Olive_Entities,
            PackageReference.Olive_Entities_Data,
            PackageReference.Olive_ApiClient,
            PackageReference.Olive_Microservices
        };

        protected override void AddFiles()
        {
            using (Log.Step("Write the proxy class", LogLevel.Detailed))
                Folder.GetFile($"{Context.Current.ControllerName}.cs").WriteAllText(ProxyClassProgrammer.Generate());

            using (Log.Step("Write the proxy class mock configuration", LogLevel.Detailed))
                MockFolder.GetFile($"{Context.Current.ControllerName}.Mock.cs")
                    .WriteAllText(ProxyClassProgrammer.GenerateMock());

            using (Log.Step("Write README.txt", LogLevel.Detailed))
                Folder.GetFile("README.txt").WriteAllText(ReadmeFileGenerator.Generate());

            DtoTypes.GenerateEnums(Folder);

            // Nested, because the proxy names a nested DTO through the controller class, and that
            // class is generated here too for the declaration to merge into.
            DtoTypes.GenerateDtoClasses(Folder, nested: true);

            GenerateDataProviderClasses();
            GenerateMockConfiguration();
        }

        /// <summary>
        /// The remote data provider of each DTO that has one. The DTO classes themselves are written
        /// by GenerateDtoClasses above; this used to write them a second time here, over the top of
        /// what that had just produced.
        /// </summary>
        void GenerateDataProviderClasses()
        {
            foreach (var type in DtoTypes.All)
            {
                var generator = new DtoDataProviderClassGenerator(type, Context.Current);

                var dataProvider = generator.Generate();
                if (dataProvider.IsEmpty()) continue;

                using (Log.Step("Write remote data provider " + generator.ClassName, LogLevel.Detailed))
                    Folder.GetFile(generator.ClassName + ".cs").WriteAllText(dataProvider);
            }
        }

        void GenerateMockConfiguration()
        {
            var generator = new MockConfigurationClassGenerator(Context.Current.ControllerType);

            using (Log.Step($"Write class {Context.Current.ControllerName}MockConfiguration", LogLevel.Detailed))
                MockFolder.GetFile($"{Context.Current.ControllerName}MockConfiguration.cs")
                    .WriteAllText(generator.Generate());
        }

        public override IEnumerable<string> GetTargetFiles()
        {
            var readme = Folder.GetFile("README.txt").FullName;
            return base.GetTargetFiles().Concat($@"<file src=""{readme}"" target="""" />");
        }
    }
}