namespace OliveGenerator
{
    using Olive;
    using System;
    using System.IO;
    using System.Linq;


    class NugetCreator : NugetCreatorBase
    {

        public NugetCreator(params ProjectCreatorBase[] projectCreators) : base(Context.Current, projectCreators)
        {
        }

        protected override void CreateNuspec()
        {
            base.CreateNuspec(
                $"Provides an easy method to invoke the Api functions of {Context.Current.ControllerName}",
                null
                );

        }
    }
}
