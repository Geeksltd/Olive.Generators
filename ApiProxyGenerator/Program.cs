using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Olive;

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
            RunSummary.Current.ItemNoun = "controller";

            Begin(args, "Olive Api Proxy Generator");
            ParametersParser.SetArgs(args);

            // Kept, so that the run's closing record can name the code the process exits with.
            var exitCode = -1;

            try
            {
                // Inside the try: reading the parameters is a step of the run like any other, and a
                // failure there has to be reported rather than escaping the entry point.
                if (!ParametersParser.Current.Start())
                {
                    Helper.ShowHelp();
                    return exitCode;
                }

                ParametersParser.Current.LoadParameters(null);

                Log.OpenDefaultFile(Context.Current.TempPath);
                Log.Item = Context.Current.ControllerName;
                Describe();

                Timings.Measure("Load the target application", Context.Current.LoadAssembly);
                Context.Current.PrepareOutputDirectory();

                Timings.Measure("Find the dto types", () => DtoTypes.FindAll(
                    Context.Current.ActionMethods.SelectMany(x => x.GetArgAndReturnTypes()).ToArray()
                    , Context.Current.AssemblyObj));

                DtoDataProviderClassGenerator.ValidateRemoteDataProviderAttributes();

                var reported = RunSummary.Current.For(Context.Current.ControllerName);
                reported.Note(Context.Current.ActionMethods.Length + " api actions");
                reported.Note(DtoTypes.All.Count + " dto types");

                var proxyCreator = new ProxyProjectCreator();
                proxyCreator.Generate();

                var mSharpCreators = DtoTypes.All.Any()
                    ? new ProjectCreatorBase[] { new MSharpProjectCreator() }
                    : Array.Empty<ProjectCreatorBase>();

                mSharpCreators.Do(x => x.Generate());

                var packages = new List<NugetCreatorBase> { new NugetCreator(proxyCreator).PrepareNuspec() };
                if (mSharpCreators.Any()) packages.Add(new NugetCreator(mSharpCreators).PrepareNuspec());

                // One MSBuild invocation for the lot, instead of one per project.
                var failed = Timings.Measure("Build every generated project", () => ProjectBuilder.BuildAll(Context.Current.TempPath,
                    new[] { (ProjectCreatorBase)proxyCreator }.Concat(mSharpCreators)));

                foreach (var project in failed)
                    reported.Fail("the generated project did not build");

                if (proxyCreator.UseFallbackVersions)
                    Log.Warn("The packages of this run were built against this generator's own " +
                        "default package versions, not the ones the target application uses.");

                packages.Where(x => !x.Projects.Intersects(failed)).Do(x => x.PackAndPublish());

                if (failed.None() && !RunSummary.Current.AnyFailed)
                {
                    // Before the copy below, not only in the finally. Alone among the generators this
                    // one hands the whole of its temp folder to /out:, and the adopted nuget config
                    // in it can carry the target's feed credentials. Publishing is already done by
                    // this point, so nothing still needs the feeds.
                    TargetNugetConfig.RemoveFrom(Context.Current.TempPath);

                    if (Context.Current.Output != null)
                        using (Log.Step("Copy the generated sources to " + Context.Current.Output.FullName))
                            Context.Current.TempPath.CopyTo(Context.Current.Output.FullName, true);

                    exitCode = 0;
                }
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

                RunSummary.Current.Report();
                Timings.Report();
                Log.Shutdown(exitCode);
            }

            return exitCode;
        }

        /// <summary>What this run was asked to do, before it starts doing it.</summary>
        static void Describe()
        {
            Log.Field("publisher service", Context.Current.PublisherService);
            Log.Field("api assembly", Context.Current.AssemblyFile);
            Log.Field("api controller", Context.Current.ControllerName);
            Log.Field("temp folder", Context.Current.TempPath);
            Log.Field("publish to", (object)Context.Current.Output ?? Context.Current.NugetServer);
            Log.Field("log file", Log.FilePath);
        }

    }
}