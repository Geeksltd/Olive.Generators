namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using Olive;

    /// <summary>
    /// Builds every generated project of a run together, and owns the retry for when the target
    /// microservice's package versions turn out to be unrestorable.
    /// </summary>
    static class ProjectBuilder
    {
        /// <summary>
        /// Builds every project in one invocation and returns the ones that failed, so the caller
        /// can still publish the packages whose projects are sound.
        /// </summary>
        public static ProjectCreatorBase[] BuildAll(DirectoryInfo root, IEnumerable<ProjectCreatorBase> creators)
        {
            var all = creators.ToArray();
            if (all.None()) return Array.Empty<ProjectCreatorBase>();

            try
            {
                SolutionBuilder.Build(root, all.Select(x => x.ProjectFile));
                return Array.Empty<ProjectCreatorBase>();
            }
            // CanFallBack, not IsFromTarget: the question here is whether rebuilding would reference
            // anything different, not where the version came from. A target that already sits on this
            // generator's own default version has nothing to fall back to, and retrying it is a
            // second full build of the identical thing, failing identically.
            catch (Exception ex) when (all.Any(c => c.References.Any(x => x.CanFallBack)) &&
                TargetPackageVersions.IsMissingPackageVersion(ex))
            {
                // The versions the target microservice uses are on none of the feeds reachable
                // from the temp folder. A package built against this generator's defaults is
                // better than none at all.
                Log.Warn("Could not build against the package versions the target application uses: " +
                    TargetPackageVersions.SummariseFailure(ex));

                using var scope = Log.Step("Rebuild against this generator's default package versions");

                // All of them together: a package whose projects reference different versions of
                // the same dependency is not something to publish.
                all.Do(x => x.FallBackToDefaultVersions());

                var failed = Attempt(root, all);
                if (failed.Any()) scope.Fail();

                return failed;
            }
            catch (Exception ex)
            {
                return Report(ex, all);
            }
        }

        static ProjectCreatorBase[] Attempt(DirectoryInfo root, ProjectCreatorBase[] all)
        {
            try
            {
                SolutionBuilder.Build(root, all.Select(x => x.ProjectFile));
                return Array.Empty<ProjectCreatorBase>();
            }
            catch (Exception ex) { return Report(ex, all); }
        }

        static ProjectCreatorBase[] Report(Exception error, ProjectCreatorBase[] all)
        {
            var blamed = SolutionBuilder.Blame(error, all.Select(x => x.ProjectFile));

            var failed = all.Where(x => blamed.Any(b => b.FullName == x.ProjectFile.FullName)).ToArray();

            Log.Error("Failed to build " + failed.Select(x => x.Name).ToString(", "),
                new Exception(TargetPackageVersions.SummariseFailure(error)));

            if (failed.Length < all.Length)
                Log.Info("Still publishing: " + all.Except(failed).Select(x => x.Name).ToString(", "));

            return failed;
        }
    }
}
