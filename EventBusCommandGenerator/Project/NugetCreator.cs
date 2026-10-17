namespace OliveGenerator
{
    using System.Linq;
    using Olive;

    class NugetCreator : NugetCreatorBase
    {
        public NugetCreator(params ProjectCreatorBase[] projectCreators) : base(Context.Current, projectCreators)
        {
        }

        protected override string BuildDescription()
            => $"Provides an easy method to invoke the Api functions of {Context.Current.CommandName}";

        protected override string BuildDependencies()
            => ProjectCreators.First().GetNugetDependencies()
                .Select(x => $"<dependency id=\"{x.Package}\" version=\"{GetDependencyVersion(ProjectCreators.First(), x)}\" />")
                .ToLinesString();
    }
}