using Olive;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Linq;
using System.Xml.XPath;

namespace OliveGenerator
{
    class MSharpProjectCreator : ProjectCreator
    {
        const string NETCOREAPP3_1 = "netcoreapp3.1";
        static Dictionary<string, Dictionary<string, Dictionary<string, string>>> _MSharpMetadata;
        static Dictionary<string, Dictionary<string, Dictionary<string, string>>> MSharpMetadata { get { return _MSharpMetadata ??= LoadMsharpMetadata(); } }

        public static Dictionary<string, Dictionary<string, Dictionary<string, string>>> LoadMsharpMetadata()
        {
            var workingDir = Context.MSharp.GetSubDirectory($"lib\\{NETCOREAPP3_1}");
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

        public MSharpProjectCreator() : base("MSharp") { }

        protected override string Framework => NETCOREAPP3_1;

        [EscapeGCop]
        internal override string IconUrl => "http://licensing.msharp.co.uk/images/icon.png";

        protected override string[] References => new[] { "Olive", "MSharp" };

        protected override void AddFiles()
        {
            foreach (var item in Context.ExposedTypes)
            {
                Console.Write("Adding M# model class " + item.GetType().Name + "...");
                Folder.GetFile(item.GetType().Name + ".cs").WriteAllText(new MSharpModelProgrammer(this, item).Generate());
                Console.WriteLine("Done");
            }
        }
    }
}