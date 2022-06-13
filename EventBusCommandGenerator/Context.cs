using Olive;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;

namespace OliveGenerator
{
    class Context : ContextBase
    {
        public static Context Current { get; } = new Context();

        public string CommandName;
        public Type CommandType;
        public static Assembly AssemblyObject;
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
            }
            catch (Exception ex)
            {
                throw new Exception("Failed to delete the previous output directory " +
                    TempPath.FullName + Environment.NewLine + ex.Message);
            }
        }

        internal void LoadAssembly()
        {
            AppDomain.CurrentDomain.ResolveAssemblies();
            AssemblyObject = Assembly.LoadFrom(AssemblyFile.ExistsOrThrow().FullName);

            CommandType = AssemblyObject.GetType(CommandName) ??
                AssemblyObject.GetTypes().FirstOrDefault(x => x.Name == CommandName) ??
                throw new Exception($"No type in the assembly {AssemblyFile.FullName} is named: {CommandName}.");

            CommandName = CommandType.FullName; // Ensure it has full namespace 
        }
    }
}