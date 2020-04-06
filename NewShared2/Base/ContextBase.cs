using System.IO;
using System.Reflection;

namespace OliveGenerator
{
    public class ContextBase
    {
        public string PublisherService, NugetServer, NugetApiKey;
        public DirectoryInfo TempPath, Output, Source;
        public FileInfo AssemblyFile;
        public Assembly AssemblyObj;
    }
}