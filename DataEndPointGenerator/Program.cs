using Olive;
using System;
using System.Collections.Generic;
using System.Linq;

namespace OliveGenerator
{
    partial class Program
    {
        static int Main(string[] args)
        {
            if (!ParametersParser.Start(args)) return -1;
            try
            {
                ParametersParser.LoadParameters();

                Console.WriteLine("Generating Data Endpoint from...");
                Console.WriteLine("Assembly: " + Context.AssemblyFile);
                Console.WriteLine("Endpoint: " + Context.EndpointName);
                Console.WriteLine("Temp folder: " + Context.TempPath);

                Context.LoadAssembly();
                Context.FindExposedTypes();
                Context.PrepareOutputDirectory();

                new List<ProjectCreator> { new EndpointProjectCreator() };

                var endPointCreator = new EndpointProjectCreator();
                endPointCreator.Build();
                new NugetCreator(endPointCreator).Create();

                if (Context.ExposedTypes.Any())
                {
                    var projectCreators = new[] { new MSharpProjectCreator(), new MSharp46ProjectCreator() };
                    projectCreators.AsParallel().Do(x => x.Build());
                    new NugetCreator(projectCreators).Create();
                }

                Console.WriteLine("Add done");
                return 0;
            }
            catch (Exception ex)
            {
                Helper.ShowError(ex);
                return -1;
            }
        }
    }
}