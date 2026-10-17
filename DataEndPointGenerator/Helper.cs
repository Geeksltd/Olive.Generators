using System;
using System.Reflection;

namespace OliveGenerator
{
    class Helper
    {
        static void ShowLine(string text, ConsoleColor color) => Show(text + Environment.NewLine, color);

        static void Show(string text, ConsoleColor color)
        {
            Console.ForegroundColor = color;
            Console.Write(" " + text);
            Console.ResetColor();
        }

        static void Param(string name, string description)
        {
            Show(name + ": ", ConsoleColor.Yellow);
            Console.WriteLine(description);
        }

        public static int ShowHelp()
        {
            // The usage screen is prose. When the console is a stream of records, saying so as a
            // record is the only thing that will not corrupt whatever is reading it.
            if (Log.IsJson)
            {
                Log.Error("The command line is missing a required parameter. " +
                    "Rerun without /logformat:json to see the usage screen.");

                return -1;
            }

            ShowLine("Usage pattern:\n", ConsoleColor.Red);
            Show("   dotnet ", ConsoleColor.White);
            Show(Assembly.GetExecutingAssembly().GetName().Name + ".dll ", ConsoleColor.Green);
            ShowLine("/assembly:... /dataEndpoint:...[;...] [/out:...] [/push:... /apiKey:...] [/verbosity:...] [/log:...] [/timing]", ConsoleColor.Yellow);
            Console.WriteLine();

            ShowLine("PARAMETERS: ", ConsoleColor.Red);
            Console.WriteLine("--------------------");

            Param("assembly", @"File name of the assembly containing the data endpoint. Usually it's the path to the 'Website.dll'.");
            Console.WriteLine();

            Param("dataEndpoint", @"The name of the data endpoint class. If there are multiple classes with the same name, provide the full name with namespace. Several may be separated by a semicolon, and generating them together is much faster than one run each.");
            Console.WriteLine();

            Param("source", @"The folder holding the assembly. Defaults to the current directory.");
            Console.WriteLine();

            Param("out", @"The full path to a directory to publish the generated nuget packages. e.g. C:\Projects\my-solution\PrivatePackages");
            Console.WriteLine();

            Param("push", @"The url of a [private] nuget server to publish the generated package to. For example: http://nuget.my-solution.my-server.com/nuget");
            Console.WriteLine();

            Param("apiKey", @"The Api Key expected by the [private] nuget server, for security.");
            Console.WriteLine();

            Param("timing", @"Prints a breakdown of where the run spent its time, to standard error.");
            Console.WriteLine();

            Param("verbosity", @"How much the run says as it goes: quiet, minimal, normal (the default), detailed or diagnostic. 'detailed' includes the output of the builds and packs as they run, which is what to reach for when a run fails or stalls.");
            Console.WriteLine();

            Param("logformat", @"How the console is written: text (the default, for a person) or json (one self-describing record per line, for another program to read).");
            Console.WriteLine();

            Param("log", @"Writes a full-detail log to this file, whatever the console verbosity is. One is written beside the run's temp folder by default, and the path is printed at the start of the run.");
            Console.WriteLine();

            Param("logjson", @"Also writes the log to this file as newline-delimited json, one object per line, for a pipeline to read.");
            Console.WriteLine();

            Param("heartbeat", @"Seconds of silence before the run says which step it is still on. Defaults to 15. Pass 0 to turn it off.");
            Console.WriteLine();

            Param("nocolor", @"Never colours the output.");
            Console.WriteLine();
            return -1;
        }
    }
}