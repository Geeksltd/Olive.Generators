using System;
using System.Collections.Generic;
using System.IO;
using Olive;

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
    }
}