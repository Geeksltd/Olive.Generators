namespace OliveGenerator
{
    class SubscriptionNugetCreator : NugetCreator
    {
        public SubscriptionNugetCreator(ProjectCreatorBase projectCreator) : base(projectCreator)
        {
        }

        protected override string BuildDescription()
            => $"Replicated entity definition interfaces for {Context.Current.EndpointName}. " +
                "Reference from non-M-Sharp consumers and generate entities with CareServe.";
    }
}
