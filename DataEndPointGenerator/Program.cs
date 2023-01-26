using Olive;
using Olive.SharedHelper;
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

                /// hashonly argument added to Olive.Endpoint Generator to help CI/CD process to identify modified Endpoints faster using hash function
                /// by detecting "hashonly" in aruments list, Olive.Endpoint.Generator generates a hash to help CI agent to identify modified Endpoints
                if (args.Any(x => x.ToLower().Contains("/hashonly")))
                {
                    var hashvalue = HashHelper.HashExposedType(Context.ExposedTypes);
                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"Endpoint Hash = {hashvalue}");
                    return 0;
                }

                Context.PrepareOutputDirectory();

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