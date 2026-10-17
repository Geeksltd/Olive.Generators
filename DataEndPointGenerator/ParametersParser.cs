using System;
using System.IO;
using Olive;

namespace OliveGenerator
{
    class ParametersParser : ParametersParserBase
    {
        ParametersParser(ContextBase context, string[] args) : base(context, args) { }

        public static ParametersParser Current { get; private set; }

        public static void SetArgs(string[] args) => Current = new ParametersParser(Context.Current, args);

        public override bool Start()
        {
            base.Start();

            Context.Current.MSharp = Context.Current.Source.SolutionDirectory().GetSubDirectory("M#");

            if (Param("assembly").IsEmpty() || Param("dataEndpoint").IsEmpty())
            {
                Helper.ShowHelp();
                return false;
            }

            return true;
        }

        public override void LoadParameters(string defaultName)
        {
            // Several endpoints may be listed, so that one invocation can generate all of them.
            // A CI loop calling this once per endpoint pays the process start, the load context
            // handover and the assembly load again every time.
            Context.Current.EndpointNames = ParamList("dataEndpoint");

            base.LoadParameters(defaultName ?? "dataendpoint-generator");
        }
    }
}
