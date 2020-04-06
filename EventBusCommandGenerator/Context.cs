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
            var pluginLocation = AssemblyFile.ExistsOrThrow().FullName;

            AssemblyObj = Assembly.LoadFrom(AssemblyFile.ExistsOrThrow().FullName);

            CommandType = AssemblyObj.GetType(CommandName) ??
                AssemblyObj.GetTypes().FirstOrDefault(x => x.Name == CommandName && x.BaseType == typeof(EventBusCommandMessage)) ??
                throw new Exception($"No type in the assembly {AssemblyFile.FullName} is named: {CommandName}.");

            CommandName = CommandType.FullName; // Ensure it has full namespace 
        }
    }
}