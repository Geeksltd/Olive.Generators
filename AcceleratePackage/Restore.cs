using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Olive;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace OliveGenerator
{
    internal class Restore
    {

        internal static void Start()
        {
            Console.WriteLine();
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("Extract Mode");
            Console.ResetColor();

            Environment.CurrentDirectory = Context.FileName.AsFile().Directory.FullName;

            Context.Run("nuget restore --PackagesDirectory \"%userprofile%\\.nuget\\packages\"");
        }
    }
}