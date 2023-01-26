namespace OliveGenerator
{
    using System.Linq;
    using Olive;

    class NugetCreator : NugetCreatorBase
    {
        public NugetCreator(params ProjectCreatorBase[] projectCreators) : base(Context.Current, projectCreators)
        {
        }

        protected override void CreateNuspec()
        {
            base.CreateNuspec(
                $"Provides an easy method to invoke the Api functions of {Context.Current.CommandName}",
                $"{ProjectCreators.First().GetNugetDependencies().Select(x => $"<dependency id=\"{x}\" version=\"{GetLatestNugetVersion(x)}\" />").ToLinesString()}"
                );
        }
    }
}