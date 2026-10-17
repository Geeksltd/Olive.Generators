using System;

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
            Show("   accelerate-package ", ConsoleColor.White);
            ShowLine(" extract --out packages.json  | restore packages.json", ConsoleColor.Yellow);
            Console.WriteLine();

            ShowLine("PARAMETERS: ", ConsoleColor.Red);
            Console.WriteLine("--------------------");

            Param("extract", @"It will find all .csproj files in the solution, parse them, and extract a unique list of all nuget packages used across them.");
            Console.WriteLine();

            Param("--out", @"It indicate we will have result in a file");
            Console.WriteLine();

            Param("packages.json", @"it will use as Input with ""Restor"" command and Output as ""Extract"" command");
            Console.WriteLine();

            Console.WriteLine();

            Param("/verbosity:...", @"How much the run says as it goes: quiet, minimal, normal (the default), detailed or diagnostic. 'detailed' includes the output of the restore as it arrives.");
            Console.WriteLine();

            Param("/logformat:...", @"How the console is written: text (the default, for a person) or json (one self-describing record per line, for another program to read).");
            Console.WriteLine();

            Param("/log:...", @"Writes a full-detail log to this file, whatever the console verbosity is.");
            Console.WriteLine();

            Param("/logjson:...", @"Also writes the log to this file as newline-delimited json, one object per line.");
            Console.WriteLine();

            Param("/heartbeat:...", @"Seconds of silence before the run says which step it is still on. Defaults to 15. Pass 0 to turn it off.");
            Console.WriteLine();

            Param("/timing", @"Prints a breakdown of where the run spent its time, to standard error.");
            Console.WriteLine();
            return -1;
        }
    }
}