using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Loader;

namespace OliveGenerator
{
    /// <summary>
    /// Loads the assemblies of the microservice being scanned, preferring the copy that ships with
    /// that microservice whenever it is newer than the one this generator was compiled against.
    /// <para>
    /// The generator carries its own Olive, Olive.Entities, Olive.Entities.Data.Replication, ...
    /// When the target microservice references a newer version of any of them, the default load
    /// context cannot serve it (its own copy is older than the reference), so the old code fell
    /// back to Assembly.LoadFile, which loads into a separate context. The shared types then had
    /// two identities and every cast to them (e.g. SourceEndpoint) failed with an
    /// InvalidCastException, even though the target's Olive version has no bearing on the output.
    /// </para>
    /// </summary>
    class TargetAppLoadContext : AssemblyLoadContext
    {
        static readonly string GeneratorFolder = AppContext.BaseDirectory;

        readonly string TargetFolder;
        readonly ConcurrentDictionary<string, bool> FrameworkAssemblies = new ConcurrentDictionary<string, bool>();

        public TargetAppLoadContext(string targetFolder) : base(nameof(TargetAppLoadContext))
            => TargetFolder = targetFolder;

        protected override Assembly Load(AssemblyName request)
        {
            var theirs = FindDll(TargetFolder, request.Name);

            // Not shipped with the target app: let the default context (our own dependencies and
            // the shared framework) serve it.
            if (theirs == null) return null;

            var ours = FindDll(GeneratorFolder, request.Name);

            if (ours == null)
            {
                // Owned by the shared framework. Loading our own copy of it would give the target's
                // types a second identity, which is the very problem this context exists to avoid.
                if (IsProvidedByFramework(request.Name)) return null;
            }
            else if (GetVersion(theirs) <= GetVersion(ours)) return null;

            try
            {
                Log.Diagnostic($"Loading {request.Name} from the target application: {theirs}");
                return LoadFromAssemblyPath(theirs);
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to load {theirs}: {ex.Message}");
                return null;
            }
        }

        internal static string FindDll(string folder, string assemblyName)
        {
            if (string.IsNullOrWhiteSpace(folder) || string.IsNullOrWhiteSpace(assemblyName)) return null;

            var file = Path.Combine(folder, assemblyName + ".dll");
            return File.Exists(file) ? file : null;
        }

        static Version GetVersion(string dll)
        {
            try { return AssemblyName.GetAssemblyName(dll).Version ?? new Version(); }
            catch { return new Version(); }
        }

        bool IsProvidedByFramework(string assemblyName)
        {
            return FrameworkAssemblies.GetOrAdd(assemblyName, name =>
            {
                try { return Default.LoadFromAssemblyName(new AssemblyName(name)) != null; }
                catch { return false; }
            });
        }
    }

    /// <summary>
    /// Bootstraps the generator inside a <see cref="TargetAppLoadContext"/> so that the generator
    /// and the microservice it scans agree on a single set of Olive types.
    /// </summary>
    static class GeneratorHost
    {
        /// <summary>The folder holding the target microservice's assemblies.</summary>
        public static string TargetFolder { get; private set; } = Environment.CurrentDirectory;

        /// <summary>
        /// True while running in the default load context, i.e. before the generator has been
        /// handed over to a context that can see the target's own assembly versions.
        /// </summary>
        public static bool NeedsTargetContext
            => AssemblyLoadContext.GetLoadContext(typeof(GeneratorHost).Assembly) == AssemblyLoadContext.Default;

        /// <summary>
        /// Reloads this generator into a load context bound to the target's folder and calls its
        /// entry point again. Returns null if that could not be arranged, in which case the caller
        /// should simply carry on in the default context.
        /// <para>
        /// This must be called before any Olive type is touched, hence the entry method it invokes
        /// has to stay free of them too.
        /// </para>
        /// </summary>
        public static int? RunInTargetContext(string[] args, Type entryPoint)
        {
            try
            {
                var context = new TargetAppLoadContext(FindTargetFolder(args));
                var self = context.LoadFromAssemblyPath(entryPoint.Assembly.Location);

                var main = self.GetType(entryPoint.FullName)
                    ?.GetMethod("Main", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

                if (main == null) return null;

                return (int)main.Invoke(null, new object[] { args });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                // Rethrown with its original stack, which a bare "throw ex.InnerException" replaces
                // with this line. The stack of a failure inside the load context is the whole of
                // what makes it diagnosable, since the reflection frames around it say nothing.
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();

                throw; // Unreachable: the line above always throws.
            }
            catch (Exception ex)
            {
                Log.Warn(Log.Describe("Could not run inside a load context bound to the target application, " +
                    "so its own assembly versions are not in play", ex));
                return null;
            }
        }

        /// <summary>Records the target folder, so assembly probing does not rely on the current
        /// directory, which the project creators change while generating.</summary>
        public static void Initialize(string[] args) => TargetFolder = FindTargetFolder(args);

        /// <summary>Loads an assembly of the target microservice into the generator's own context.</summary>
        public static Assembly LoadTargetAssembly(string path)
        {
            var context = AssemblyLoadContext.GetLoadContext(typeof(GeneratorHost).Assembly)
                ?? AssemblyLoadContext.Default;

            return context.LoadFromAssemblyPath(path);
        }

        static string FindTargetFolder(string[] args)
        {
            try
            {
                var source = Param(args, "source") ?? Environment.CurrentDirectory;
                var assembly = Param(args, "assembly") ?? "Website.dll";

                var folder = Path.GetDirectoryName(Path.GetFullPath(Path.Combine(source, assembly)));
                if (Directory.Exists(folder)) return folder;
            }
            catch (Exception ex)
            {
                Log.Warn("Failed to determine the target application's folder: " + ex.Message);
            }

            return Environment.CurrentDirectory;
        }

        static string Param(string[] args, string key)
        {
            var prefix = "/" + key + ":";

            var value = args?.FirstOrDefault(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                ?.Substring(prefix.Length);

            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
