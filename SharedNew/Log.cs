namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Text;
    using System.Threading;
    using OliveGenerator.Logging;

    /// <summary>
    /// How much of a run reaches the console. Lower is more important, so a message is shown when
    /// its own level is at or below the configured one. The log file always receives everything.
    /// </summary>
    enum LogLevel { Quiet, Minimal, Normal, Detailed, Diagnostic }

    /// <summary>
    /// How the console is written. Text is meant for a person; json emits one self-describing
    /// record per line, for another program to read.
    /// </summary>
    enum LogFormat { Text, Json }

    /// <summary>
    /// What a line is, as opposed to how verbose a run has to be for it to be shown. The two
    /// usually follow each other, but not always: the run summary has to survive
    /// /verbosity:minimal without claiming to be a warning, and a step that closes as failed is an
    /// error however ordinary its opening line was.
    /// </summary>
    enum Severity { Error, Warning, Info, Debug, Trace }

    /// <summary>
    /// One open step of a run. Disposing it closes the step and reports how long it took.
    /// </summary>
    sealed class LogScope : IDisposable
    {
        internal string Title;
        internal LogLevel Level;
        internal int Depth;
        internal readonly Stopwatch Watch = Stopwatch.StartNew();
        internal bool Failed;
        internal string Outcome;
        bool Disposed;

        /// <summary>Adds a short result to the closing line, e.g. how many types were found.</summary>
        public LogScope Note(string outcome) { Outcome = outcome; return this; }

        /// <summary>Marks the step as failed, so the closing line says so.</summary>
        public void Fail(string why = null)
        {
            Failed = true;
            if (why != null) Outcome = why;
        }

        public void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            Log.CloseScope(this);
        }
    }

    /// <summary>
    /// Everything a run says about itself.
    /// <para>
    /// The generators used to print a line before a step and the word "Done" after it, which said
    /// nothing about how long anything took, went silent for the whole of the longest steps (the
    /// build and the pack, whose child process output was buffered and then discarded), and could
    /// not be attributed to one item of a batch. This replaces that with timestamped, nested,
    /// self-timing steps, live child process output, a heartbeat while a step is silent, and a
    /// full-detail log file that survives a run the agent killed.
    /// </para>
    /// <para>
    /// Every line carries its own severity, and <c>/logformat:json</c> turns the console itself
    /// into one record per line, so another program reading this tool's output never has to infer
    /// anything from the shape of a sentence.
    /// </para>
    /// </summary>
    static class Log
    {
        const int DefaultHeartbeatSeconds = 15;

        /// <summary>Enough to cover everything said before the temp folder, and so the log file, exists.</summary>
        const int MaxBufferedLines = 10000;

        /// <summary>The width of the severity column, which is the widest name plus nothing.</summary>
        const int SeverityWidth = 5;

        static readonly object Gate = new object();
        static readonly Stopwatch Clock = Stopwatch.StartNew();
        static readonly DateTime StartedAt = DateTime.UtcNow;
        static readonly List<LogScope> OpenScopes = new List<LogScope>();
        static readonly List<KeyValuePair<string, TimeSpan>> Recorded = new List<KeyValuePair<string, TimeSpan>>();
        static readonly List<string> Buffered = new List<string>();

        static StreamWriter FileSink, JsonSink;
        static TimeSpan LastActivity;
        static Timer Heartbeat;
        static int HeartbeatSeconds = DefaultHeartbeatSeconds;
        static string[] Items = Array.Empty<string>();
        static bool Colours = CanColour();
        static bool BufferOverflowed;
        static long Sequence;

        /// <summary>Whether the run has already been closed. Read through Interlocked.</summary>
        static int ShutDown;

        static readonly bool UnderGithubActions =
            "true".Equals(Environment.GetEnvironmentVariable("GITHUB_ACTIONS"), StringComparison.OrdinalIgnoreCase);

        /// <summary>Identifies this run, so records of concurrent runs can be told apart.</summary>
        public static string RunId { get; } = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>How much reaches the console. The log file always gets everything.</summary>
        public static LogLevel Level { get; private set; } = LogLevel.Normal;

        /// <summary>How the console is written.</summary>
        public static LogFormat Format { get; private set; } = LogFormat.Text;

        /// <summary>Whether the console is a stream of records rather than something to read.</summary>
        public static bool IsJson => Format == LogFormat.Json;

        /// <summary>The item currently being generated, named on every record.</summary>
        public static string Item { get; set; }

        /// <summary>Where the full-detail log is being written, once it has been opened.</summary>
        public static string FilePath { get; private set; }

        /// <summary>Kept out of every line, whichever sink it goes to.</summary>
        public static string Secret { get; set; }

        public static bool IsEnabled(LogLevel level) => level <= Level;

        /// <summary>The elapsed time of every step of the run, for the timing breakdown.</summary>
        public static KeyValuePair<string, TimeSpan>[] Steps
        {
            get { lock (Gate) return Recorded.ToArray(); }
        }

        public static TimeSpan Elapsed => Clock.Elapsed;

        // ---------------------------------------------------------------- Setting up

        /// <summary>
        /// Reads the logging switches off the command line. Safe to skip: everything below works
        /// at the default level and format with no sinks other than the console.
        /// </summary>
        public static void Initialize(string[] args)
        {
            args = args ?? Array.Empty<string>();

            // First, so that anything this method itself has to report already knows whether the
            // console is a stream of records that a stray sentence would corrupt.
            Format = ParseFormat(Param(args, "logformat")) ?? Format;

            Level = ParseLevel(Param(args, "verbosity") ?? Param(args, "v")) ?? Level;

            // Set before anything is written, including the command line this run was given: the
            // api key is passed as an argument and the log goes into a build log.
            Secret = Param(args, "apiKey");

            if (IsJson || args.Any(x => Is(x, "/nocolor") || Is(x, "/nocolour"))) Colours = false;

            if (int.TryParse(Param(args, "heartbeat"), out var seconds))
                HeartbeatSeconds = Math.Max(0, seconds);

            var json = Param(args, "logjson");
            if (json.HasContent()) JsonSink = Create(json);

            var file = Param(args, "log");
            if (file.HasContent()) OpenFile(file);

            if (HeartbeatSeconds > 0)
                Heartbeat = new Timer(_ => Beat(), null, 1000, 1000);
        }

        /// <summary>
        /// Opens the run: which tool this is, which version, and what it was asked to do. In text
        /// this is one line naming the absolute start time, so every elapsed stamp below it can be
        /// turned back into a wall clock time.
        /// </summary>
        public static void Run(string tool, string version, string[] args)
        {
            Write(LogLevel.Normal, null,
                $"{tool} {version} started {StartedAt:yyyy-MM-dd'T'HH:mm:ss'Z'} (run {RunId}, process {Environment.ProcessId})",
                kind: "run-start",
                fields: new[]
                {
                    Text("tool", tool),
                    Text("version", version),
                    Text("startedAt", StartedAt.ToString("o", CultureInfo.InvariantCulture)),
                    Count("processId", Environment.ProcessId),
                    Text("commandLine", string.Join(" ", args.OrNone())),
                });
        }

        /// <summary>
        /// Starts writing the full-detail log to the given file. Anything said before this point
        /// is written to it first, so the file covers the whole run however late it was opened.
        /// </summary>
        public static void OpenFile(string path)
        {
            var sink = Create(path);
            if (sink == null) return;

            lock (Gate)
            {
                FileSink?.Dispose();
                FileSink = sink;
                FilePath = Path.GetFullPath(path);

                foreach (var line in Buffered) sink.WriteLine(line);

                if (BufferOverflowed)
                    sink.WriteLine("[log] Earlier lines were dropped: the run said more before this file was opened than the buffer holds.");

                Buffered.Clear();
            }
        }

        /// <summary>
        /// Opens the default log file inside the run's own temp folder, unless one was named on the
        /// command line. A failed run then leaves a full-detail record behind, whatever the console
        /// verbosity was, and however the agent ended the process.
        /// </summary>
        public static void OpenDefaultFile(DirectoryInfo runFolder)
        {
            if (FilePath != null || runFolder == null) return;

            // Beside the run's own folder rather than inside it: the generators wipe that folder
            // before they write any project into it, and a log deleted halfway through a run is
            // worth nothing. The folder is named after the run, so parallel runs do not collide.
            var folder = runFolder.Parent ?? runFolder;

            OpenFile(Path.Combine(folder.FullName, runFolder.Name + ".log"));
        }

        /// <summary>The items of the run, so every line of a batch can say which one it belongs to.</summary>
        public static void SetItems(string[] items) => Items = items ?? Array.Empty<string>();

        /// <summary>
        /// Closes the run with the code the process is about to exit with, then flushes and closes
        /// the sinks. Never leave a run without calling this.
        /// </summary>
        public static void Shutdown(int? exitCode = null)
        {
            // Once only. A run now has more than one way out — a parameter that cannot be read, a
            // failure caught by the entry point's own guard — and a second run-end record would
            // tell a reader that one process was two runs.
            if (Interlocked.Exchange(ref ShutDown, 1) == 1) return;

            var timer = Heartbeat;
            Heartbeat = null;
            timer?.Dispose();

            Write(LogLevel.Normal, null,
                $"Finished in {Duration(Clock.Elapsed)}" + (exitCode == null ? null : $" with exit code {exitCode}."),
                kind: "run-end",
                fields: new[]
                {
                    Number("durationSeconds", Clock.Elapsed.TotalSeconds),
                    exitCode == null ? Raw("exitCode", "null") : Count("exitCode", exitCode.Value),
                });

            lock (Gate)
            {
                Flush(FileSink);
                Flush(JsonSink);
                FileSink = null;
                JsonSink = null;
            }
        }

        static void Flush(StreamWriter sink)
        {
            if (sink == null) return;

            try { sink.Flush(); sink.Dispose(); }
            catch (IOException) { /* Nothing useful to say about a log that cannot be closed. */ }
        }

        // ---------------------------------------------------------------- Saying things

        public static void Error(Exception error) => Error(null, error);

        public static void Error(string context, Exception error)
        {
            MarkFailed();

            var described = Describe(context, error);

            Write(LogLevel.Quiet, null, described, ConsoleColor.Red, kind: "error",
                fields: new[] { Text("exception", error?.GetType().FullName) });

            if (UnderGithubActions) WriteRaw("::error::" + Redact(OneLine(described)));

            // Always into the log file, and onto the console only at the higher verbosities.
            var stack = StackOf(error);
            if (stack.HasContent()) Write(LogLevel.Detailed, null, stack, kind: "stack");
        }

        public static void Error(string message)
        {
            MarkFailed();
            Write(LogLevel.Quiet, null, message, ConsoleColor.Red, kind: "error");
            if (UnderGithubActions) WriteRaw("::error::" + Redact(OneLine(message)));
        }

        public static void Warn(string message)
        {
            Write(LogLevel.Minimal, null, message, ConsoleColor.Yellow, kind: "warning");
            if (UnderGithubActions) WriteRaw("::warning::" + Redact(OneLine(message)));
        }

        /// <summary>Shown at the default verbosity: the shape of the run.</summary>
        public static void Info(string message) => Write(LogLevel.Normal, null, message);

        /// <summary>Shown with /verbosity:detailed: what each step actually did.</summary>
        public static void Detail(string message) => Write(LogLevel.Detailed, null, message);

        /// <summary>Shown with /verbosity:diagnostic: the settings a step resolved before running.</summary>
        public static void Diagnostic(string message) => Write(LogLevel.Diagnostic, null, message);

        /// <summary>
        /// A line of the end-of-run summary. Shown at every verbosity but quiet: whatever else a
        /// run printed, what it produced has to survive.
        /// </summary>
        public static void Headline(string message, bool failed = false)
        {
            // A blank separator line is a shape for a reader, and an empty record for a parser.
            if (message.IsBlank() && IsJson) return;

            Write(LogLevel.Minimal, null, message, failed ? ConsoleColor.Red : (ConsoleColor?)null,
                depth: 0, kind: "summary", severity: failed ? Severity.Error : Severity.Info);
        }

        /// <summary>The totals of the run, as one record.</summary>
        public static void SummaryTotals(string message, int items, int packages, int failed)
            => Write(LogLevel.Minimal, null, message, depth: 0, kind: "summary-totals", severity: Severity.Info,
                fields: new[]
                {
                    Count("items", items),
                    Count("packagesPublished", packages),
                    Count("itemsFailed", failed),
                    Number("durationSeconds", Clock.Elapsed.TotalSeconds),
                });

        /// <summary>What became of one endpoint, command or controller.</summary>
        public static void SummaryItem(string message, string status, string notes, string failure)
            => Write(LogLevel.Minimal, null, message, depth: 0, kind: "summary-item",
                severity: status == "FAILED" ? Severity.Error : Severity.Info,
                fields: new[]
                {
                    Text("status", status),
                    Text("notes", notes),
                    Text("failure", failure),
                });

        /// <summary>What became of one package, and where it went.</summary>
        public static void SummaryPackage(string message, string id, string version,
            string destination, string status, string failure)
            => Write(LogLevel.Minimal, null, message, depth: 0, kind: "summary-package",
                severity: status == "FAILED" ? Severity.Error : Severity.Info,
                fields: new[]
                {
                    Text("package", id),
                    Text("packageVersion", version),
                    Text("destination", destination),
                    Text("status", status),
                    Text("failure", failure),
                });

        /// <summary>A named setting of the run, reported as a name and a value in either format.</summary>
        public static void Field(string name, object value, LogLevel level = LogLevel.Normal)
        {
            var rendered = (value?.ToString()).Or("(none)");

            Write(level, null, name.PadRight(16) + " " + rendered, kind: "field",
                fields: new[] { Text("name", name), Text("value", rendered) });
        }

        /// <summary>
        /// A value this tool exists to report, such as an endpoint's hash. In text it is printed
        /// bare and unprefixed, because that is the line a pipeline has always read.
        /// </summary>
        public static void Result(string name, string value)
        {
            if (IsJson)
            {
                Write(LogLevel.Quiet, null, name + " = " + value, kind: "result", severity: Severity.Info,
                    fields: new[] { Text("name", name), Text("value", value) });

                return;
            }

            lock (Gate)
            {
                Console.Out.WriteLine(name + " = " + value);
                LastActivity = Clock.Elapsed;
                Buffer(name + " = " + value);

                // The json sink is fed here as well, and not only in the branch above. It is chosen
                // with /logjson: and is independent of the console format, so a run writing text to
                // the console and records to a file would otherwise leave the file without the one
                // kind of record the run exists to produce.
                JsonSink?.WriteLine(Record(LogLevel.Quiet, Severity.Info, "result", name + " = " + value,
                    0, new[] { Text("name", name), Text("value", value) }));
            }
        }

        /// <summary>One line of output from a child process, attributed to the process it came from.</summary>
        public static void Child(string source, string line)
            => Write(LogLevel.Detailed, "|", source + "> " + line, kind: "child-output",
                fields: new[] { Text("source", source), Text("output", line) });

        // ---------------------------------------------------------------- Steps

        /// <summary>
        /// Opens a step. Dispose it to close it, which reports the outcome and how long it took.
        /// </summary>
        public static LogScope Step(string title, LogLevel level = LogLevel.Normal)
        {
            var scope = new LogScope { Title = title, Level = level };

            lock (Gate)
            {
                scope.Depth = OpenScopes.Count;
                OpenScopes.Add(scope);
            }

            if (UnderGithubActions && !IsJson && scope.Depth == 0 && level <= Level)
                WriteRaw("::group::" + Redact(title));

            Write(level, ">", title, depth: scope.Depth, kind: "step-start",
                fields: new[] { Text("step", title) });

            return scope;
        }

        /// <summary>Runs the given step, closing it as failed if it throws.</summary>
        public static T Step<T>(string title, Func<T> body, LogLevel level = LogLevel.Normal)
        {
            using var scope = Step(title, level);

            try { return body(); }
            catch { scope.Fail(); throw; }
        }

        public static void Step(string title, Action body, LogLevel level = LogLevel.Normal)
            => Step(title, () => { body(); return 0; }, level);

        internal static void CloseScope(LogScope scope)
        {
            lock (Gate)
            {
                var index = OpenScopes.LastIndexOf(scope);
                if (index >= 0) OpenScopes.RemoveRange(index, OpenScopes.Count - index);

                Recorded.Add(new KeyValuePair<string, TimeSpan>(scope.Title, scope.Watch.Elapsed));
            }

            var outcome = scope.Failed ? "failed" : "done";

            var text = scope.Title + "  " + (scope.Failed ? "FAILED" : "done") + " in " + Duration(scope.Watch.Elapsed) +
                (scope.Outcome.HasContent() ? "  " + scope.Outcome : null);

            Write(scope.Level, "<", text, scope.Failed ? ConsoleColor.Red : (ConsoleColor?)null, scope.Depth,
                kind: "step-end", severity: scope.Failed ? Severity.Error : (Severity?)null,
                fields: new[]
                {
                    Text("step", scope.Title),
                    Text("outcome", outcome),
                    Number("durationSeconds", scope.Watch.Elapsed.TotalSeconds),
                    Text("note", scope.Outcome),
                });

            if (UnderGithubActions && !IsJson && scope.Depth == 0 && scope.Level <= Level) WriteRaw("::endgroup::");
        }

        /// <summary>Marks the innermost open step as failed, so its closing line says so.</summary>
        static void MarkFailed()
        {
            lock (Gate)
                if (OpenScopes.Count > 0) OpenScopes[OpenScopes.Count - 1].Failed = true;
        }

        // ---------------------------------------------------------------- The heartbeat

        /// <summary>
        /// Says that the innermost step is still running when nothing has been said for a while.
        /// A stalled restore or a build waiting on a feed that never answers is otherwise
        /// indistinguishable, in a build log, from a fast run.
        /// </summary>
        static void Beat()
        {
            LogScope scope;

            lock (Gate)
            {
                if (OpenScopes.Count == 0) return;
                if ((Clock.Elapsed - LastActivity).TotalSeconds < HeartbeatSeconds) return;

                scope = OpenScopes[OpenScopes.Count - 1];
            }

            Write(LogLevel.Minimal, "...", "still running: " + scope.Title + "  " + Duration(scope.Watch.Elapsed),
                depth: scope.Depth, kind: "heartbeat", severity: Severity.Info,
                fields: new[]
                {
                    Text("step", scope.Title),
                    Number("durationSeconds", scope.Watch.Elapsed.TotalSeconds),
                });
        }

        // ---------------------------------------------------------------- Writing

        static void Write(LogLevel level, string marker, string text, ConsoleColor? colour = null, int? depth = null,
            string kind = "message", Severity? severity = null, params (string Key, string Json)[] fields)
        {
            if (text == null) return;

            var reported = severity ?? DefaultSeverity(level);

            lock (Gate)
            {
                var indent = depth ?? OpenScopes.Count;
                var stamp = Stamp(reported);
                var first = true;

                foreach (var line in text.Replace("\r", "").Split('\n'))
                {
                    var rendered = Redact(stamp + Pad(indent) + Marker(marker, first) + line).TrimEnd();
                    // Trimmed the same way the text is, so the two formats say the same thing.
                    var record = Record(level, reported, kind, line.TrimEnd(), indent, first ? fields : null);
                    first = false;

                    if (level <= Level)
                    {
                        WriteConsole(IsJson ? record : rendered, colour);

                        // Only a line that actually reached the console counts as activity: the
                        // heartbeat exists for whoever is reading it, and a step streaming into
                        // the log file alone still looks dead to them.
                        LastActivity = Clock.Elapsed;
                    }

                    Buffer(rendered);
                    JsonSink?.WriteLine(record);
                }
            }
        }

        /// <summary>A line the sinks take verbatim, for the build agent's own annotation syntax.</summary>
        static void WriteRaw(string line)
        {
            lock (Gate)
            {
                Console.Out.WriteLine(line);
                LastActivity = Clock.Elapsed;
                Buffer(line);
            }
        }

        static void Buffer(string line)
        {
            if (FileSink != null) { FileSink.WriteLine(line); return; }

            if (Buffered.Count >= MaxBufferedLines) { BufferOverflowed = true; return; }

            Buffered.Add(line);
        }

        static void WriteConsole(string line, ConsoleColor? colour)
        {
            if (colour == null || !Colours) { Console.Out.WriteLine(line); return; }

            try
            {
                Console.ForegroundColor = colour.Value;
                Console.Out.WriteLine(line);
            }
            finally { Console.ResetColor(); }
        }

        static string Marker(string marker, bool first)
        {
            if (marker.IsBlank()) return null;

            // Continuation lines line up under the first, without repeating its marker.
            return (first ? marker : new string(' ', marker.Length)) + " ";
        }

        static string Pad(int depth) => depth <= 0 ? "" : new string(' ', Math.Min(depth, 12) * 2);

        /// <summary>
        /// The fixed part every text line starts with: elapsed time, severity, and the item being
        /// generated when a run covers more than one.
        /// </summary>
        static string Stamp(Severity severity)
        {
            var result = "[+" + ((int)Clock.Elapsed.TotalMinutes).ToString("00") + ":" +
                Clock.Elapsed.Seconds.ToString("00") + "." + Clock.Elapsed.Milliseconds / 100 + "] " +
                Name(severity).PadRight(SeverityWidth) + " ";

            // Only worth the width when a run covers more than one item.
            if (Items.Length > 1 && Item.HasContent()) result += "[" + Item + "] ";

            return result;
        }

        /// <summary>The severity a line has when it does not ask for one of its own.</summary>
        static Severity DefaultSeverity(LogLevel level)
        {
            switch (level)
            {
                case LogLevel.Quiet: return Severity.Error;
                case LogLevel.Minimal: return Severity.Warning;
                case LogLevel.Detailed: return Severity.Debug;
                case LogLevel.Diagnostic: return Severity.Trace;
                default: return Severity.Info;
            }
        }

        /// <summary>The name a reader and a parser both see in the severity column.</summary>
        static string Name(Severity severity)
        {
            switch (severity)
            {
                case Severity.Error: return "ERROR";
                case Severity.Warning: return "WARN";
                case Severity.Debug: return "DEBUG";
                case Severity.Trace: return "TRACE";
                default: return "INFO";
            }
        }

        public static string Duration(TimeSpan value)
            => value.TotalSeconds < 60
                ? value.TotalSeconds.ToString("F1", CultureInfo.InvariantCulture) + "s"
                : (int)value.TotalMinutes + "m " + value.Seconds.ToString("00") + "s";

        static string Redact(string text)
            => Secret.IsBlank() || text == null ? text : text.Replace(Secret, "***");

        // ---------------------------------------------------------------- The json records

        /// <summary>
        /// One self-describing record. Every record carries the same head, so a reader never has
        /// to know which kind it is before it can order, filter or attribute it.
        /// </summary>
        static string Record(LogLevel level, Severity severity, string kind, string message, int depth,
            (string Key, string Json)[] fields)
        {
            var builder = new StringBuilder();

            builder.Append("{\"seq\":").Append(++Sequence)
                .Append(",\"run\":").Append(Quote(RunId))
                .Append(",\"time\":").Append(Quote(DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)))
                .Append(",\"elapsedSeconds\":").Append(Number(Clock.Elapsed.TotalSeconds))
                .Append(",\"level\":").Append(Quote(Name(severity)))
                .Append(",\"verbosity\":").Append(Quote(level.ToString().ToLowerInvariant()))
                .Append(",\"kind\":").Append(Quote(kind.Or("message")))
                .Append(",\"depth\":").Append(depth)
                .Append(",\"item\":").Append(Item.HasContent() ? Quote(Item) : "null")
                .Append(",\"message\":").Append(Quote(Redact(message)));

            foreach (var field in fields.OrNone())
            {
                if (field.Key.IsBlank() || field.Json == null) continue;

                builder.Append(',').Append(Quote(field.Key)).Append(':').Append(field.Json);
            }

            return builder.Append('}').ToString();
        }

        /// <summary>A field whose value is text, or nothing at all when there is no value.</summary>
        static (string, string) Text(string key, string value)
            => (key, value.HasContent() ? Quote(Redact(value)) : null);

        static (string, string) Number(string key, double value) => (key, Number(value));

        /// <summary>A field whose value is a whole number, such as a count or an exit code.</summary>
        static (string, string) Count(string key, long value)
            => (key, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>A field whose value is already json, for the few that are not text.</summary>
        static (string, string) Raw(string key, string json) => (key, json);

        /// <summary>
        /// Always with a dot for a decimal point: a machine reads these, and the thousands
        /// separator and comma decimals of some cultures are not json.
        /// </summary>
        static string Number(double value) => value.ToString("F3", CultureInfo.InvariantCulture);

        static string Quote(string value)
        {
            var builder = new StringBuilder("\"");

            foreach (var character in value.Or(""))
                switch (character)
                {
                    case '"': builder.Append("\\\""); break;
                    case '\\': builder.Append("\\\\"); break;
                    case '\n': builder.Append("\\n"); break;
                    case '\r': builder.Append("\\r"); break;
                    case '\t': builder.Append("\\t"); break;
                    default:
                        if (character < ' ') builder.Append("\\u").Append(((int)character).ToString("x4"));
                        else builder.Append(character);
                        break;
                }

            return builder.Append('"').ToString();
        }

        // ---------------------------------------------------------------- Describing a failure

        /// <summary>
        /// The message, then every inner exception, then the loader exceptions. A type that fails
        /// to load reports nothing useful in its own message, and that is the failure this codebase
        /// hits most: the generator and the target microservice disagreeing on an Olive version.
        /// </summary>
        public static string Describe(string context, Exception error)
        {
            var builder = new StringBuilder();

            if (context.HasContent()) builder.Append(context).Append(": ");
            builder.Append(error?.Message.Or("Unknown error.") ?? "Unknown error.");

            foreach (var inner in Inners(error))
                builder.AppendLine().Append("caused by ").Append(inner.GetType().Name).Append(": ").Append(inner.Message);

            foreach (var line in LoaderMessages(error))
                builder.AppendLine().Append("could not load: ").Append(line);

            return builder.ToString();
        }

        static IEnumerable<Exception> Inners(Exception error)
        {
            var result = new List<Exception>();

            for (var current = error?.InnerException; current != null; current = current.InnerException)
            {
                if (current is AggregateException aggregate)
                    result.AddRange(aggregate.Flatten().InnerExceptions);
                else result.Add(current);

                if (result.Count > 10) break;
            }

            return result;
        }

        static IEnumerable<string> LoaderMessages(Exception error)
        {
            var result = new List<string>();

            for (var current = error; current != null; current = current.InnerException)
            {
                if (!(current is ReflectionTypeLoadException typeLoad)) continue;

                result.AddRange(typeLoad.LoaderExceptions.ExceptNull().Select(x => x.Message));
            }

            return result.Distinct().Take(20);
        }

        static string StackOf(Exception error)
        {
            var result = error?.StackTrace;

            for (var inner = error?.InnerException; inner != null; inner = inner.InnerException)
                if (inner.StackTrace.HasContent()) result = inner.StackTrace;

            return result;
        }

        static string OneLine(string text) => text.Or("").Replace("\r", "").Replace("\n", " ");

        // ---------------------------------------------------------------- Odds and ends

        static StreamWriter Create(string path)
        {
            try
            {
                var folder = Path.GetDirectoryName(Path.GetFullPath(path));
                if (folder.HasContent()) Directory.CreateDirectory(folder);

                // No byte order mark: the json sink is read line by line by other tools, and a
                // mark on the first line is not valid json. Flushed on every line, because a run
                // the build agent kills must still leave its log behind.
                return new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
                {
                    AutoFlush = true
                };
            }
            catch (Exception ex)
            {
                Complain("Could not open the log file " + path + ": " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// The log's own problems go to standard error. They are not records, and standard output
        /// may be a stream that another program is parsing.
        /// </summary>
        static void Complain(string message) => Console.Error.WriteLine(message);

        static bool CanColour()
        {
            try { return !Console.IsOutputRedirected; }
            catch (IOException) { return false; }
        }

        static LogFormat? ParseFormat(string value)
        {
            if (value.IsBlank()) return null;

            switch (value.Trim().ToLowerInvariant())
            {
                case "text": return LogFormat.Text;
                case "json": case "ndjson": return LogFormat.Json;
                default:
                    Complain("Unrecognised log format '" + value + "'. Use text or json.");
                    return null;
            }
        }

        static LogLevel? ParseLevel(string value)
        {
            if (value.IsBlank()) return null;

            switch (value.Trim().ToLowerInvariant())
            {
                case "q": case "quiet": case "error": return LogLevel.Quiet;
                case "m": case "minimal": case "warn": return LogLevel.Minimal;
                case "n": case "normal": case "info": return LogLevel.Normal;
                case "d": case "detailed": case "debug": return LogLevel.Detailed;
                case "diag": case "diagnostic": case "trace": return LogLevel.Diagnostic;
                default:
                    Complain("Unrecognised verbosity '" + value +
                        "'. Use quiet, minimal, normal, detailed or diagnostic.");
                    return null;
            }
        }

        static string Param(string[] args, string key)
        {
            var prefix = "/" + key + ":";

            return args.FirstOrDefault(x => x != null && x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                ?.Substring(prefix.Length).Trim();
        }

        static bool Is(string argument, string name)
            => name.Equals(argument?.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

namespace OliveGenerator.Logging
{
    using System.Collections.Generic;
    using System.Linq;

    /// <summary>
    /// The few string helpers the log needs. It is compiled into every generator, including ones
    /// that run before the target application's Olive version has been settled, so it cannot use
    /// Olive's own.
    /// </summary>
    static class LogTextExtensions
    {
        public static bool IsBlank(this string value) => string.IsNullOrWhiteSpace(value);

        public static bool HasContent(this string value) => !string.IsNullOrWhiteSpace(value);

        public static string Or(this string value, string fallback) => value.HasContent() ? value : fallback;

        public static IEnumerable<T> ExceptNull<T>(this IEnumerable<T> items) where T : class
            => items == null ? Enumerable.Empty<T>() : items.Where(x => x != null);

        public static bool None<T>(this IEnumerable<T> items) => items == null || !items.Any();

        public static IEnumerable<T> OrNone<T>(this IEnumerable<T> items) => items ?? Enumerable.Empty<T>();

        public static string Join(this IEnumerable<string> items, string separator)
            => string.Join(separator, items.Where(x => x.HasContent()));
    }
}
