namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Security.Cryptography;
    using System.Text;
    using Olive;

    /// <summary>
    /// Builds every generated project of a run in a single MSBuild invocation.
    /// <para>
    /// One "dotnet build" per project meant one SDK start and one full nuget restore per project,
    /// and they had to run one at a time because each pointed the child process at its own folder
    /// by setting the process-wide current directory. A solution restores once and lets MSBuild
    /// build the projects in parallel itself.
    /// </para>
    /// </summary>
    static class SolutionBuilder
    {
        /// <summary>The project type guid for a C# SDK-style project.</summary>
        const string CSharpProjectType = "9A19103F-16F7-4668-BE54-9A1E7A4F7556";

        const string SolutionName = "generated.sln";

        /// <summary>
        /// Debug, because the nuspec templates refer to bin\Debug\&lt;framework&gt; directly.
        /// </summary>
        const string Configuration = "Debug";

        /// <summary>
        /// How many projects MSBuild may build at once, or null to leave it one per processor.
        /// <para>
        /// A run of many endpoints is one solution of a few hundred projects, and a bare -m gives
        /// MSBuild a worker node per processor. The node count therefore follows the core count
        /// while the memory each node holds does not, which is what makes a build agent with many
        /// cores and little memory the case this exists for.
        /// </para>
        /// </summary>
        public static int? MaxCpu;

        /// <summary>What to tell MSBuild about parallelism. Unset, this is what every run used.</summary>
        static string Parallelism => MaxCpu is int count ? "-m:" + count : "-m";

        public static void Build(DirectoryInfo root, IEnumerable<FileInfo> projects)
        {
            var all = projects.ToArray();
            if (all.None()) return;

            foreach (var project in all)
                Log.Detail("to build: " + project.FullName);

            if (all.IsSingle())
            {
                // A solution buys nothing for one project, and skipping it keeps the log readable.
                Run(root, all.Single().FullName, all.Length);
                return;
            }

            var solution = root.GetFile(SolutionName);
            solution.WriteAllText(Render(root, all));
            Log.Detail("wrote " + solution.FullName);

            Run(root, solution.FullName, all.Length);
        }

        static void Run(DirectoryInfo root, string target, int projects)
        {
            using var scope = Log.Step($"Build {Path.GetFileName(target)} ({projects} project{(projects == 1 ? "" : "s")})");

            try
            {
                // The terminal logger is turned off through MSBUILDTERMINALLOGGER instead of -tl, which
                // older SDKs on a build agent would reject outright.
                ProcessRunner.DotNet(
                    $"build \"{target}\" -c {Configuration} {Parallelism} --nologo -v:{MSBuildVerbosity}", root);
            }
            catch { scope.Fail(); throw; }
        }

        /// <summary>
        /// How much MSBuild itself says, following the verbosity the run was asked for. It used to
        /// be pinned at quiet, which meant the longest step of a run reported nothing at all, not
        /// even which project it was on when it stalled.
        /// </summary>
        static string MSBuildVerbosity
        {
            get
            {
                switch (Log.Level)
                {
                    case LogLevel.Quiet:
                    case LogLevel.Minimal: return "q";
                    case LogLevel.Detailed: return "n";
                    case LogLevel.Diagnostic: return "d";
                    default: return "m";
                }
            }
        }

        /// <summary>
        /// The projects a build failure blames, so one bad item in a batch does not discard the
        /// packages of every other one. MSBuild prefixes each error with the full path of the
        /// project it came from. When nothing can be attributed, every project is treated as
        /// failed, which is what a single build of the whole batch used to mean anyway.
        /// </summary>
        public static FileInfo[] Blame(Exception error, IEnumerable<FileInfo> projects)
        {
            var all = projects.ToArray();

            var message = error?.Message;
            if (message.IsEmpty()) return all;

            // By the project's folder as well as its own path. MSBuild prefixes a compile error with
            // the source file that caused it, which lives in the project's folder but is not the
            // csproj, so matching the csproj alone attributed nothing and blamed the whole batch for
            // one item's compile error. The separator is part of the match: without it the folder of
            // "Foo.Endpoint" is a substring of the folder of "FooBar.Endpoint".
            var named = all
                .Where(x => message.Contains(x.FullName, StringComparison.OrdinalIgnoreCase) ||
                            message.Contains(x.Directory.FullName + Path.DirectorySeparatorChar,
                                StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (named.Any()) return named;

            // Worth saying out loud. Everything the caller does after this — which packages it
            // skips, which items it reports as failed, which of them its own caller then retries —
            // follows from an attribution that did not happen, and the run is about to fail every
            // item in the batch for what may be a fault in one of them.
            Log.Warn($"The build failed without naming any of the {all.Length} generated projects, " +
                "so every item of this run is treated as failed. The build output in the log says " +
                "what actually went wrong.");

            return all;
        }

        static string Render(DirectoryInfo root, FileInfo[] projects)
        {
            var result = new StringBuilder();

            result.AppendLine();
            result.AppendLine("Microsoft Visual Studio Solution File, Format Version 12.00");

            var entries = projects.Select(x => new
            {
                // The folder name, not the project name: two projects of one package share a
                // project file name (MSharp and MSharp46 both produce MSharp.csproj), and a
                // solution needs the display names to be distinct.
                Name = x.Directory.Name,
                Path = System.IO.Path.GetRelativePath(root.FullName, x.FullName),
                Id = DeterministicGuid(x.FullName)
            }).ToArray();

            foreach (var item in entries)
            {
                result.AppendLine($"Project(\"{{{CSharpProjectType}}}\") = \"{item.Name}\", \"{item.Path}\", \"{{{item.Id}}}\"");
                result.AppendLine("EndProject");
            }

            result.AppendLine("Global");
            result.AppendLine("\tGlobalSection(SolutionConfigurationPlatforms) = preSolution");
            result.AppendLine($"\t\t{Configuration}|Any CPU = {Configuration}|Any CPU");
            result.AppendLine("\tEndGlobalSection");
            result.AppendLine("\tGlobalSection(ProjectConfigurationPlatforms) = postSolution");

            foreach (var item in entries)
            {
                result.AppendLine($"\t\t{{{item.Id}}}.{Configuration}|Any CPU.ActiveCfg = {Configuration}|Any CPU");
                result.AppendLine($"\t\t{{{item.Id}}}.{Configuration}|Any CPU.Build.0 = {Configuration}|Any CPU");
            }

            result.AppendLine("\tEndGlobalSection");
            result.AppendLine("EndGlobal");

            return result.ToString();
        }

        /// <summary>A stable guid per project path, so a regenerated solution is identical.</summary>
        static string DeterministicGuid(string path)
        {
            using var hasher = MD5.Create();
            var hash = hasher.ComputeHash(Encoding.UTF8.GetBytes(path.ToLowerInvariant()));

            return new Guid(hash).ToString().ToUpperInvariant();
        }
    }
}
