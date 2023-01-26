using System.IO;
using System.Linq;
using System.Xml;
using Olive;

namespace OliveGenerator
{
    internal class PackageExtractor
    {
        static internal NugetPackage[] Extract(FileInfo project)
        {
            var xml = new XmlDocument();
            xml.Load(project.FullName);

            var containerNode =
                xml.SelectNodes(@"/Project/ItemGroup")
                .Cast<XmlNode>()
                .Where(n => n.FirstChild.Name == "PackageReference")
                .FirstOrDefault();

            if (containerNode == null) return new NugetPackage[0];

            return containerNode.ChildNodes.Cast<XmlNode>()
                 .Where(x => x.Attributes != null)
                 .Select(x => new { Include = x.Attributes["Include"], Version = x.Attributes["Version"] })
                 .Where(x => x.Include != null && x.Version != null)
                 .Select(x => new NugetPackage(x.Include.Value, x.Version.Value))
                 .ToArray();
        }
    }
}