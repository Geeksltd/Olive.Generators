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
            ShowLine("Usage pattern:\n", ConsoleColor.Red);
            Show("   dotnet ", ConsoleColor.White);
            Show(Assembly.GetExecutingAssembly().GetName().Name + ".dll ", ConsoleColor.Green);
            ShowLine("/assembly:... /command:... [/out:...] [/push:... /apiKey:...]", ConsoleColor.Yellow);
            Console.WriteLine();

            ShowLine("PARAMETERS: ", ConsoleColor.Red);
            Console.WriteLine("--------------------");

            Param("assembly", @"File name of the assembly containing the command. Usually it's the path to the 'Website.dll'.");
            Console.WriteLine();

            Param("command", @"The name of the event bus command class. If there are multiple classes with the same name, provide the full name with namespace.");
            Console.WriteLine();

            Param("out", @"The full path to a directory to publish the generated nuget packages. e.g. C:\Projects\my-solution\PrivatePackages");
            Console.WriteLine();

            Param("push", @"The url of a [private] nuget server to publish the generated package to. For example: http://nuget.my-solution.my-server.com/nuget");
            Console.WriteLine();

            Param("apiKey", @"The Api Key expected by the [private] nuget server, for security.");
            Console.WriteLine();
            Console.ReadKey(intercept: true);
            return -1;
        }

        public static void ShowError(Exception ex, ConsoleColor color = ConsoleColor.Red)
        {
            Console.ForegroundColor = color;
            Console.Write(" " + ex.Message);
            Console.ResetColor();
        }
    }
}