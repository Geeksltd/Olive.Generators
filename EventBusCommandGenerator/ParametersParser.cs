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
            Context.Source = Environment.CurrentDirectory.AsDirectory();

            if (Param("assembly").IsEmpty() || Param("command").IsEmpty())
            {
                Helper.ShowHelp();
                return false;
            }

            return Param("assembly").HasValue() || Param("command").HasValue();
        }

        public static void LoadParameters()
        {
            Context.CommandName = Param("command");
            Context.Output = Param("out")?.AsDirectory();

            if (Context.Output?.Exists == false)
                throw new Exception("The specified output folder does not exist.");

            Context.NugetServer = Param("push");
            Context.NugetApiKey = Param("apiKey");

            Context.AssemblyFile = Context.Source
                .GetFile(Param("assembly").Or("Website.dll"))
                .ExistsOrThrow();

            Context.TempPath = Path.GetTempPath().AsDirectory()
                .GetOrCreateSubDirectory("eventbuscommand-generator").CreateSubdirectory(Guid.NewGuid().ToString());
        }

        static string Param(string key)
        {
            var decorateKey = "/" + key + ":";
            return Args.FirstOrDefault(x => x.StartsWith(decorateKey))?.TrimStart(decorateKey).OrNullIfEmpty();
        }
    }
}