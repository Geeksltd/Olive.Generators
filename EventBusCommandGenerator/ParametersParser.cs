using Olive;

namespace OliveGenerator
{
    internal class ParametersParser : ParametersParserBase
    {
        protected ParametersParser(ContextBase context, string[] args) : base(context, args)
        {
        }
        // public static ParametersParser SetArgs(string[] args) { return Current = new ParametersParser(Context.Current, args); }
        public static void SetArgs(string[] args) { Current = new ParametersParser(Context.Current, args); }

        public static ParametersParser Current { get; private set; }

        public override bool Start()
        {
            base.Start();

            if (Param("assembly").IsEmpty() || Param("command").IsEmpty())
            {
                Helper.ShowHelp();
                return false;
            }

            return Param("assembly").HasValue() || Param("command").HasValue();
        }

        public override void LoadParameters(string defaultName)
        {
            // Several commands may be listed, so that one invocation can generate all of them.
            // A CI loop calling this once per command pays the process start, the load context
            // handover and the assembly load again every time.
            Context.Current.CommandNames = ParamList("command");
            base.LoadParameters("eventbuscommand-generator");
        }
    }
}