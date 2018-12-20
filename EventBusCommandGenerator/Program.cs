using System;
using System.Collections.Generic;

namespace OliveGenerator
{
    class Program
    {
        static int Main(string[] args)
        {
            if (!ParametersParser.Start(args)) return -1;
            try
            {
                ParametersParser.LoadParameters();

                Console.WriteLine("Generating Event Bus Command from...");
                Console.WriteLine("Assembly: " + Context.AssemblyFile);
                Console.WriteLine("Command: " + Context.CommandName);
                Console.WriteLine("Temp folder: " + Context.TempPath);

                Context.LoadAssembly();
                Context.PrepareOutputDirectory();

                new List<ProjectCreator> { new EventBusCommandProjectCreator() };

                var eventBusCommandProjectCreator = new EventBusCommandProjectCreator();
                eventBusCommandProjectCreator.Build();
                new NugetCreator(eventBusCommandProjectCreator).Create();

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
