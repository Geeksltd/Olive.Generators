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

            TargetPackageVersions.Load(TheContext.AssemblyFile);
            TargetFramework.Load(TheContext.AssemblyFile);
            SolutionBuilder.MaxCpu = ReadMaxCpu();

            TheContext.TempPath = Path.GetTempPath().AsDirectory()
                .GetOrCreateSubDirectory(defaultName ?? "eventbuscommand-generator").CreateSubdirectory(Guid.NewGuid().ToString());
        }

        /// <summary>
        /// How many of the generated projects MSBuild may build at once. Left out, it uses one per
        /// processor as it always has. It is worth setting on a build agent with more cores than it
        /// has memory to give each of them, where a batch of endpoints is a few hundred projects.
        /// </summary>
        int? ReadMaxCpu()
        {
            var value = Param("maxCpu");
            if (value.IsEmpty()) return null;

            if (!int.TryParse(value, out var result) || result < 1)
                throw new Exception($"/maxCpu: expects a whole number of 1 or more, but was given \"{value}\".");

            return result;
        }

        /// <summary>Splits a parameter that may list several values, so one run covers all of them.</summary>
        public string[] ParamList(string key)
            => Param(key).OrEmpty().Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(x => x.Trim()).Where(x => x.HasValue()).Distinct().ToArray();

        /// <summary>
        /// One switch's value. Matched without regard to case, as the logging switches and the load
        /// context's own reading of /assembly: and /source: already are: "/Assembly:" used to decide
        /// which folder the target was probed from while leaving this none the wiser, so the run
        /// went looking for Website.dll instead.
        /// </summary>
        public string Param(string key)
        {
            var decorateKey = "/" + key + ":";

            return Args.FirstOrDefault(x => x != null &&
                    x.StartsWith(decorateKey, StringComparison.OrdinalIgnoreCase))
                ?.Substring(decorateKey.Length).OrNullIfEmpty();
        }
    }
}