using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Olive;

namespace OliveGenerator
{
    partial class Program
    {
        static bool Initialize(string[] args)
        {
            Console.WriteLine("Current directory: " + Environment.CurrentDirectory);

            if (args.Contains("/debug"))
            {
                Console.Write("Waiting for debugger to attach...");
                while (!Debugger.IsAttached) Thread.Sleep(100);
                Console.WriteLine("Attached.");
            }

            AppDomain.CurrentDomain.ResolveAssemblies();

            if (!ParametersParser.Start(args))
            {
                Helper.ShowHelp();
                return false;
            }

            return true;
        }

        public static void ShowError(Exception error)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("ERROR!");
            Console.WriteLine(error.Message);
            Console.ResetColor();
            Console.WriteLine(error.GetUsefulStack());
            Console.WriteLine("Press any key to end, or rerun the command with /debug.");
            Console.ReadKey();
        }
    }
}