using Olive;
using System;
using System.IO;
using System.Linq;

namespace OliveGenerator
{
    class ParametersParser
    {
        static string[] Args;

        internal static bool Start(string[] args)
        {
            Args = args;

            if (Args.Length == 0) return false;

            switch (Context.Command = args.First().ToPascalCaseId().To<Command>())
            {
                case Command.Extract:
                    if (Args.Length < 3 || Args[1] != "--out") return false;
                    Context.FileName = Args[2];
                    return true;
                case Command.Restore:
                    if (Args.Length < 2) return false;
                    Context.FileName = Args[1];
                    Context.Parallel = args.Length > 2 && Args[2] == "-parallel";
                    return true;
                default: throw new NotSupportedException();
            }
        }

        public static void LoadParameters()
        {
            Context.BasePath = Environment.CurrentDirectory.AsDirectory();
            Console.WriteLine("BasePath: " + Context.BasePath.FullName);

            Context.ProjectFiles = Context.BasePath.GetFiles("*.csproj", SearchOption.AllDirectories).ToList();
        }
    }
}