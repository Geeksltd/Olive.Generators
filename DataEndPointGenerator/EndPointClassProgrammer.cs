using System;
using System.Text;
using Olive;

namespace OliveGenerator
{
    class EndpointClassProgrammer
    {
        static Type Endpoint => Context.EndpointType;
        static string ClassName => Endpoint.Name;

        public static string Generate()
        {
            if (Endpoint.Namespace?.EndsWith("Service") != true) 
                throw new Exception($"Endpoint namespace '{Endpoint.Namespace}' should ends with 'Service' keyword");

            if (!Endpoint.Name.ToLower().EndsWith("endpoint")) 
                throw new Exception($"Endpoint class name '{Endpoint.Name}' should ends with 'Endpoint' keyword (case insensitive)");

            if (Endpoint.FullName.Contains("_")) 
                throw new Exception($"Endpoint class name '{Endpoint.Name}' or namespace '{Endpoint.Namespace}' should not contains '_'");

            if (Endpoint.FullName.Split('.').Length!= 2) 
                throw new Exception($"Endpoint class name '{Endpoint.Name}' or namespace '{Endpoint.Namespace}' should not contains extra '.'");

            var r = new StringBuilder();

            r.AppendLine("namespace " + Endpoint.Namespace.Replace(".", " ").ToPascalCaseId());
            r.AppendLine("{");
            r.AppendLine("using Olive;");
            r.AppendLine("using Olive.Entities.Replication;");
            r.AppendLine();

            r.AppendLine($"public class {ClassName} : DestinationEndpoint");
            r.AppendLine("{");

            r.AppendLine($"public override string QueueUrl => Olive.Config.Get(\"DataReplication:{Endpoint.FullName}:Url\").Or(Olive.Entities.Data.Replication.QueueUrlProvider.UrlProvider.GetUrl(GetType()));");
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