using Olive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace OliveGenerator
{
    partial class Program
    {
        static int Main(string[] args)
        {
            ParametersParser.SetArgs(args);

            if (!ParametersParser.Current.Start()) return -1;
            try
            {
                ParametersParser.Current.LoadParameters(null);

                Console.WriteLine("Generating Event Bus Command from...");
                Console.WriteLine("Assembly: " + Context.Current.AssemblyFile);
                Console.WriteLine("Command: " + Context.Current.CommandName);
                Console.WriteLine("Temp folder: " + Context.Current.TempPath);

                Context.Current.LoadAssembly();
                Context.Current.PrepareOutputDirectory();


                DtoTypes.FindAll(Context.Current.CommandType.GetFiledTypes(), Context.Current.AssemblyObj);


                // hashonly argument added to help CI / CD process to identify modified EventBusCommands faster using hash function
                // by detecting "hashonly" in aruments list, this generates a hash to help CI agent to identify modified EventBusCommands
                if (args.Any(x => x.ToLower().Contains("/hashonly")))
                {
                    var hashvalue = HashHelper.HashFieldTypes(Context.Current.CommandType.GetFields());
                    Console.Out.WriteLine();
                    Console.Out.WriteLine($"EventBusCommand Hash = {hashvalue}");
                    return 0;
                }


                new List<ProjectCreatorBase> { new EventBusCommandProjectCreator() };

                var proxyCreator = new EventBusCommandProjectCreator();
                proxyCreator.Build();


                //if (Context.Current.Output != null)
                //    Context.Current.TempPath.CopyTo(Context.Current.Output.FullName, true);

                new NugetCreator(proxyCreator).Create();


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
