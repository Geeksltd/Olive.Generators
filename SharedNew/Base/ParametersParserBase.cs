using System;
using System.IO;
using System.Linq;
using Olive;

namespace OliveGenerator
{
    public abstract class ParametersParserBase
    {
        protected string[] Args { get; private set; }

        protected ContextBase TheContext;

        protected ParametersParserBase(ContextBase context, string[] args)
        {
            TheContext = context;
            Args = args;
        }

        public virtual bool Start()
        {
            TheContext.Source = (Param("source") ?? Environment.CurrentDirectory).AsDirectory();
            return true;
        }

        public virtual void LoadParameters(string defaultName)
        {
            TheContext.Output = Param("out")?.AsDirectory();

            if (TheContext.Output?.Exists == false)
                throw new Exception("The specified output folder does not exist.");

            TheContext.NugetServer = Param("push");
            TheContext.PublisherService = Param("serviceName");
            TheContext.NugetApiKey = Param("apiKey");

            TheContext.AssemblyFile = TheContext.Source
                .GetFile(Param("assembly").Or("Website.dll"))
                .ExistsOrThrow();

            TheContext.TempPath = Path.GetTempPath().AsDirectory()
                .GetOrCreateSubDirectory(defaultName ?? "eventbuscommand-generator").CreateSubdirectory(Guid.NewGuid().ToString());
        }

        public string Param(string key)
        {
            var decorateKey = "/" + key + ":";
            return Args.FirstOrDefault(x => x.StartsWith(decorateKey))?.TrimStart(decorateKey).OrNullIfEmpty();
        }
    }
}