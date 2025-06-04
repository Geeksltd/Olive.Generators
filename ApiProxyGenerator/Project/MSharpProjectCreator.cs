﻿using System;
using Olive;

namespace OliveGenerator
{
    class MSharpProjectCreator : ProjectCreatorBase
    {
        public MSharpProjectCreator() : base(Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.ControllerType.FullName + "." + "MSharp")) { }

        protected override string Framework => "net8.0";

        [EscapeGCop]
        internal override string IconUrl => "http://licensing.msharp.co.uk/images/icon.png";

        protected override string[] References => new[] { "Olive", "MSharp" };

        protected override void AddFiles()
        {
            foreach (var type in DtoTypes.All)
            {
                Console.Write("Adding M# entity definition class " + type.Name + "...");
                Folder.GetFile(type.Name + ".cs").WriteAllText(new MSharpModelProgrammer(type).Generate());
                Console.WriteLine("Done");
            }
        }
    }
}