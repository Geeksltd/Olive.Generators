using Olive;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace OliveGenerator
{
    class Context : ContextBase
    {
        public static Context Current { get; } = new Context();

        public string ControllerName;//, PublisherService, NugetServer, NugetApiKey;
        //public static FileInfo AssemblyFile;
        //public static DirectoryInfo TempPath, Output, Source;
        //public static Assembly AssemblyObj;
        public Type ControllerType;
        public MethodGenerator[] ActionMethods;

        internal void PrepareOutputDirectory()
        {
            if (!TempPath.Exists)
                throw new Exception("Output directory not found: " + TempPath.FullName);

            try
            {
                if (TempPath.Exists)
                    TempPath.DeleteAsync(recursive: true, harshly: true).WaitAndThrow();
                TempPath.Create();
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to delete the previous output directory " +
                    TempPath.FullName + Environment.NewLine + ex.Message);
            }
        }

        internal void LoadAssembly()
        {
            var pluginLocation = AssemblyFile.ExistsOrThrow().FullName;

            AssemblyObj = Assembly.LoadFrom(AssemblyFile.ExistsOrThrow().FullName);

            ControllerType = AssemblyObj.GetType(ControllerName);

            if (ControllerType == null) // Maybe no namespace?
            {
                ControllerType = AssemblyObj.GetTypes().FirstOrDefault(x => x.Name == ControllerName)
                  ?? throw new Exception(ControllerName + " was not found.");
            }

            ControllerName = ControllerType.FullName; // Ensure it has full namespace

            ActionMethods = ControllerType
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Select(x => new MethodGenerator(x, ControllerType))
                .ToArray();

            if (ActionMethods.Length == 0) throw new Exception("This controller has no action method.");
        }
    }
}