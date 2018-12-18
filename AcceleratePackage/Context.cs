using Olive;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;

namespace OliveGenerator
{
    public enum Command { Extract, Restore }

    class Context
    {
        public static string FileName;
        public static DirectoryInfo BasePath;
        public static List<FileInfo> ProjectFiles = new List<FileInfo>();
        public static List<NugetPackage> Packages = new List<NugetPackage>();
        public static bool Parallel;
        public static Command Command = Command.Restore;

        internal static string Run(string command)
        {
            var cmd = new Process();
            cmd.StartInfo.FileName = "cmd.exe";
            cmd.StartInfo.RedirectStandardInput = true;
            cmd.StartInfo.RedirectStandardOutput = true;
            cmd.StartInfo.CreateNoWindow = false;
            cmd.StartInfo.UseShellExecute = false;
            cmd.Start();
            cmd.StandardInput.WriteLine(command);
            cmd.StandardInput.Flush();
            cmd.StandardInput.Close();
            var result = cmd.StandardOutput.ReadToEnd().ToStringOrEmpty().Trim();

            if (result.StartsWith("Could not ")) throw new Exception(result);

            if (result.Contains("Build FAILED"))
            {
                Console.WriteLine("Compile " + command + " manually...");
                Console.ReadLine();
                // throw new Exception(result.TrimBefore("Build FAILED"));
            }

            cmd.WaitForExit();

            return result;
        }
    }
}