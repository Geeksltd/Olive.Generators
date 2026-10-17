using Olive;

namespace OliveGenerator
{
    class MSharpProjectCreator : ProjectCreatorBase
    {
        public MSharpProjectCreator() : base(Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.ControllerType.FullName + "." + "MSharp")) { }

        const string LegacyFramework = "net46";

        /// <summary>
        /// Where the M# leg of the package lands when the target application does not say what it
        /// targets, which only a class library rather than a microservice would fail to do.
        /// </summary>
        const string DefaultFramework = "net8.0";

        /// <summary>
        /// Follows the application being generated from, so an upgraded microservice publishes an
        /// M# package for the framework it now runs on. This used to name one outright, which meant
        /// the moniker only moved when this generator was released again.
        /// </summary>
        protected override string Framework => TargetFramework.Moniker.Or(DefaultFramework);

        /// <summary>
        /// One project targeting both, rather than two projects generating identical sources.
        /// The nuspec still lists a lib folder per framework, so the package is unchanged.
        /// </summary>
        protected override string[] Frameworks => new[] { Framework, LegacyFramework };

        [EscapeGCop]
        internal override string IconUrl => "http://licensing.msharp.co.uk/images/icon.png";

        internal override PackageReference[] References => new[] { PackageReference.Olive, PackageReference.MSharp };

        /// <summary>Olive does not target the legacy framework, so that leg carries only M#.</summary>
        protected override PackageReference[] ReferencesFor(string framework)
            => framework == LegacyFramework ? new[] { PackageReference.MSharp } : References;

        protected override void AddFiles()
        {
            foreach (var type in DtoTypes.All)
            {
                using (Log.Step("Write M# entity definition class " + type.Name, LogLevel.Detailed))
                    Folder.GetFile(type.Name + ".cs").WriteAllText(new MSharpModelProgrammer(type).Generate());
            }
        }
    }
}