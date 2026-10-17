using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Olive;

namespace OliveGenerator
{
    internal class PackageExtractor
    {
        /// <summary>
        /// Every package a project references.
        /// <para>
        /// This used to read one ItemGroup: the first whose first child was a PackageReference.
        /// A project that spreads its references over several groups, which is what conditioning
        /// them on the target framework produces, had all but one group silently dropped, and one
        /// whose first child was a comment threw.
        /// </para>
        /// </summary>
        static internal NugetPackage[] Extract(FileInfo project)
        {
            var document = XDocument.Load(project.FullName);

            return document.Descendants()
                .Where(x => x.Name.LocalName == "PackageReference")
                .Select(x => new
                {
                    Include = (string)x.Attribute("Include") ?? (string)x.Attribute("Update"),
                    Version = (string)x.Attribute("Version")
                })
                .Where(x => x.Include.HasValue() && x.Version.HasValue())
                // The same package and version legitimately appears more than once in a project
                // that conditions its references on the target framework.
                .GroupBy(x => x.Include + "/" + x.Version, StringComparer.OrdinalIgnoreCase)
                .Select(x => x.First())
                .Select(x => new NugetPackage(x.Include, x.Version))
                .ToArray();
        }
    }
}
