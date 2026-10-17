using Olive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace OliveGenerator
{
    partial class Program
    {
        static int Main(string[] args)
        {
            // Nothing here may touch an Olive type: the target application's assembly versions are
            // not in play until Start() runs inside the load context created below.
            if (GeneratorHost.NeedsTargetContext)
            {
                var result = GeneratorHost.RunInTargetContext(args, typeof(Program));
                if (result.HasValue) return result.Value;
            }

            GeneratorHost.Initialize(args);

            return Guard(() => Start(args));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static int Start(string[] args)
        {
            RunSummary.Current.ItemNoun = "command";

            Begin(args, "Olive Event Bus Command Generator");
            ParametersParser.SetArgs(args);

            // Kept, so that the run's closing record can name the code the process exits with.
            var exitCode = -1;

            try
            {
                // Inside the try: reading the parameters is a step of the run like any other, and a
                // failure there has to be reported rather than escaping the entry point.
                if (!ParametersParser.Current.Start()) return exitCode;

                ParametersParser.Current.LoadParameters(null);

                Log.OpenDefaultFile(Context.Current.TempPath);
                Log.SetItems(Context.Current.CommandNames);
                Describe();

                // Once for the whole run, however many commands were asked for.
                Timings.Measure("Load the target application", Context.Current.LoadAssembly);

                // hashonly argument added to help CI / CD process to identify modified EventBusCommands faster using hash function
                // by detecting "hashonly" in aruments list, this generates a hash to help CI agent to identify modified EventBusCommands
                exitCode = args.Any(x => x.ToLower().Contains("/hashonly")) ? ShowHashes() : Generate();
            }
            catch (Exception ex)
            {
                ShowError(ex);
                exitCode = -1;
            }
            finally
            {
                // Before anything else, and however the run ended: it can hold a feed credential.
                TargetNugetConfig.RemoveFrom(Context.Current.TempPath);

                // Also after a failure: a run that produced half of what was asked for, or died
                // slowly, is exactly the one whose summary and breakdown are worth reading.
                RunSummary.Current.Report();
                Timings.Report();
                Log.Shutdown(exitCode);
            }

            return exitCode;
        }

        /// <summary>What this run was asked to do, before it starts doing it.</summary>
        static void Describe()
        {
            Log.Field("assembly", Context.Current.AssemblyFile);
            Log.Field("commands", Context.Current.CommandNames.ToString(", "));
            Log.Field("temp folder", Context.Current.TempPath);
            Log.Field("publish to", (object)Context.Current.Output ?? Context.Current.NugetServer);
            Log.Field("log file", Log.FilePath);
        }

        static int ShowHashes()
        {
            if (!Log.IsJson) Console.Out.WriteLine();

            // Per command, so one that cannot be read does not cost the others their hash. The
            // loop of single invocations this replaces behaved that way too.
            var failed = Attempt("Hash", command =>
            {
                SelectCommand(command);

                var hashValue = HashHelper.HashCommand(Context.Current.CommandType);

                // In text this is printed bare, exactly as it always was, because a CI job reads
                // the line. In json it becomes a result record like everything else.
                Log.Result(Context.Current.CommandNames.IsSingle()
                    ? "EventBusCommand Hash"
                    : $"{Context.Current.CommandName} Hash", hashValue);
            });

            return failed.Any() ? -1 : 0;
        }

        /// <summary>
        /// Runs the action against every command of the run, and returns the ones it threw for
        /// rather than abandoning the rest.
        /// </summary>
        static string[] Attempt(string what, Action<string> action)
        {
            var failed = new List<string>();

            foreach (var command in Context.Current.CommandNames)
            {
                Log.Item = command;

                // The scope is closed before the item is cleared, so its closing line is attributed
                // to the same item as everything inside it.
                using (var scope = Log.Step($"{what} {command}"))
                {
                    try { action(command); }
                    catch (Exception ex)
                    {
                        scope.Fail();
                        failed.Add(command);

                        // Against whichever name this command is already known by. Selecting it is
                        // what resolves the name it was asked for into a full type name, and sets
                        // Log.Item to it; before that, the only name there is is the one the caller
                        // passed. Reporting the asked-for name unconditionally gave a command that
                        // was named in short form two rows in the summary, one per name.
                        RunSummary.Current.For(Log.Item.Or(command)).Fail(ex.Message);
                        Log.Error(command, ex);
                    }
                }

                Log.Item = null;
            }

            return failed.ToArray();
        }

        static int Generate()
        {
            Context.Current.PrepareOutputDirectory();

            if (Context.Current.CommandNames.None())
                throw new Exception("No event bus command was named. Pass /command: with one name, " +
                    "or several separated by a semicolon.");

            var packages = new List<NugetCreatorBase>();
            var projects = new List<ProjectCreatorBase>();

            // Which command each project belongs to, so a build failure can be blamed on the one
            // that caused it rather than on the batch.
            var owner = new Dictionary<ProjectCreatorBase, string>();

            // Phase one: generate every project. Everything that reads Context runs here, while it
            // still points at the command being generated. A command that cannot be generated is
            // reported and skipped, leaving the rest of the batch intact.
            var notGenerated = Attempt("Generate", command =>
            {
                SelectCommand(command);

                var creator = new EventBusCommandProjectCreator();
                creator.Generate();

                owner[creator] = Context.Current.CommandName;
                RunSummary.Current.For(Context.Current.CommandName).Note("1 project");

                projects.Add(creator);
                packages.Add(new NugetCreator(creator).PrepareNuspec());
            });

            // Phase two: one MSBuild invocation for the lot, instead of one per project.
            var failed = Timings.Measure("Build every generated project",
                () => ProjectBuilder.BuildAll(Context.Current.TempPath, projects));

            foreach (var project in failed)
                RunSummary.Current.For(owner.TryGet(project).Or(project.Name))
                    .Fail("the generated project did not build");

            if (projects.Any(x => x.UseFallbackVersions))
                Log.Warn("The packages of this run were built against this generator's own default " +
                    "package versions, not the ones the target application uses.");

            // Phase three: packing and publishing read only run-wide settings, so the command the
            // context points at no longer matters. A package whose project failed is skipped, so
            // one bad command in a batch does not cost every other one its package.
            packages.Where(x => !x.Projects.Intersects(failed)).Do(x => x.PackAndPublish());

            if (failed.Any() || notGenerated.Any() || RunSummary.Current.AnyFailed) return -1;

            return 0;
        }

        /// <summary>
        /// Points the context at one command and rediscovers its DTOs. DtoTypes keeps its results
        /// in statics that FindAll replaces wholesale, so this must run immediately before the
        /// files for that command are generated, never earlier.
        /// </summary>
        static void SelectCommand(string command)
        {
            Context.Current.Select(command);
            DtoTypes.FindAll(Context.Current.CommandType.GetFiledTypes(), Context.Current.AssemblyObj);
        }
    }
}
