using Olive;
using Olive.Entities.Replication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OliveGenerator
{
    class Context : ContextBase
    {
        public static Context Current { get; } = new Context();

        public string EndpointName;

        /// <summary>Every endpoint this run was asked to generate.</summary>
        public string[] EndpointNames = Array.Empty<string>();

        public DirectoryInfo MSharp;

        public Type EndpointType;
        public List<ExposedType> ExposedTypes = new List<ExposedType>();

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
        /// Loads the target application once for the whole run. Resolving the individual endpoints
        /// is separate, so a run covering several of them pays this only once.
        /// </summary>
        internal void LoadAssembly()
        {
            AppDomain.CurrentDomain.ResolveAssemblies();
            AssemblyObj = GeneratorHost.LoadTargetAssembly(AssemblyFile.ExistsOrThrow().FullName);

            Log.Detail("Loaded " + AssemblyObj.FullName);
        }

        /// <summary>Points this context at one of the endpoints the run was asked to generate.</summary>
        internal void Select(string endpoint)
        {
            EndpointType = AssemblyObj.GetType(endpoint) ??
                AssemblyObj.GetTypes().FirstOrDefault(x => x.Name == endpoint) ??
                throw new Exception($"No type in the assembly {AssemblyFile.FullName} is named: {endpoint}.");

            EndpointName = EndpointType.FullName; // Ensure it has full namespace 
            Log.Item = EndpointName;
        }

        internal void FindExposedTypes()
        {
            //================================================================================
            // If an InvalidCastException occurs here,
            // check whether "Olive.Entities.Data.Replication" needs to be updated.
            // Types must be loaded from the same assembly version both here and in the target microservice assembly.
            //================================================================================

            var existingTypes = EndpointType.CreateInstance<SourceEndpoint>().GetTypes();
            ExposedTypes = existingTypes.Select(x => x.CreateInstance<ExposedType>()).ToList();

            if (ExposedTypes.None()) throw new Exception("This endpoint has no exposed data types.");

            foreach (var type in ExposedTypes)
                type.Define();

            foreach (var type in ExposedTypes)
                Log.Detail($"exposes {type.DomainType?.FullName} with {type.Fields.OrEmpty().Count()} field(s)");
        }

        internal FileInfo[] FindReferenceDataFiles()
        {
            const string key = "ReferenceData";

            var current = AssemblyFile.Directory;
            while (current != null && !current.GetSubDirectory(key).Exists())
                current = current.Parent;

            if (current == null)
                return Array.Empty<FileInfo>();

            var endpointName = EndpointName.Split('.', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
            current = current.GetSubDirectory(key).GetSubDirectory(endpointName);

            if (current == null || !current.Exists())
                return Array.Empty<FileInfo>();

            return current.GetFiles("*.json", System.IO.SearchOption.AllDirectories);
        }
    }
}
