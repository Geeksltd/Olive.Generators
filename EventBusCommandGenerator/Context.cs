using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Olive;

namespace OliveGenerator
{
    class Context : ContextBase
    {
        public static Context Current { get; } = new Context();

        public string CommandName;

        /// <summary>Every command this run was asked to generate.</summary>
        public string[] CommandNames = Array.Empty<string>();

        public Type CommandType;
        public List<FieldInfo> CommandFields = new List<FieldInfo>();

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

        /// <summary>
        /// Loads the target application once for the whole run. Resolving the individual commands
        /// is separate, so a run covering several of them pays this only once.
        /// </summary>
        internal void LoadAssembly()
        {
            AppDomain.CurrentDomain.ResolveAssemblies();
            AssemblyObj = GeneratorHost.LoadTargetAssembly(AssemblyFile.ExistsOrThrow().FullName);

            Log.Detail("Loaded " + AssemblyObj.FullName);
        }

        /// <summary>Points this context at one of the commands the run was asked to generate.</summary>
        internal void Select(string command)
        {
            CommandType = AssemblyObj.GetType(command) ??
                AssemblyObj.GetTypes().FirstOrDefault(x => x.Name == command) ??
                throw new Exception($"No type in the assembly {AssemblyFile.FullName} is named: {command}.");

            CommandName = CommandType.FullName; // Ensure it has full namespace
            Log.Item = CommandName;
        }
    }
}