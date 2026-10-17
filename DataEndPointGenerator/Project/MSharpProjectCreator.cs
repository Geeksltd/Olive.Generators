using Olive;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Xml;

namespace OliveGenerator
{
    class MSharpProjectCreator : ProjectCreatorBase
    {
        const string LegacyFramework = "net46";

        /// <summary>The model reader this generator runs to read the M# metadata.</summary>
        const string DslAssembly = "MSharp.DSL.dll";

        static readonly Lazy<DirectoryInfo> Chosen = new Lazy<DirectoryInfo>(ChooseFrameworkDirectory);

        static DirectoryInfo FrameworkDirectoryInfo => Chosen.Value;

        /// <summary>The framework the M# package is built for, readable without creating a project, for the hash.</summary>
        internal static string ModelFramework => FrameworkDirectoryInfo?.Name;

        protected override string Framework => FrameworkDirectoryInfo?.Name;

        /// <summary>
        /// Which of the M# installation's lib folders to read the model from. They are named after
        /// target frameworks, and an installation that has been upgraded keeps the folder it was
        /// built against before alongside the current one.
        /// <para>
        /// This used to pick between them by maximising a boolean, so which of "net8.0" and
        /// "net10.0" won came down to the order the file system happened to list them in. The one
        /// left behind by an upgrade can be missing the runtimeconfig.json that
        /// "dotnet MSharp.DSL.dll" needs, which is how an upgraded M# turned every endpoint of a
        /// run into a failure that named a framework nobody had asked for.
        /// </para>
        /// </summary>
        static DirectoryInfo ChooseFrameworkDirectory()
        {
            var candidates = Context.Current.MSharp.GetSubDirectory("lib")
                .GetDirectories()
                .Where(x => x.Name.StartsWith("net", StringComparison.OrdinalIgnoreCase))
                .Where(x => Holds(x, DslAssembly))
                .OrderByDescending(x => Holds(x, Path.ChangeExtension(DslAssembly, ".runtimeconfig.json")))
                .ThenByDescending(x => FrameworkVersion(x.Name))
                .ToArray();

            foreach (var skipped in candidates.Skip(1))
                Log.Detail($"ignoring the M# model in {skipped.Name}, which is not the newest runnable one.");

            return candidates.FirstOrDefault();
        }

        /// <summary>Whether a folder holds the named file, whatever the file system does about case.</summary>
        static bool Holds(DirectoryInfo folder, string file)
            => folder.GetFiles().Any(x => x.Name.Equals(file, StringComparison.OrdinalIgnoreCase));

        /// <summary>
        /// The framework a lib folder is named after, so that "net10.0" sorts above "net8.0" rather
        /// than below it as a string does. The old style has no dot and spells one part per digit,
        /// so "net46" is 4.6 and not 46.
        /// <para>Spelt out in full because this type inherits a Version of its own.</para>
        /// </summary>
        static System.Version FrameworkVersion(string folder)
        {
            var none = new System.Version(0, 0);

            // A trailing platform, as in "net8.0-windows", plays no part in which is the newer.
            var name = folder.TrimStart("net").Split('-')[0];

            if (name.None(char.IsDigit)) return none;

            if (!name.Contains('.')) name = name.Where(char.IsDigit).Select(x => x.ToString()).ToString(".");

            return System.Version.TryParse(name, out var result) ? result : none;
        }

        /// <summary>
        /// One project targeting both, rather than two projects generating identical sources.
        /// The nuspec still lists a lib folder per framework, so the package is unchanged.
        /// </summary>
        protected override string[] Frameworks
            => new[] { Framework, LegacyFramework }.ExceptNull().Distinct().ToArray();

        static Dictionary<string, Dictionary<string, Dictionary<string, string>>> _MSharpMetadata;

        static Dictionary<string, Dictionary<string, Dictionary<string, string>>> MSharpMetadata
        {
            get { return _MSharpMetadata ??= LoadMsharpMetadata(); }
        }

        public MSharpProjectCreator() : base(
            Context.Current.TempPath.GetOrCreateSubDirectory(Context.Current.EndpointType.FullName + ".MSharp"))
        {
        }

        /// <summary>The data endpoint packages have always carried the usual assembly attributes.</summary>
        protected override bool GenerateAssemblyInfo => true;

        protected override string ProjectDescription => "Auto-generated package for data endpoint: " + Name;

        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadMsharpMetadata()
        {
            var folder = FrameworkDirectoryInfo
                ?? throw new Exception($"No lib folder of {Context.Current.MSharp.FullName} holds " +
                    $"{DslAssembly}, so the M# model cannot be read.");

            using var scope = Log.Step("Read the M# model metadata from " + folder.FullName);

            var data = ProcessRunner.DotNet(DslAssembly, folder);

            var result = new Dictionary<string, Dictionary<string, Dictionary<string, string>>>();

            var xdoc = new XmlDocument();
            xdoc.LoadXml(data);

            foreach (XmlNode type in xdoc.SelectNodes("./*/EntityType"))
            {
                var typeName = type["ClassName"].InnerText;
                result.Add(typeName, new Dictionary<string, Dictionary<string, string>>());

                foreach (XmlNode property in xdoc.SelectNodes("./*/*[./EntityType='" + type.Attributes["ID"].Value + "']"))
                {
                    var propertyName = property["Title"]?.InnerText;
                    if (propertyName.IsEmpty()) continue;
                    result[typeName][propertyName] = new Dictionary<string, string>();

                    foreach (XmlNode attr in property.ChildNodes)
                        result[typeName][propertyName].Add(attr.Name, attr.InnerText);
                }
            }

            scope.Note($"{result.Count} entity type(s)");

            return result;
        }

        internal T? TryGetMetadataValueFor<T>(Type type, string property, string attribute) where T : struct =>
            MSharpMetadata.TryGet(type.Name)?.TryGet(property)?.TryGet(attribute)?.TryParseAs<T>();

        [EscapeGCop]
        internal override string IconUrl => "http://licensing.msharp.co.uk/images/icon.png";

        /// <summary>What the M# project references, readable without creating one, for the hash.</summary>
        internal static PackageReference[] Packages => new[]
        {
           PackageReference.Olive,
           PackageReference.MSharp
        };

        internal override PackageReference[] References => Packages;

        /// <summary>Olive does not target the legacy framework, so that leg carries only M#.</summary>
        protected override PackageReference[] ReferencesFor(string framework)
            => framework == LegacyFramework ? new[] { PackageReference.MSharp } : References;

        protected override void AddFiles()
        {
            foreach (var item in Context.Current.ExposedTypes)
            {
                using (Log.Step("Write M# model class " + item.GetType().Name, LogLevel.Detailed))
                    Folder.GetFile((string)item.GetType().Name + ".cs")
                        .WriteAllText(new MSharpModelProgrammer(this, item).Generate());
            }
        }
    }
}
