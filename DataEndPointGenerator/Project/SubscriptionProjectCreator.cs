using Olive;
using System.Linq;

namespace OliveGenerator
{
    /// <summary>
    /// Builds the <c>{Publisher}.{Consumer}Endpoint.Subscription</c> package: definition interfaces,
    /// concrete EF entities, and <c>{Endpoint}Ef</c> consumer for non-M# apps (AMP #182377).
    /// </summary>
    class SubscriptionProjectCreator : ProjectCreatorBase
    {
        public SubscriptionProjectCreator() : base(
            Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.EndpointType.FullName + ".Subscription"))
        {
        }

        protected override string Framework => "net10.0";

        protected override bool GenerateAssemblyInfo => true;

        protected override string ProjectDescription =>
            "Replicated entity definitions and EF consumer for data endpoint: " + Name;

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        internal static PackageReference[] Packages => new[]
        {
            PackageReference.Olive_Entities,
            PackageReference.Olive_Entities_Data_Replication_QueueUrlProvider,
            PackageReference.Olive_Entities_Data_EF_Replication,
        };

        internal override PackageReference[] References => Packages;

        public override System.Collections.Generic.IEnumerable<PackageReference> GetNugetDependencies() => References;

        protected override void AddFiles()
        {
            var publisherNamespace = Context.Current.EndpointType.Namespace;

            foreach (var item in Context.Current.ExposedTypes)
            {
                var interfaceFile = "I" + item.GetType().Name + ".cs";
                using (Log.Step("Write subscription interface " + interfaceFile, LogLevel.Detailed))
                    Folder.GetFile(interfaceFile).WriteAllText(
                        new SubscriptionInterfaceProgrammer(item, publisherNamespace).Generate());

                var entityFile = item.GetType().Name + ".cs";
                using (Log.Step("Write subscription entity " + entityFile, LogLevel.Detailed))
                    Folder.GetFile(entityFile).WriteAllText(
                        new SubscriptionEntityProgrammer(item, publisherNamespace).Generate());
            }

            var efFile = Context.Current.EndpointName + "Ef.cs";
            using (Log.Step("Write subscription EF endpoint " + efFile, LogLevel.Detailed))
                Folder.GetFile(efFile).WriteAllText(SubscriptionEfEndpointProgrammer.Generate());
        }
    }
}
