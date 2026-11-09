using Olive;
using System.Linq;
using System.Text;

namespace OliveGenerator
{
    /// <summary>
    /// Generates <c>{Endpoint}Ef</c> — EF Core consumer endpoint (AMP #182377 non-M# path).
    /// </summary>
    internal class SubscriptionEfEndpointProgrammer
    {
        static string EndpointFullName => Context.Current.EndpointType.FullName;
        static string EndpointName => Context.Current.EndpointType.Name;
        static string PublisherNamespace => Context.Current.EndpointType.Namespace.Replace(".", " ").ToPascalCaseId();

        internal static string Generate()
        {
            var className = EndpointName + "Ef";
            var r = new StringBuilder();

            r.AppendLine("using System;");
            r.AppendLine("using System.Reflection;");
            r.AppendLine("using System.Threading.Tasks;");
            r.AppendLine("using Microsoft.EntityFrameworkCore;");
            r.AppendLine("using Olive;");
            r.AppendLine("using Olive.Entities.EF.Replication;");
            r.AppendLine();
            r.AppendLine("namespace " + PublisherNamespace);
            r.AppendLine("{");
            r.AppendLine($"public sealed class {className} : DestinationEndpoint");
            r.AppendLine("{");
            r.AppendLine($"const string EndpointTypeFullName = \"{EndpointFullName}\";");
            r.AppendLine();
            r.AppendLine("static string ResolveQueueUrl() =>");
            r.AppendLine("    Config.Get(\"DataReplication:\" + EndpointTypeFullName + \":Url\")");
            r.AppendLine("    .Or(Config.Get(\"DataReplication:\" + EndpointTypeFullName.Replace(\".\", \"_\") + \":Url\"))");
            r.AppendLine("    .Or(\"FOR_DEVELOPMENT_ONLY_DataReplication_\" + EndpointTypeFullName);");
            r.AppendLine();
            r.AppendLine("public override string QueueUrl => ResolveQueueUrl();");
            r.AppendLine();
            r.AppendLine($"public {className}(Assembly domainAssembly, DbContext dbContext) : base(domainAssembly, dbContext)");
            r.AppendLine("{");

            var exposedTypes = Context.Current.ExposedTypes.ToList();
            var firstEntityName = exposedTypes.First().GetType().Name;

            foreach (var item in exposedTypes)
                r.AppendLine($"    Register(\"{item.GetType().Namespace}.{item.GetType().Name}\");");

            r.AppendLine("}");
            r.AppendLine();
            r.AppendLine($"public static Assembly DomainAssembly => typeof({firstEntityName}).Assembly;");
            r.AppendLine();
            r.AppendLine("public static void ConfigureModel(ModelBuilder modelBuilder) =>");
            r.AppendLine("    modelBuilder.ConfigureReplicationEntities("
                + string.Join(", ", exposedTypes.Select(x => "typeof(" + x.GetType().Name + ")")) + ");");
            r.AppendLine();
            r.AppendLine("public static bool IsQueueConfigured =>");
            r.AppendLine("    ResolveQueueUrl().HasValue() && !ResolveQueueUrl().StartsWith(\"FOR_DEVELOPMENT_ONLY_\", StringComparison.Ordinal);");
            r.AppendLine();
            r.AppendLine("public static Task SubscribeAsync(DbContext dbContext, Assembly domainAssembly, bool isRefreshMessageRequired = false) =>");
            r.AppendLine($"    new {className}(domainAssembly, dbContext).Subscribe(isRefreshMessageRequired);");
            r.AppendLine("}");
            r.AppendLine("}");

            return new CSharpFormatter(r.ToString()).Format();
        }
    }
}
