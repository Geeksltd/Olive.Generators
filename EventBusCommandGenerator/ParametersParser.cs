using Olive;
using System;
using System.IO;
using System.Linq;

namespace OliveGenerator
{
    internal class ParametersParser : ParametersParserBase
    {
        //public static ParametersParser SetArgs(string[] args) { return Current = new ParametersParser(Context.Current, args); }
        public static void SetArgs(string[] args) { Current = new ParametersParser(Context.Current, args); }

        public static ParametersParser Current { get; private set; }

        protected ParametersParser(ContextBase context, string[] args) : base(context, args)
        {
        }

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
            Context.Current.CommandName = Param("command");
            base.LoadParameters("eventbuscommand-generator");
        }
    }
}