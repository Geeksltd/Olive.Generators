namespace OliveGenerator
{
    using System.Linq;
    using Olive;

    class NugetCreator : NugetCreatorBase
    {
        public NugetCreator(params ProjectCreatorBase[] projectCreators) : base(Context.Current, projectCreators)
        {
        }

        /// <summary>The endpoint package drops the ".Endpoint" suffix its project folder carries.</summary>
        protected override string PackageId => ProjectCreators.First().Name.TrimEnd(".Endpoint");

        protected override string BuildDescription()
            => $"Provides an easy method to invoke the Api functions of {Context.Current.EndpointName}";

        protected override string BuildDependencies()
            => ProjectCreators.First().GetNugetDependencies()
                .Select(x => $"<dependency id=\"{x.Package}\" version=\"{GetDependencyVersion(ProjectCreators.First(), x)}\" />")
                .ToLinesString();
    }
}
