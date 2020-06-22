using Olive;
using System;
using System.Collections.Generic;

namespace OliveGenerator
{
    class ProxyProjectCreator : ProjectCreatorBase
    {
        public ProxyProjectCreator() : base(Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.ControllerType.FullName + "." + "Proxy")
            ) { }

        protected override string Framework => "netstandard2.0";

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.ApiProxyGenerator/ProxyIcon.png";

        protected override string[] References
            => new[] { "Olive", "Olive.Entities", "Olive.Entities.Data", "Olive.ApiClient", "Olive.Microservices" };

        protected override void AddFiles()
        {
            Console.Write("Adding the proxy class...");
            Folder.GetFile($"{Context.Current.ControllerName}.cs").WriteAllText(ProxyClassProgrammer.Generate());
            Console.WriteLine("Done");
            Console.Write("Adding the proxy class mock configuration...");
            MockFolder.GetFile($"{Context.Current.ControllerName}.Mock.cs").WriteAllText(ProxyClassProgrammer.GenerateMock());
            Console.WriteLine("Done");
            Console.Write("Adding ReamMe.txt file ...");
            Folder.GetFile("README.txt").WriteAllText(ReadmeFileGenerator.Generate());
            Console.WriteLine("Done");

            DtoTypes.GenerateEnums(Folder);
            DtoTypes.GenerateDtoClasses(Folder);
            GenerateDataProviderClasses();
            GenerateMockConfiguration();
        }

        void GenerateDataProviderClasses()
        {
            foreach (var type in DtoTypes.All)
            {
                Console.Write("Adding DTO class " + type.Name + "...");
                var dto = new DtoProgrammer(type);
                Folder.GetFile(type.Name + ".cs").WriteAllText(dto.Generate());

                var dataProvider = new DtoDataProviderClassGenerator(type, Context.Current).Generate();
                if (dataProvider.HasValue())
                    Folder.GetFile(type.Name + "DataProvider.cs").WriteAllText(dataProvider);

                Console.WriteLine("Done");
            }
        }

        void GenerateMockConfiguration()
        {
            var generator = new MockConfigurationClassGenerator(Context.Current.ControllerType);
            Console.Write($"Adding class {Context.Current.ControllerName}MockConfiguration");
            MockFolder.GetFile($"{Context.Current.ControllerName}MockConfiguration.cs").WriteAllText(generator.Generate());
            Console.WriteLine("Done");
        }

        public override IEnumerable<string> GetTargetFiles()
        {
            var readme = Folder.GetFile("README.txt").FullName;
            return base.GetTargetFiles().Concat($@"<file src=""{readme}"" target="""" />");
        }
    }
}