namespace OliveGenerator
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Xml.Linq;
    using Olive;

    /// <summary>
    /// Gives the temporary projects the same nuget feeds as the microservice being generated from.
    /// <para>
    /// They are built under the temp folder, which is nowhere near the target's solution, so nuget
    /// never saw its nuget.config and private feeds were invisible. Every reference to a package
    /// published only on a private feed then failed to restore, and the generators recovered by
    /// rewriting the project against their own default versions and building the whole thing a
    /// second time.
    /// </para>
    /// </summary>
    static class TargetNugetConfig
    {
        const string FileName = "nuget.config";

        /// <summary>
        /// Writes the target's nuget configuration into the given folder, so the temporary projects
        /// restore from the same feeds. Does nothing when the target has none.
        /// </summary>
        public static void WriteInto(DirectoryInfo folder, FileInfo targetAssembly)
        {
            if (folder?.Exists() != true) return;

            var chain = FindChain(targetAssembly?.Directory);

            if (chain.None())
            {
                Log.Info("The target application has no nuget.config, so the generated projects " +
                    "restore from the default feeds only.");
                return;
            }

            try
            {
                var document = Merge(chain);

                document.Save(folder.GetFile(FileName).FullName);
                Log.Info("Using the nuget feeds of " + chain.Select(x => x.FullName).ToString(", "));

                foreach (var source in PackageSources(document).Elements())
                    Log.Detail($"feed {KeyOf(source).Or("(unnamed)")}: {(string)source.Attribute("value")}");
            }
            catch (Exception ex)
            {
                Log.Warn($"Failed to adopt {chain.First().FullName}: {ex.Message}");
            }
        }

        /// <summary>
        /// Removes the copy this run wrote.
        /// <para>
        /// Always, and as soon as the run ends, because the copy can carry the target's feed
        /// credentials. They have to be there while the generated projects restore — that is the
        /// whole point of adopting the file, and a private feed is exactly the case it exists for —
        /// but the run folder it sits in lives under the shared temp directory, where anything else
        /// on a build agent can read it and where it stays until somebody prunes it days later.
        /// Nothing else in the run folder is a secret, so the rest is left for diagnosis.
        /// </para>
        /// </summary>
        public static void RemoveFrom(DirectoryInfo folder)
        {
            try
            {
                var file = folder?.GetFile(FileName);

                if (file?.Exists() != true) return;

                file.Delete();
                Log.Detail("removed the adopted " + file.FullName);
            }
            catch (Exception ex)
            {
                // Said plainly rather than swallowed: what is left behind may hold a credential.
                Log.Warn($"Could not remove the nuget configuration this run copied into " +
                    $"{folder?.FullName}: {ex.Message}. Delete it by hand if it carries a feed credential.");
            }
        }

        /// <summary>
        /// Merges the package sources of the whole chain into the nearest configuration, the way
        /// nuget itself would have done had the projects been built inside the target's solution.
        /// Everything else, such as credentials and source mappings, comes from the nearest one.
        /// </summary>
        static XDocument Merge(FileInfo[] chain)
        {
            var document = XDocument.Load(chain.First().FullName);
            MakeSourcePathsAbsolute(document, chain.First().Directory);

            var sources = PackageSources(document);
            var known = new HashSet<string>(sources.Elements().Select(KeyOf).ExceptNull(),
                StringComparer.OrdinalIgnoreCase);

            foreach (var file in chain.Skip(1))
            {
                // A "clear" hides everything declared further out, so there is nothing left to add.
                if (sources.Elements().Any(x => x.Name.LocalName == "clear")) break;

                var outer = XDocument.Load(file.FullName);
                MakeSourcePathsAbsolute(outer, file.Directory);

                foreach (var item in PackageSources(outer).Elements().ToArray())
                {
                    if (item.Name.LocalName == "clear") { sources.Add(item); break; }

                    var key = KeyOf(item);
                    if (key != null && !known.Add(key)) continue; // A nearer config already won.

                    sources.Add(item);
                }
            }

            return document;
        }

        static string KeyOf(XElement element) => (string)element.Attribute("key");

        static XElement PackageSources(XDocument document)
        {
            var result = document.Descendants().FirstOrDefault(x => x.Name.LocalName == "packageSources");

            if (result == null)
            {
                result = new XElement("packageSources");
                (document.Root ?? new XElement("configuration")).Add(result);
            }

            return result;
        }

        /// <summary>
        /// A local feed is usually written relative to the config that declares it. Copied as is,
        /// it would be resolved against the temp folder, which is on an unrelated part of the disk.
        /// </summary>
        static void MakeSourcePathsAbsolute(XDocument document, DirectoryInfo origin)
        {
            foreach (var item in PackageSources(document).Elements())
            {
                var value = (string)item.Attribute("value");

                if (value.IsEmpty()) continue;
                if (value.Contains("://")) continue; // A feed url, not a path.
                if (Path.IsPathRooted(value)) continue;

                item.SetAttributeValue("value", Path.GetFullPath(Path.Combine(origin.FullName, value)));
            }
        }

        /// <summary>
        /// Every nuget.config at or above the target application's folder, nearest first. Nuget
        /// reads the whole chain, not just the closest one, so anything less can miss a feed.
        /// </summary>
        static FileInfo[] FindChain(DirectoryInfo from)
        {
            var result = new List<FileInfo>();

            for (var folder = from; folder != null; folder = folder.Parent)
            {
                var found = folder.GetFiles()
                    .FirstOrDefault(x => x.Name.Equals(FileName, StringComparison.OrdinalIgnoreCase));

                if (found != null) result.Add(found);
            }

            return result.ToArray();
        }
    }
}
