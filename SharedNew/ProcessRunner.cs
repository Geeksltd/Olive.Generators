namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Olive;

    /// <summary>
    /// Runs the external tool the generators depend on: the dotnet CLI.
    /// <para>
    /// Every generator used to have its own copy of this, each starting cmd.exe and feeding it the
    /// command line through stdin. That doubled the process count, left the child's standard error
    /// unread (so a build that filled the pipe deadlocked the generator), and pointed the child at
    /// the target folder by mutating the process-wide current directory, which is what forced the
    /// project builds to run one at a time.
    /// </para>
    /// </summary>
    static class ProcessRunner
    {
        /// <summary>
        /// The dotnet CLI that is running this generator, so a child build uses the same one rather
        /// than whatever PATH happens to resolve to.
        /// </summary>
        static readonly string DotNetPath =
            Environment.GetEnvironmentVariable("DOTNET_HOST_PATH").OrNullIfEmpty() ?? "dotnet";

        /// <summary>The tools a run depends on, reported once so a wrong one is visible up front.</summary>
        public static void Describe() => Log.Field("dotnet", DotNetPath, LogLevel.Detailed);

        /// <summary>
        /// Settings that only slow a generated build down or corrupt what we read back from it.
        /// The terminal logger matters most: its ANSI escapes would break the nuget error-code
        /// sniffing that the fallback-version retry depends on.
        /// </summary>
        static readonly Dictionary<string, string> ChildEnvironment = new Dictionary<string, string>
        {
            ["DOTNET_NOLOGO"] = "1",
            ["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1",
            ["DOTNET_SKIP_FIRST_TIME_EXPERIENCE"] = "1",
            ["DOTNET_GENERATE_ASPNET_CERTIFICATE"] = "false",
            ["NUGET_XMLDOC_MODE"] = "skip",
            ["MSBUILDTERMINALLOGGER"] = "off",

            // This tool exits when its run ends, but MSBuild keeps the worker nodes it started
            // alive for another fifteen minutes by default, each holding well over a hundred
            // megabytes with nothing left to build. A one-shot command has nothing to reuse them
            // for, and back to back runs of different generators each left a pool of their own.
            ["MSBUILDDISABLENODEREUSE"] = "1",
        };

        /// <summary>
        /// Runs the dotnet CLI. Anything passed as <paramref name="secret"/> is kept out of the
        /// failure message: a push carries the api key on its command line, and the message ends up
        /// in the build log.
        /// </summary>
        public static string DotNet(string arguments, DirectoryInfo workingDirectory, string secret = null)
            => Run(DotNetPath, arguments, workingDirectory, secret);

        public static string Run(string executable, string arguments, DirectoryInfo workingDirectory,
            string secret = null)
        {
            var name = Step(executable, arguments);

            using var scope = Log.Step(name, LogLevel.Detailed);

            Log.Detail("command: " + executable + " " + arguments);
            Log.Diagnostic("in: " + (workingDirectory?.FullName ?? Environment.CurrentDirectory));

            try { return Execute(executable, arguments, workingDirectory, secret, name); }
            catch { scope.Fail(); throw; }
        }

        /// <summary>A name for the step, e.g. "dotnet build" or "dotnet nuget".</summary>
        static string Step(string executable, string arguments)
            => Path.GetFileNameWithoutExtension(executable) + " " + arguments.OrEmpty().Split(' ').FirstOrDefault();

        static string Execute(string executable, string arguments, DirectoryInfo workingDirectory,
            string secret, string name)
        {
            var info = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                WorkingDirectory = workingDirectory?.FullName ?? Environment.CurrentDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
            };

            foreach (var item in ChildEnvironment) info.Environment[item.Key] = item.Value;

            var output = new StringBuilder();
            var error = new StringBuilder();

            using var process = new Process { StartInfo = info };

            // Streamed as it arrives as well as collected. Buffering it and printing nothing until
            // the process ended is what made a build or a restore look identical to a hang: these
            // are the longest steps of a run and they used to say one word between them.
            process.OutputDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (output) output.AppendLine(e.Data);
                Log.Child(name, e.Data);
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data == null) return;
                lock (error) error.AppendLine(e.Data);
                Log.Child(name, e.Data);
            };

            try { process.Start(); }
            catch (Exception ex)
            {
                throw new Exception($"Failed to start '{executable}': {ex.Message}", ex);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            Log.Diagnostic($"{name} started as process {Id(process)}.");

            // The parameterless overload also waits for the redirected streams to reach end of
            // file. The one taking a timeout does not, which is how output goes missing.
            process.WaitForExit();

            Log.Detail($"{name} exited with code {process.ExitCode}.");

            var combined = Combine(output.ToString(), error.ToString());

            // The whole of it, not just standard error: nuget writes NU1101 and its siblings to
            // standard output under dotnet build, and the fallback-version retry looks for them.
            if (process.ExitCode != 0)
                throw new Exception(Redact($"'{executable} {arguments}' failed with exit code " +
                    $"{process.ExitCode}.{Environment.NewLine}{Significant(combined)}", secret));

            return Redact(combined, secret).Trim();
        }

        /// <summary>
        /// The part of a failed run's output worth putting in an exception message: every line that
        /// names an error or a nuget diagnostic, and the tail. At the higher verbosities the whole
        /// of it would be thousands of lines, and the message is read by a person, echoed to the
        /// console and searched for nuget's NU codes by the fallback-version retry. The full output
        /// is in the log file either way, because it was streamed there as it arrived.
        /// </summary>
        static string Significant(string output)
        {
            const int MaxErrorLines = 200, TailLines = 40;

            var lines = output.OrEmpty().Replace("\r", "").Split('\n');
            if (lines.Length <= MaxErrorLines + TailLines) return output;

            var interesting = lines
                .Where(x => x.Contains("error", StringComparison.OrdinalIgnoreCase) ||
                            x.Contains("warning NU", StringComparison.OrdinalIgnoreCase) ||
                            x.Contains("Unable to find package", StringComparison.OrdinalIgnoreCase))
                .Take(MaxErrorLines)
                .ToList();

            var tail = lines.Skip(Math.Max(0, lines.Length - TailLines));

            interesting.Add($"... {lines.Length} lines of output in total. The whole of it is in {Log.FilePath.OrEmpty().Or("the log file")}.");
            interesting.AddRange(tail);

            return interesting.ToLinesString();
        }

        /// <summary>The process id, when the process ended too quickly to still have one.</summary>
        static string Id(Process process)
        {
            try { return process.Id.ToString(); }
            catch (InvalidOperationException) { return "(already exited)"; }
        }

        /// <summary>Keeps a credential out of anything that reaches the console or a log.</summary>
        static string Redact(string text, string secret)
            => secret.IsEmpty() ? text : text.Replace(secret, "***");

        static string Combine(string output, string error)
        {
            if (error.IsEmpty()) return output;
            if (output.IsEmpty()) return error;

            return output.TrimEnd() + Environment.NewLine + error;
        }
    }
}
