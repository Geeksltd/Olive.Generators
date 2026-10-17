namespace OliveGenerator
{
    using System;
    using System.Linq;

    /// <summary>
    /// Where the time went, printed on request with /timing.
    /// <para>
    /// Most of a run is spent in child processes, and which of them dominates depends on the
    /// target: a cold nuget restore, the M# metadata dump, packing. Without a breakdown, tuning
    /// this is guesswork.
    /// </para>
    /// <para>
    /// The measuring itself now belongs to <see cref="Log"/>: every step of a run is a timed scope
    /// whether or not this breakdown was asked for, so the durations are already on each step's
    /// closing line. This aggregates them by step name at the end of the run.
    /// </para>
    /// </summary>
    static class Timings
    {
        public static bool Enabled { get; private set; }

        public static void Initialize(string[] args)
            => Enabled = args?.Any(x => (x ?? "").ToLower().StartsWith("/timing")) == true;

        /// <summary>Runs the given step, recording how long it took.</summary>
        public static T Measure<T>(string step, Func<T> action) => Log.Step(step, action);

        public static void Measure(string step, Action action) => Log.Step(step, action);

        /// <summary>Writes the breakdown to standard error, so it stays out of the tool's output.</summary>
        public static void Report()
        {
            if (!Enabled) return;

            Console.Error.WriteLine();
            Console.Error.WriteLine("Timing breakdown");

            const int Width = 48;

            foreach (var group in Log.Steps.GroupBy(x => x.Key).OrderByDescending(x => x.Sum(v => v.Value.Ticks)))
            {
                var time = TimeSpan.FromTicks(group.Sum(x => x.Value.Ticks));
                Console.Error.WriteLine($"  {Fit(group.Key, Width)} {time.TotalSeconds,8:N2}s  x{group.Count()}");
            }

            Console.Error.WriteLine($"  {Fit("TOTAL", Width)} {Log.Elapsed.TotalSeconds,8:N2}s");
        }

        /// <summary>Keeps the columns lined up, however long a step's name turned out to be.</summary>
        static string Fit(string text, int width)
            => text.Length <= width ? text.PadRight(width) : text.Substring(0, width - 1) + "\u2026";
    }
}
