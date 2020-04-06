using Olive;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;

namespace OliveGenerator
{
    partial class Program
    {
        static int Main(string[] args)
        {
            ParametersParser.SetArgs(args);

            if (!Initialize(args)) return -1;

            try
            {
                ParametersParser.Current.LoadParameters(null);

                Console.WriteLine("Generating Client SDK proxy from...");
                Console.WriteLine("Publisher service: " + Context.Current.PublisherService);
                Console.WriteLine("Api assembly: " + Context.Current.AssemblyFile);
                Console.WriteLine("Api controller: " + Context.Current.ControllerName);
                Console.WriteLine("Temp folder: " + Context.Current.TempPath);
                
                Context.Current.LoadAssembly();
                Context.Current.PrepareOutputDirectory();


                DtoTypes.FindAll(
                    Context.Current.ActionMethods.SelectMany(x => x.GetArgAndReturnTypes()).ToArray()
                    , Context.Current.AssemblyObj);

                DtoDataProviderClassGenerator.ValidateRemoteDataProviderAttributes();

                new List<ProjectCreatorBase> { new ProxyProjectCreator() };

                var proxyCreator = new ProxyProjectCreator();
                proxyCreator.Build();
                //new NugetCreator(proxyCreator).Create();


                DtoTypes.GenerateMSharps();
                //if (DtoTypes.All.Any())
                //{
                //    var projectCreators = new[] { new MSharpProjectCreator(), new MSharp46ProjectCreator() };
                //    projectCreators.AsParallel().Do(x => x.Build());
                //    //new NugetCreator(projectCreators).Create();
                //}


                if (Context.Current.Output != null)
                    Context.Current.TempPath.CopyTo(Context.Current.Output.FullName, true);


                Console.WriteLine("Add done");
                return 0;
            }
            catch (Exception ex)
            {
                ShowError(ex);
                return -1;
            }
        }


    }
}