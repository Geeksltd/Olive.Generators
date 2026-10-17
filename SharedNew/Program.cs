using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using Olive;

namespace OliveGenerator
{
    partial class Program
    {
        /// <summary>
        /// Runs a generator's entry point, and makes sure that however it ends there is a record
        /// saying so.
        /// <para>
        /// Everything a generator does inside its own try already reports itself. What this covers
        /// is the part before it: starting the log, and reading the parameters, which can fail on
        /// its own — the data endpoint generator resolves the target's solution folder there, and
        /// throws when the assembly it was given is not inside one. Such a failure used to escape
        /// Main, so the process died with a stack on standard error, no error record, no run-end
        /// and no summary, leaving a caller reading the record stream with nothing but an exit code.
        /// </para>
        /// </summary>
        /// <remarks>
        /// Not inlined, for the same reason each generator's Start is not: this is called from Main,
        /// which must reach the load context decision without touching an Olive type, and the error
        /// reporting below uses Olive's own extensions. A method holding a try/catch is not inlined
        /// in any case; saying so keeps that from being an accident.
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Guard(Func<int> start)
        {
            try { return start(); }
            catch (Exception ex)
            {
                ShowError(ex);

                // Shutdown is idempotent, so this settles the run whether or not the generator's
                // own finally already did.
                Log.Shutdown(-1);

                return -1;
            }
        }

        /// <summary>
        /// Everything every generator does before it reads its own parameters: start the log, say
        /// what this run is and what it will use, and hook assembly resolution.
        /// </summary>
        static void Begin(string[] args, string tool)
        {
            Log.Initialize(args);
            Timings.Initialize(args);

            Announce(args, tool);

            if (args.Contains("/debug")) WaitForDebugger();

            AppDomain.CurrentDomain.ResolveAssemblies();
        }

        /// <summary>
        /// What this run is, and what it is about to use. All of it is diagnostic apart from the
        /// first line, and all of it goes into the log file whatever the console verbosity.
        /// </summary>
        static void Announce(string[] args, string tool)
        {
            var assembly = Assembly.GetExecutingAssembly();

            Log.Run(tool, assembly.GetName().Version?.ToString(3), args);

            Log.Field("working folder", Environment.CurrentDirectory, LogLevel.Detailed);
            Log.Field("target folder", GeneratorHost.TargetFolder, LogLevel.Detailed);
            Log.Field("runtime", RuntimeInformation.FrameworkDescription, LogLevel.Detailed);
            Log.Field("os", RuntimeInformation.OSDescription.ToLines().FirstOrDefault(), LogLevel.Detailed);
            Log.Field("machine", Environment.MachineName, LogLevel.Diagnostic);

            ProcessRunner.Describe();
        }

        static void WaitForDebugger()
        {
            Log.Info("Waiting for a debugger to attach to process " + Environment.ProcessId + "...");
            while (!Debugger.IsAttached) Thread.Sleep(100);
            Log.Info("Debugger attached.");
        }

        /// <summary>
        /// Reports a failure with everything that might explain it: the inner exceptions, the
        /// loader exceptions of a failed type load, and the stack.
        /// </summary>
        public static void ShowError(Exception error)
        {
            Log.Error(error);
            Log.Info("Rerun with /verbosity:detailed to see what each step did, or /debug to attach a debugger.");

            if (Log.FilePath.HasValue()) Log.Info("Full log: " + Log.FilePath);
        }
    }
}
