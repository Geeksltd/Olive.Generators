namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using OliveGenerator.Logging;

    /// <summary>One nuget package a run produced, and where it ended up.</summary>
    class PackageResult
    {
        public string Id, Version, Destination;
        public bool Failed;
        public string Failure;

        internal string Status => Failed ? "FAILED" : "published";
    }

    /// <summary>What a run made of one endpoint, command or controller.</summary>
    class ItemResult
    {
        public string Name;
        public bool Failed;
        public string Failure;

        /// <summary>Short facts worth carrying to the summary, e.g. how many types were exposed.</summary>
        public readonly List<string> Notes = new List<string>();

        public readonly List<PackageResult> Packages = new List<PackageResult>();

        internal string Status
        {
            get
            {
                if (Failed || Packages.Any(x => x.Failed)) return "FAILED";
                return Packages.Any() ? "OK" : "NOTHING";
            }
        }

        public ItemResult Note(string note)
        {
            if (note.HasContent()) Notes.Add(note);
            return this;
        }

        public ItemResult Fail(string why)
        {
            Failed = true;
            Failure = why;
            return this;
        }

        public PackageResult Package(string id)
        {
            var result = Packages.FirstOrDefault(x => x.Id == id);

            if (result == null) Packages.Add(result = new PackageResult { Id = id });

            return result;
        }
    }

    /// <summary>
    /// What the run produced, printed as one table when it ends.
    /// <para>
    /// A batch of endpoints used to leave the reader to reconstruct the outcome by reading the
    /// whole log: which ones were generated, which package version each ended up at, where it was
    /// published, and which failed and why. This states it once, at every verbosity but quiet, and
    /// after a failure as well as a success.
    /// </para>
    /// </summary>
    class RunSummary
    {
        public static RunSummary Current { get; } = new RunSummary();

        readonly List<ItemResult> Items = new List<ItemResult>();

        /// <summary>The word for one item of this generator's runs, e.g. "endpoint".</summary>
        public string ItemNoun = "item";

        public ItemResult For(string name)
        {
            lock (Items)
            {
                var result = Items.FirstOrDefault(x => x.Name == name);

                if (result == null) Items.Add(result = new ItemResult { Name = name });

                return result;
            }
        }

        /// <summary>Whether anything at all went wrong, so the caller can settle the exit code.</summary>
        public bool AnyFailed
        {
            get { lock (Items) return Items.Any(x => x.Status == "FAILED"); }
        }

        public void Report()
        {
            ItemResult[] items;
            lock (Items) items = Items.ToArray();

            if (items.None()) return;

            var failed = items.Count(x => x.Status == "FAILED");
            var packages = items.SelectMany(x => x.Packages).Count(x => !x.Failed);

            // The totals are about the run, not about whichever item happened to be generated last.
            Log.Item = null;

            Log.Headline("");
            Log.SummaryTotals("Run summary: " + Count(items.Length, ItemNoun) + ", " +
                Count(packages, "package") + " published, " +
                (failed == 0 ? "none failed" : failed + " failed") +
                ", in " + Log.Duration(Log.Elapsed),
                items.Length, packages, failed);

            var width = items.Select(x => x.Name.Or("").Length).Concat(new[] { 20 }).Max();

            foreach (var item in items)
            {
                // Every record names the item it is about in its head, so each row of the table is
                // written while the log points at the item that row describes.
                Log.Item = item.Name;

                Log.SummaryItem(
                    "  " + item.Name.Or("(unnamed)").PadRight(width) + "  " + item.Status.PadRight(8) +
                        item.Notes.Join(", "),
                    item.Status, item.Notes.Join(", "), Reason(item.Failure));

                // Only for a reader: a parser already has it as the record's own failure field.
                if (item.Failure.HasContent() && !Log.IsJson)
                    Log.Headline("  " + new string(' ', width) + "  " + "why: " + First(item.Failure), failed: true);

                foreach (var package in item.Packages)
                    Log.SummaryPackage(
                        "      " + package.Id + " " + package.Version +
                            (package.Failed
                                ? "  FAILED: " + First(package.Failure)
                                : "  -> " + package.Destination.Or("(not published)")),
                        package.Id, package.Version, package.Destination,
                        package.Status, Reason(package.Failure));
            }

            Log.Item = null;
        }

        static string Count(int number, string noun)
            => number + " " + noun + (number == 1 ? "" : "s");

        /// <summary>The reason a thing failed, or nothing at all when it did not.</summary>
        static string Reason(string text) => text.HasContent() ? First(text) : null;

        /// <summary>The summary is a table, so a multi-line failure is cut to its first line.</summary>
        static string First(string text)
            => text.Or("").Replace("\r", "").Split('\n').FirstOrDefault(x => x.HasContent()).Or("(no detail)");
    }
}
