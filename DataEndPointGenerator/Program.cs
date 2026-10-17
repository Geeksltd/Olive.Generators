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
            // The data endpoint packages have always been versioned this way round.
            ProjectCreatorBase.VersionFormat = "yyMM.ddHH.mmss";
            RunSummary.Current.ItemNoun = "endpoint";

            Begin(args, "Olive Data Endpoint Generator");
            ParametersParser.SetArgs(args);

            // Kept, so that the run's closing record can name the code the process exits with.
            var exitCode = -1;

            try
            {
                // Inside the try, because reading the parameters is a step of the run that can fail
                // like any other: this generator resolves the target's solution folder here, and
                // throws when the assembly it was given is not inside one. Outside, that failure
                // ended the process with no error record, no summary and no run-end.
                if (!ParametersParser.Current.Start()) return exitCode;

                ParametersParser.Current.LoadParameters("dataendpoint-generator");

                Log.OpenDefaultFile(Context.Current.TempPath);
                Log.SetItems(Context.Current.EndpointNames);
                Describe();

                // Once for the whole run, however many endpoints were asked for.
                Timings.Measure("Load the target application", Context.Current.LoadAssembly);

                /// hashonly argument added to Olive.Endpoint Generator to help CI/CD process to identify modified Endpoints faster using hash function
                /// by detecting "hashonly" in aruments list, Olive.Endpoint.Generator generates a hash to help CI agent to identify modified Endpoints
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
            Log.Field("endpoints", Context.Current.EndpointNames.ToString(", "));
            Log.Field("m# folder", Context.Current.MSharp);
            Log.Field("temp folder", Context.Current.TempPath);
            Log.Field("publish to", (object)Context.Current.Output ?? Context.Current.NugetServer);
            Log.Field("log file", Log.FilePath);
        }

        static int ShowHashes()
        {
            if (!Log.IsJson) Console.Out.WriteLine();

            // Per endpoint, so one that cannot be read does not cost the others their hash. The
            // loop of single invocations this replaces behaved that way too.
            var failed = Attempt("Hash", endpoint =>
            {
                Context.Current.Select(endpoint);
                Context.Current.FindExposedTypes();

                var hashValue = HashHelper.HashExposedType(Context.Current.ExposedTypes,
                    Context.Current.FindReferenceDataFiles());

                // In text this is printed bare, exactly as it always was, because a CI job reads
                // the line. In json it becomes a result record like everything else.
                Log.Result(Context.Current.EndpointNames.IsSingle()
                    ? "Endpoint Hash"
                    : $"{Context.Current.EndpointName} Hash", hashValue);
            });

            return failed.Any() ? -1 : 0;
        }

        /// <summary>
        /// Runs the action against every endpoint of the run, and returns the ones it threw for
        /// rather than abandoning the rest.
        /// </summary>
        static string[] Attempt(string what, Action<string> action)
        {
            var failed = new List<string>();

            foreach (var endpoint in Context.Current.EndpointNames)
            {
                Log.Item = endpoint;

                // The scope is closed before the item is cleared, so its closing line is attributed
                // to the same item as everything inside it.
                using (var scope = Log.Step($"{what} {endpoint}"))
                {
                    try { action(endpoint); }
                    catch (Exception ex)
                    {
                        scope.Fail();
                        failed.Add(endpoint);

                        // Against whichever name this endpoint is already known by. Selecting it is
                        // what resolves the name it was asked for into a full type name, and sets
                        // Log.Item to it; before that, the only name there is is the one the caller
                        // passed. Reporting the asked-for name unconditionally gave an endpoint that
                        // was named in short form two rows in the summary, one per name.
                        RunSummary.Current.For(Log.Item.Or(endpoint)).Fail(ex.Message);
                        Log.Error(endpoint, ex);
                    }
                }

                Log.Item = null;
            }

            return failed.ToArray();
        }

        static int Generate()
        {
            Context.Current.PrepareOutputDirectory();

            if (Context.Current.EndpointNames.None())
                throw new Exception("No data endpoint was named. Pass /dataEndpoint: with one name, " +
                    "or several separated by a semicolon.");

            var packages = new List<NugetCreatorBase>();
            var projects = new List<ProjectCreatorBase>();

            // Which endpoint each project belongs to, so a build failure can be blamed on the one
            // that caused it rather than on the batch.
            var owner = new Dictionary<ProjectCreatorBase, string>();

            // Phase one: generate every project of every endpoint. Everything that reads Context
            // runs here, while it still points at the endpoint being generated. An endpoint that
            // cannot be generated is reported and skipped, leaving the rest of the batch intact.
            var notGenerated = Attempt("Generate", endpoint =>
            {
                Context.Current.Select(endpoint);
                Timings.Measure("Find exposed types", Context.Current.FindExposedTypes);

                var reported = RunSummary.Current.For(Context.Current.EndpointName);
                reported.Note(Context.Current.ExposedTypes.Count + " exposed types");

                // Staged, so a half-generated endpoint contributes nothing to the build.
                var creators = new List<ProjectCreatorBase>();
                var nugets = new List<NugetCreatorBase>();

                var endPointCreator = new EndpointProjectCreator();
                endPointCreator.Generate();
                creators.Add(endPointCreator);
                nugets.Add(new NugetCreator(endPointCreator).PrepareNuspec());

                if (Context.Current.ExposedTypes.Any())
                {
                    var mSharpCreator = new MSharpProjectCreator();
                    mSharpCreator.Generate();
                    creators.Add(mSharpCreator);
                    nugets.Add(new NugetCreator(mSharpCreator).PrepareNuspec());
                }

                reported.Note(creators.Count + " projects");
                creators.Do(x => owner[x] = Context.Current.EndpointName);

                projects.AddRange(creators);
                packages.AddRange(nugets);
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

            // Phase three: packing and publishing read only run-wide settings, so the endpoint the
            // context points at no longer matters. A package whose project failed is skipped, so
            // one bad endpoint in a batch does not cost every other one its package.
            packages.Where(x => !x.Projects.Intersects(failed)).Do(x => x.PackAndPublish());

            if (failed.Any() || notGenerated.Any() || RunSummary.Current.AnyFailed) return -1;

            return 0;
        }
    }
}
