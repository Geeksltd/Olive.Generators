using Olive;
using System;

namespace OliveGenerator
{
    class MSharpProjectCreator : ProjectCreator
    {
        public MSharpProjectCreator() : base("MSharp") { }

        protected override string Framework => "netcoreapp3.1";

        [EscapeGCop]
        internal override string IconUrl => "http://licensing.msharp.co.uk/images/icon.png";

        protected override string[] References => new[] { "Olive", "MSharp" };

        protected override void AddFiles()
        {
            foreach (var item in Context.ExposedTypes)
            {
                Console.Write("Adding M# model class " + item.GetType().Name + "...");
                Folder.GetFile(item.GetType().Name + ".cs").WriteAllText(new MSharpModelProgrammer(item).Generate());
                Console.WriteLine("Done");
            }
        }
    }
}