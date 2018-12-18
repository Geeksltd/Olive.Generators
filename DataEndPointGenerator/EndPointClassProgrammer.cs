using Olive;
using System;
using System.Linq;
using System.Text;

namespace OliveGenerator
{
    class EndpointClassProgrammer
    {
        static Type Endpoint => Context.EndpointType;
        static string ClassName => Endpoint.Name;

        public static string Generate()
        {
            var r = new StringBuilder();

            r.AppendLine("namespace " + Endpoint.Namespace);
            r.AppendLine("{");
            r.AppendLine("using System;");
            r.AppendLine("using System.Threading.Tasks;");
            r.AppendLine("using Olive.Entities;");
            r.AppendLine("using Olive.Entities.Replication;");
            r.AppendLine();

            r.AppendLine($"public class {ClassName} : DestinationEndpoint");
            r.AppendLine("{");

            r.AppendLine($"public override string QueueUrl => Olive.Config.GetOrThrow(\"DataReplication:{Endpoint.FullName}:Url\");");
            r.AppendLine();

            foreach (var item in Context.ExposedTypes)
                r.AppendLine($"public static EndpointSubscriber {item.GetType().Name} {{ get; private set; }}");
            r.AppendLine();

            r.AppendLine($"public {ClassName}(System.Reflection.Assembly domainAssembly) : base(domainAssembly)");
            r.AppendLine("{");
            foreach (var item in Context.ExposedTypes)
                r.AppendLine($"    {item.GetType().Name} = Register(\"{item.GetType().Namespace}.{item.GetType().Name}\");");
            r.AppendLine("}");
            r.AppendLine();

            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }


    }
}