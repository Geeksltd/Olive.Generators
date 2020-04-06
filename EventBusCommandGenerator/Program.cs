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


                new List<ProjectCreatorBase> { new EventBusCommandProjectCreator() };

                var proxyCreator = new EventBusCommandProjectCreator();
                proxyCreator.Build();


                if (Context.Current.Output != null)
                    Context.Current.TempPath.CopyTo(Context.Current.Output.FullName, true);

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
