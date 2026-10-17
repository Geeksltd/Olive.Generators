using System;
using System.Linq;
using System.Reflection;
using Olive;

namespace OliveGenerator
{
    class Context : ContextBase
    {
        public static Context Current { get; } = new Context();

        public string ControllerName;//, PublisherService, NugetServer, NugetApiKey;
        // public static FileInfo AssemblyFile;
        // public static DirectoryInfo TempPath, Output, Source;
        // public static Assembly AssemblyObj;
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

                // Written before anything is built, so the temporary projects can see the private
                // feeds the target microservice restores from.
                TargetNugetConfig.WriteInto(TempPath, AssemblyFile);
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

            AssemblyObj = GeneratorHost.LoadTargetAssembly(AssemblyFile.ExistsOrThrow().FullName);

            ControllerType = AssemblyObj.GetType(ControllerName);

            if (ControllerType == null) // Maybe no namespace?
            {
                ControllerType = AssemblyObj.GetTypes().FirstOrDefault(x => x.Name == ControllerName)
                  ?? throw new Exception(ControllerName + " was not found.");
            }

            ControllerName = ControllerType.FullName; // Ensure it has full namespace
            Log.Item = ControllerName;
            Log.Detail("Loaded " + AssemblyObj.FullName);

            ActionMethods = ControllerType
                .GetMethods(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(IsApiAction)
                .Select(x => new MethodGenerator(x, ControllerType))
                .ToArray();

            Log.Detail($"{ControllerName} exposes {ActionMethods.Length} api action(s): " +
                ActionMethods.Select(x => x.Method.Name).ToString(", "));

            if (ActionMethods.None())
                throw new Exception($"{ControllerName} has no action method. An api method must declare " +
                    "[Route], [HttpGet], [HttpPost], [HttpPut], [HttpPatch] or [HttpDelete].");
        }

        bool IsApiAction(MethodInfo method)
        {
            if (MethodGenerator.IsApiAction(method)) return true;

            Log.Detail($"Skipping {ControllerName}.{method.Name}(): " +
                "it declares no route or http verb attribute, so it is not an api action.");

            return false;
        }
    }
}