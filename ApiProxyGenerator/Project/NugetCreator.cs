namespace OliveGenerator
{
    class NugetCreator : NugetCreatorBase
    {
        public NugetCreator(params ProjectCreatorBase[] projectCreators) : base(Context.Current, projectCreators)
        {
        }

        protected override string BuildDescription()
            => $"Provides an easy method to invoke the Api functions of {Context.Current.ControllerName}";
    }
}