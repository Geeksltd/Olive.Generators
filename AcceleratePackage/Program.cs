using System;

namespace OliveGenerator
{
    class Program
    {
        static int Main(string[] args)
        {
            if (!ParametersParser.Start(args)) return -1;
            try
            {
                ParametersParser.LoadParameters();

                Console.WriteLine("Command: " + Context.Command + " nugets ");
                Console.WriteLine("File: file: " + Context.FileName);
                Console.WriteLine();

                if (Context.Command == Command.Extract) Extract.Start();
                else Restore.Start();

                Console.WriteLine("Cashe Nuget was done");

                return 0;
            }
            catch (Exception ex)
            {
                Helper.ShowError(ex);
                return -1;
            }
        }
    }
}