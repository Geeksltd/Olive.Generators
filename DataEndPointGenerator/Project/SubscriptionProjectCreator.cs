using Olive;
using System.Linq;

namespace OliveGenerator
{
    /// <summary>
    /// Builds the <c>{Publisher}.{Consumer}Endpoint.Subscription</c> package: definition interfaces for non-M# consumers.
    /// </summary>
    class SubscriptionProjectCreator : ProjectCreatorBase
    {
        public SubscriptionProjectCreator() : base(
            Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.EndpointType.FullName + ".Subscription"))
        {
        }

        protected override string Framework => "netstandard2.0";

        protected override bool GenerateAssemblyInfo => true;

        protected override string ProjectDescription =>
            "Replicated entity definition interfaces for data endpoint: " + Name;

        [EscapeGCop]
        internal override string IconUrl
            => "https://raw.githubusercontent.com/Geeksltd/Olive/master/Integration/Olive.DataEndpointGenerator/EndpointIcon.png";

        internal static PackageReference[] Packages => new[] { PackageReference.Olive_Entities };

        internal override PackageReference[] References => Packages;

        public override System.Collections.Generic.IEnumerable<PackageReference> GetNugetDependencies() => References;

        protected override void AddFiles()
        {
            var publisherNamespace = Context.Current.EndpointType.Namespace;

            foreach (var item in Context.Current.ExposedTypes)
            {
                var fileName = "I" + item.GetType().Name + ".cs";
                using (Log.Step("Write subscription interface " + fileName, LogLevel.Detailed))
                    Folder.GetFile(fileName).WriteAllText(
                        new SubscriptionInterfaceProgrammer(item, publisherNamespace).Generate());
            }
        }
    }
}
