using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using Olive;

namespace OliveGenerator
{
    class MSharpProjectCreator : ProjectCreator
    {
        static DirectoryInfo FrameworkDirectoryInfo = Context.MSharp.GetSubDirectory("lib").GetDirectories().WithMax(x => x.Name.StartsWith("net"));
        protected override string Framework => FrameworkDirectoryInfo?.Name;

        static Dictionary<string, Dictionary<string, Dictionary<string, string>>> _MSharpMetadata;
        static Dictionary<string, Dictionary<string, Dictionary<string, string>>> MSharpMetadata { get { return _MSharpMetadata ??= LoadMsharpMetadata(); } }

        public MSharpProjectCreator() : base("MSharp") { }

        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadMsharpMetadata()
        {
            var workingDir = FrameworkDirectoryInfo;
            var data = System.Threading.Tasks.Task.Factory.RunSync(() => "dotnet msharp.dsl.dll".Run(c => c.WorkingDirectory = workingDir.FullName));

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

            return result;
        }

        internal T? TryGetMetadataValueFor<T>(Type type, string property, string attribute) where T : struct =>
            MSharpMetadata.TryGet(type.Name)?.TryGet(property)?.TryGet(attribute)?.TryParseAs<T>();

        [EscapeGCop]
        internal override string IconUrl => "http://licensing.msharp.co.uk/images/icon.png";

        protected override PackageReference[] References => new[]
        {
           PackageReference.Olive,
           PackageReference.MSharp
        };

        protected override void AddFiles()
        {
            foreach (var item in Context.ExposedTypes)
            {
                Console.Write("Adding M# model class " + item.GetType().Name + "...");
                Folder.GetFile((string)item.GetType().Name + ".cs")
                    .WriteAllText(new MSharpModelProgrammer(this, item).Generate());
                Console.WriteLine("Done");
            }
        }
    }
}